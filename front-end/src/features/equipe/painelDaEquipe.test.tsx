import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Esquema } from '../../shared/api/tipos'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { renderizarTela } from '../../test/telas'

type Painel = Esquema<'PainelGestor'>
type Equipe = Esquema<'FuncionarioEquipeResposta'>
type Calendario = Esquema<'FeriasCalendario'>
const ex = <T,>(op: string, status?: number, nome?: string) =>
  exemploDoContrato<T>(op, status, nome)
const PAINEL = ex<Painel>('obterPainelDaEquipe')
const EQUIPE = ex<Equipe>('listarEquipeDoGestor')
const CALENDARIO = ex<Calendario>('obterCalendarioDeFeriasDaEquipe')
const problemaDoContrato = (op: string, status: number, nome?: string) =>
  problema(status, ex<Record<string, unknown>>(op, status, nome))

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
})
afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
})

async function abrir(rota = '/gestor/equipe') {
  const tela = renderizarTela(rota, { perfil: 'Gestor' })
  await screen.findByRole('heading', { level: 1, name: 'Equipe Comercial' })
  return tela
}
describe('Painel da equipe (tela 51)', { timeout: 20000 }, () => {
  it('titulo, subtitulo e o botao da avaliacao com o ciclo; as tres rotas vao sem parametros, conforme o contrato', async () => {
    const painel = capturar('obterPainelDaEquipe')
    const equipe = capturar('listarEquipeDoGestor')
    const calendario = capturar('obterCalendarioDeFeriasDaEquipe')
    await abrir()
    expect(
      screen.getByText(
        '8 pessoas. Você vê o ponto, as férias, os treinamentos e as advertências de quem está no seu setor.',
      ),
    ).toBeInTheDocument()
    const botao = screen.getByRole('link', { name: 'Avaliar a equipe (ciclo 2026.2)' })
    expect(botao).toHaveAttribute(
      'href',
      `/eu/formularios/${PAINEL.avaliacao!.formularioId}/responder`,
    )
    await screen.findByRole('table', { name: 'Pessoas da equipe' })
    await screen.findByRole('table', { name: 'Férias da equipe por semana' })
    for (const captura of [painel, equipe, calendario]) {
      expect(captura.ultima().consulta.size).toBe(0)
      captura.conforme()
    }
  })

  it('faixa "Hoje": trabalhando, de ferias e quem esta sem registro; o que e zero some', async () => {
    await abrir()
    const faixa = await screen.findByRole('region', { name: 'Hoje' })
    expect(await within(faixa).findByText('6 trabalhando')).toBeInTheDocument()
    expect(within(faixa).getByText('1 de férias')).toBeInTheDocument()
    expect(within(faixa).getByText('1 sem registro: Diego Farias')).toBeInTheDocument()
    expect(within(faixa).queryByText(/atestado|ainda não começaram/)).toBeNull()
  })

  it('faixa "Hoje" mostra atestado e "ainda nao comecaram" quando sao maiores que zero e omite o trecho "sem registro" quando e zero', async () => {
    capturar('listarEquipeDoGestor', {
      corpo: {
        ...EQUIPE,
        contagensHoje: { ...EQUIPE.contagensHoje, semRegistro: 0, atestado: 2, aindaNaoComecou: 1 },
        itens: EQUIPE.itens.map((i) => ({ ...i, situacaoHoje: 'TRABALHANDO' as const })),
      },
    })
    await abrir()
    const faixa = await screen.findByRole('region', { name: 'Hoje' })
    expect(await within(faixa).findByText('2 com atestado')).toBeInTheDocument()
    expect(within(faixa).getByText('1 ainda não começaram')).toBeInTheDocument()
    expect(within(faixa).queryByText(/sem registro/)).toBeNull()
  })

  it('dia sem expediente: a faixa diz so "Hoje não há expediente."', async () => {
    capturar('listarEquipeDoGestor', { corpo: { ...EQUIPE, diaUtil: false } })
    await abrir()
    const faixa = await screen.findByRole('region', { name: 'Hoje' })
    expect(await within(faixa).findByText('Hoje não há expediente.')).toBeInTheDocument()
    expect(within(faixa).queryByText(/trabalhando/)).toBeNull()
  })

  it('"Esperando sua aprovacao": primeiro as ferias e depois os ajustes de ponto, na ordem da API', async () => {
    await abrir()
    const secao = await screen.findByRole('region', { name: 'Esperando sua aprovação' })
    const cartoes = await within(secao).findAllByRole('listitem')
    expect(cartoes.map((c) => c.getAttribute('aria-label'))).toEqual([
      ...PAINEL.aguardandoAprovacao.ferias.map((f) => `Férias de ${f.funcionario.nome}`),
      ...PAINEL.aguardandoAprovacao.ajustesDePonto.map(
        (a) => `Ajuste de ponto de ${a.funcionario.nome}`,
      ),
    ])
    const ferias = within(secao).getByRole('listitem', { name: 'Férias de Pedro Santos' })
    expect(
      within(ferias).getByText(/Pedro Santos pediu 15 dias, de 10\/11 a 24\/11\./),
    ).toBeInTheDocument()
    expect(
      within(ferias).getByText(
        'Coincide 8 dias com as férias já aprovadas de Júlia Nunes (03/11 a 17/11). A equipe fica com 6 pessoas nesse período.',
      ),
    ).toBeInTheDocument()
    for (const nome of ['Aprovar', 'Sugerir outra data', 'Recusar'])
      expect(within(ferias).getByRole('button', { name: nome })).toBeEnabled()
    const ajuste = within(secao).getByRole('listitem', { name: 'Ajuste de ponto de Júlia Nunes' })
    expect(
      within(ajuste).getByText('Júlia Nunes pediu: Incluir o período 13:05 - 17:50'),
    ).toBeInTheDocument()
    expect(within(ajuste).getByText('Dia 05/10/2026')).toBeInTheDocument()
    expect(
      within(ajuste).getByText(/Esqueci de registrar quando voltei do almoço/),
    ).toBeInTheDocument()
  })

  it('sem nada esperando diz "Nada esperando sua aprovação."', async () => {
    capturar('obterPainelDaEquipe', {
      corpo: { ...PAINEL, aguardandoAprovacao: { ferias: [], ajustesDePonto: [] } },
    })
    await abrir()
    expect(await screen.findByText('Nada esperando sua aprovação.')).toBeInTheDocument()
  })

  describe('decidir as ferias', () => {
    async function comCartao() {
      const tela = await abrir()
      const secao = await screen.findByRole('region', { name: 'Esperando sua aprovação' })
      const cartao = await within(secao).findByRole('listitem', { name: 'Férias de Pedro Santos' })
      return { ...tela, cartao }
    }

    it('"Aprovar" chama aprovarFeriasComoGestor sem corpo e recarrega o painel, o calendario e os contadores do menu', async () => {
      const aprovar = capturar('aprovarFeriasComoGestor')
      const painel = capturar('obterPainelDaEquipe')
      const calendario = capturar('obterCalendarioDeFeriasDaEquipe')
      const contadores = capturar('obterContadoresDoMenuDoGestor')
      const { user, cartao } = await comCartao()
      const [antesPainel, antesCalendario] = [painel.quantas(), calendario.quantas()]
      await user.click(within(cartao).getByRole('button', { name: 'Aprovar' }))
      await waitFor(() => expect(aprovar.quantas()).toBe(1))
      expect(aprovar.ultima().parametrosDoCaminho).toEqual({
        feriasId: String(PAINEL.aguardandoAprovacao.ferias[0]!.id),
      })
      aprovar.conforme()
      expect(await screen.findByText('Pedido aprovado. Agora o RH confirma.')).toBeInTheDocument()
      await waitFor(() => expect(painel.quantas()).toBeGreaterThan(antesPainel))
      await waitFor(() => expect(calendario.quantas()).toBeGreaterThan(antesCalendario))
      expect(contadores.quantas()).toBeGreaterThan(0)
    })

    it('"Recusar" exige o motivo de 5 a 500 caracteres e manda { motivo }', async () => {
      const recusar = capturar('recusarFeriasComoGestor')
      const { user, cartao } = await comCartao()
      await user.click(within(cartao).getByRole('button', { name: 'Recusar' }))
      const modal = await screen.findByRole('dialog', { name: 'Recusar pedido de férias' })
      await user.type(within(modal).getByLabelText('Motivo'), 'curto')
      await user.clear(within(modal).getByLabelText('Motivo'))
      await user.type(within(modal).getByLabelText('Motivo'), 'abc')
      await user.click(within(modal).getByRole('button', { name: 'Recusar' }))
      expect(
        await within(modal).findByText('Escreva o motivo com 5 a 500 caracteres.'),
      ).toBeInTheDocument()
      expect(recusar.quantas()).toBe(0)
      await user.clear(within(modal).getByLabelText('Motivo'))
      await user.type(within(modal).getByLabelText('Motivo'), 'Fechamento do mês')
      await user.click(within(modal).getByRole('button', { name: 'Recusar' }))
      await waitFor(() => expect(recusar.quantas()).toBe(1))
      expect(recusar.ultima().corpo).toEqual({ motivo: 'Fechamento do mês' })
      recusar.conforme()
      expect(await screen.findByText('Pedido recusado.')).toBeInTheDocument()
    })

    it('"Sugerir outra data" manda { dataInicio, dataFim }; 422 mostra as regras e deixa a janela aberta', async () => {
      const sugerir = capturar('sugerirOutraDataDasFerias')
      const { user, cartao } = await comCartao()
      await user.click(within(cartao).getByRole('button', { name: 'Sugerir outra data' }))
      const modal = await screen.findByRole('dialog', { name: 'Sugerir outra data' })
      fireEvent.change(within(modal).getByLabelText('Primeiro dia'), {
        target: { value: '2026-12-07' },
      })
      fireEvent.change(within(modal).getByLabelText('Último dia'), {
        target: { value: '2026-12-21' },
      })
      await user.click(within(modal).getByRole('button', { name: 'Sugerir' }))
      await waitFor(() => expect(sugerir.quantas()).toBe(1))
      expect(sugerir.ultima().corpo).toEqual({ dataInicio: '2026-12-07', dataFim: '2026-12-21' })
      sugerir.conforme()
    })

    it('422 FERIAS_REGRAS_NAO_ATENDIDAS ao sugerir mostra o texto e as regras do erro', async () => {
      capturar(
        'sugerirOutraDataDasFerias',
        problema(422, {
          title: 'Operação recusada.',
          status: 422,
          detail: 'As datas sugeridas não atendem às regras de férias.',
          codigo: 'FERIAS_REGRAS_NAO_ATENDIDAS',
          regras: [
            { codigo: 'CABE_NO_SALDO', atendida: false, mensagem: 'Passa 4 dias do saldo.' },
          ],
        }),
      )
      const { user, cartao } = await comCartao()
      await user.click(within(cartao).getByRole('button', { name: 'Sugerir outra data' }))
      const modal = await screen.findByRole('dialog', { name: 'Sugerir outra data' })
      fireEvent.change(within(modal).getByLabelText('Primeiro dia'), {
        target: { value: '2026-12-07' },
      })
      fireEvent.change(within(modal).getByLabelText('Último dia'), {
        target: { value: '2026-12-21' },
      })
      await user.click(within(modal).getByRole('button', { name: 'Sugerir' }))
      expect(
        await within(modal).findByText('As datas sugeridas não atendem às regras de férias.'),
      ).toBeInTheDocument()
      expect(within(modal).getByText('Passa 4 dias do saldo.')).toBeInTheDocument()
    })

    it.each(['etapaIncorreta', 'jaDecidido'] as const)(
      '409 %s mostra o texto do contrato e recarrega o painel (outra pessoa decidiu antes)',
      async (nome) => {
        capturar(
          'aprovarFeriasComoGestor',
          problemaDoContrato('aprovarFeriasComoGestor', 409, nome),
        )
        const painel = capturar('obterPainelDaEquipe')
        const { user, cartao } = await comCartao()
        const antes = painel.quantas()
        await user.click(within(cartao).getByRole('button', { name: 'Aprovar' }))
        const detalhe = ex<{ detail: string }>('aprovarFeriasComoGestor', 409, nome).detail
        expect(await screen.findByText(detalhe)).toBeInTheDocument()
        await waitFor(() => expect(painel.quantas()).toBeGreaterThan(antes))
      },
    )

    it('podeDecidir false liga nada: os botoes ficam desligados e aparece o motivoNaoPodeDecidir', async () => {
      const pedido = PAINEL.aguardandoAprovacao.ferias[0]!
      capturar('obterPainelDaEquipe', {
        corpo: {
          ...PAINEL,
          aguardandoAprovacao: {
            ...PAINEL.aguardandoAprovacao,
            ferias: [
              {
                ...pedido,
                podeDecidir: false,
                motivoNaoPodeDecidir: 'Aguardando a análise do RH.',
              },
            ],
          },
        },
      })
      const { cartao } = await comCartao()
      expect(within(cartao).getByText('Aguardando a análise do RH.')).toBeInTheDocument()
      for (const nome of ['Aprovar', 'Sugerir outra data', 'Recusar'])
        expect(within(cartao).getByRole('button', { name: nome })).toBeDisabled()
    })

    it('abono, 13o e a mensagem da pessoa aparecem no cartao', async () => {
      const pedido = PAINEL.aguardandoAprovacao.ferias[0]!
      capturar('obterPainelDaEquipe', {
        corpo: {
          ...PAINEL,
          aguardandoAprovacao: {
            ...PAINEL.aguardandoAprovacao,
            ferias: [
              {
                ...pedido,
                abonoPecuniario: true,
                diasAbono: 5,
                adiantar13Salario: true,
                observacao: 'Quero emendar com o feriado',
              },
            ],
          },
        },
      })
      const { cartao } = await comCartao()
      expect(within(cartao).getByText(/Quer vender 5 dias\./)).toBeInTheDocument()
      expect(within(cartao).getByText(/Quer adiantar a 1ª parcela do 13º\./)).toBeInTheDocument()
      expect(within(cartao).getByText('“Quero emendar com o feriado”')).toBeInTheDocument()
    })
  })

  describe('decidir o ajuste de ponto', () => {
    const cartaoDoAjuste = async () => {
      const tela = await abrir()
      const secao = await screen.findByRole('region', { name: 'Esperando sua aprovação' })
      const cartao = await within(secao).findByRole('listitem', {
        name: 'Ajuste de ponto de Júlia Nunes',
      })
      return { ...tela, cartao }
    }

    it('"Aprovar" chama aprovarPedidoDeAjusteComoGestor e recarrega o painel', async () => {
      const aprovar = capturar('aprovarPedidoDeAjusteComoGestor')
      const painel = capturar('obterPainelDaEquipe')
      const { user, cartao } = await cartaoDoAjuste()
      const antes = painel.quantas()
      await user.click(within(cartao).getByRole('button', { name: 'Aprovar' }))
      await waitFor(() => expect(aprovar.quantas()).toBe(1))
      expect(aprovar.ultima().parametrosDoCaminho).toEqual({
        pedidoId: String(PAINEL.aguardandoAprovacao.ajustesDePonto[0]!.pedidoId),
      })
      aprovar.conforme()
      await waitFor(() => expect(painel.quantas()).toBeGreaterThan(antes))
    })

    it('"Recusar" chama recusarPedidoDeAjusteComoGestor', async () => {
      const recusar = capturar('recusarPedidoDeAjusteComoGestor')
      const { user, cartao } = await cartaoDoAjuste()
      await user.click(within(cartao).getByRole('button', { name: 'Recusar' }))
      await waitFor(() => expect(recusar.quantas()).toBe(1))
      recusar.conforme()
      expect(await screen.findByText('Pedido recusado.')).toBeInTheDocument()
    })

    it('409 mostra o texto do contrato e recarrega (outra pessoa decidiu antes)', async () => {
      capturar(
        'aprovarPedidoDeAjusteComoGestor',
        problema(409, {
          title: 'Conflito.',
          status: 409,
          detail: 'Este pedido já foi decidido.',
          codigo: 'PEDIDO_JA_DECIDIDO',
        }),
      )
      const painel = capturar('obterPainelDaEquipe')
      const { user, cartao } = await cartaoDoAjuste()
      const antes = painel.quantas()
      await user.click(within(cartao).getByRole('button', { name: 'Aprovar' }))
      expect(await screen.findByText('Este pedido já foi decidido.')).toBeInTheDocument()
      await waitFor(() => expect(painel.quantas()).toBeGreaterThan(antes))
    })
  })

  describe('quadro "Férias da equipe"', () => {
    it('uma linha por pessoa na ordem da API, barras com o texto do periodo e a legenda; quem nao tem ferias fica sem barra', async () => {
      await abrir()
      const quadro = await screen.findByRole('table', { name: 'Férias da equipe por semana' })
      const linhas = within(quadro).getAllByRole('row').slice(1)
      expect(linhas.map((l) => within(l).getByRole('rowheader').textContent)).toEqual(
        PAINEL.pessoas.map((p) => p.funcionario.nome),
      )
      const julia = within(quadro).getByRole('row', { name: /Júlia Nunes/ })
      const barra = within(julia).getByLabelText('Júlia Nunes: 03/11 a 17/11 (Aprovada)')
      expect(barra).toHaveAttribute('data-situacao', 'APROVADA')
      expect(barra.className).toMatch(/col-start-5/)
      expect(barra.className).toMatch(/col-span-3/)
      const pedro = within(quadro).getByLabelText('Pedro Santos: 10/11 a 24/11 (Aguardando você)')
      expect(pedro).toHaveAttribute('data-situacao', 'AGUARDANDO_VOCE')
      const helena = within(quadro).getByRole('row', { name: /Helena Prates/ })
      expect(within(helena).queryByRole('cell')).toBeNull()
      const legenda = screen.getByRole('list', { name: 'Legenda' })
      expect(
        within(legenda)
          .getAllByRole('listitem')
          .map((i) => i.textContent),
      ).toEqual(['Aprovada', 'Aguardando você', 'Aguardando o RH'])
      expect(screen.getByText('Outubro a dezembro, por semana')).toBeInTheDocument()
    })

    it('periodo que comeca antes da janela mostra "Até dd/mm" e o que passa do fim mostra "A partir de dd/mm"', async () => {
      capturar('obterCalendarioDeFeriasDaEquipe', {
        corpo: {
          ...CALENDARIO,
          itens: [
            {
              feriasId: 1,
              funcionario: PAINEL.pessoas[0]!.funcionario,
              dataInicio: '2026-09-28',
              dataFim: '2026-10-17',
              situacao: 'APROVADA',
            },
            {
              feriasId: 2,
              funcionario: PAINEL.pessoas[1]!.funcionario,
              dataInicio: '2026-12-23',
              dataFim: '2027-01-15',
              situacao: 'COM_O_RH',
            },
          ],
        },
      })
      await abrir()
      const quadro = await screen.findByRole('table', { name: 'Férias da equipe por semana' })
      expect(
        await within(quadro).findByLabelText(
          `${PAINEL.pessoas[0]!.funcionario.nome}: Até 17/10 (Aprovada)`,
        ),
      ).toBeInTheDocument()
      const depois = within(quadro).getByLabelText(
        `${PAINEL.pessoas[1]!.funcionario.nome}: A partir de 23/12 (Aguardando o RH)`,
      )
      expect(depois).toHaveAttribute('data-situacao', 'COM_O_RH')
    })

    it('erro no calendario nao derruba a tela: so o quadro mostra o aviso com "Tentar de novo"', async () => {
      capturar(
        'obterCalendarioDeFeriasDaEquipe',
        problema(500, { title: 'Erro interno.', status: 500 }),
      )
      await abrir()
      expect(
        await screen.findByText('Erro no servidor. Tente novamente em instantes.'),
      ).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
      expect(await screen.findByRole('table', { name: 'Pessoas da equipe' })).toBeInTheDocument()
    })
  })

  describe('tabela "Pessoas da equipe"', () => {
    it('colunas, ordem da API, ponto hoje por situacao, banco de horas com sinal, ferias, avaliacao e advertencias', async () => {
      await abrir()
      const tabela = await screen.findByRole('table', { name: 'Pessoas da equipe' })
      expect(
        within(tabela)
          .getAllByRole('columnheader')
          .map((c) => c.textContent),
      ).toEqual([
        'Pessoa',
        'Ponto hoje',
        'Banco de horas',
        'Férias disponíveis',
        'Avaliação 2026.2',
        'Advertências',
        'Ações',
      ])
      const linhas = (await within(tabela).findAllByRole('row')).slice(1)
      expect(linhas).toHaveLength(PAINEL.pessoas.length)
      const diego = within(tabela).getByText('Diego Farias').closest('tr')!
      expect(within(diego).getByText('Sem registro')).toBeInTheDocument()
      expect(within(diego).getByText('−3h45')).toBeInTheDocument()
      expect(within(diego).getByText('22 dias')).toBeInTheDocument()
      expect(within(diego).getByText('Pendente')).toBeInTheDocument()
      expect(within(diego).getByText('2')).toBeInTheDocument()
      const larissa = within(tabela).getByText('Larissa Moura').closest('tr')!
      expect(within(larissa).getByText('De férias até 17/10')).toBeInTheDocument()
      expect(within(larissa).getByText('Não se aplica')).toBeInTheDocument()
      expect(within(larissa).getByText('Nenhuma')).toBeInTheDocument()
      const fernanda = within(tabela).getByText('Fernanda Lopes').closest('tr')!
      expect(within(fernanda).getByText('Entrada 08:10')).toBeInTheDocument()
      expect(within(fernanda).getByText('+0h40')).toBeInTheDocument()
      expect(within(fernanda).getByText('Respondida')).toBeInTheDocument()
      const igor = within(tabela).getByText('Igor Batista').closest('tr')!
      expect(within(igor).getByText('0h00')).toBeInTheDocument()
    })

    it.each([
      ['ATESTADO', 'Atestado'],
      ['AINDA_NAO_COMECOU', 'Ainda não começou'],
      ['SEM_EXPEDIENTE', 'Sem expediente'],
    ] as const)('ponto hoje %s mostra "%s"', async (situacaoHoje, texto) => {
      capturar('obterPainelDaEquipe', {
        corpo: {
          ...PAINEL,
          pessoas: PAINEL.pessoas.map((p, i) =>
            i === 0 ? { ...p, pontoHoje: { situacaoHoje, horario: null, feriasAte: null } } : p,
          ),
        },
      })
      await abrir()
      const tabela = await screen.findByRole('table', { name: 'Pessoas da equipe' })
      const primeira = (
        await within(tabela).findByText(PAINEL.pessoas[0]!.funcionario.nome)
      ).closest('tr')!
      expect(within(primeira).getByText(texto)).toBeInTheDocument()
    })

    it('sem ciclo de avaliacao aberto (avaliacao nula) o botao e a coluna somem', async () => {
      capturar('obterPainelDaEquipe', { corpo: { ...PAINEL, avaliacao: null } })
      await abrir()
      const tabela = await screen.findByRole('table', { name: 'Pessoas da equipe' })
      await within(tabela).findByText('Diego Farias')
      expect(screen.queryByRole('link', { name: /Avaliar a equipe/ })).toBeNull()
      expect(within(tabela).queryByRole('columnheader', { name: /^Avaliação/ })).toBeNull()
    })

    it('"Abrir ficha" abre a ficha da pessoa sobre o painel (/gestor/equipe/21) e fechar volta a /gestor/equipe', async () => {
      const { user, roteador } = await abrir()
      const tabela = await screen.findByRole('table', { name: 'Pessoas da equipe' })
      await user.click(
        await within(tabela).findByRole('link', { name: 'Abrir a ficha de Diego Farias' }),
      )
      expect(roteador.state.location.pathname).toBe('/gestor/equipe/21')
      const ficha = await screen.findByRole('dialog')
      expect(within(ficha).getByText('Diego Farias', { selector: 'p' })).toBeInTheDocument()
      expect(
        screen.getByRole('heading', { level: 1, name: 'Equipe Comercial', hidden: true }),
      ).toBeInTheDocument()
      await user.click(within(ficha).getByRole('button', { name: 'Fechar' }))
      await waitFor(() => expect(roteador.state.location.pathname).toBe('/gestor/equipe'))
    })

    it('sem pessoas ativas diz "Seu setor ainda não tem pessoas ativas."', async () => {
      capturar('obterPainelDaEquipe', {
        corpo: {
          ...PAINEL,
          totalPessoas: 0,
          pessoas: [],
          aguardandoAprovacao: { ferias: [], ajustesDePonto: [] },
        },
      })
      await abrir()
      expect(await screen.findByText('Seu setor ainda não tem pessoas ativas.')).toBeInTheDocument()
    })
  })

  it('gestor sem setor: "Você ainda não é gestor de um setor." e nenhum bloco', async () => {
    capturar('obterPainelDaEquipe', {
      corpo: {
        setor: null,
        totalPessoas: 0,
        aguardandoAprovacao: { ferias: [], ajustesDePonto: [] },
        avaliacao: null,
        pessoas: [],
      },
    })
    renderizarTela('/gestor/equipe', { perfil: 'Gestor' })
    expect(await screen.findByText('Você ainda não é gestor de um setor.')).toBeInTheDocument()
    expect(screen.queryByRole('table', { name: 'Pessoas da equipe' })).toBeNull()
    expect(screen.queryByRole('region', { name: 'Hoje' })).toBeNull()
  })

  it('o texto cortado do Figma nao existe: "Lembrar Diego por e-mail"', async () => {
    await abrir()
    await screen.findByRole('table', { name: 'Pessoas da equipe' })
    const principal = within(screen.getByRole('main'))
    expect(principal.queryByText(/Lembrar/i)).toBeNull()
    expect(principal.queryByRole('button', { name: /e-mail/i })).toBeNull()
  })

  it('erro no painel mostra o aviso com "Tentar de novo" (e nenhum bloco depende dele para falhar sozinho)', async () => {
    capturar('obterPainelDaEquipe', problema(500, { title: 'Erro interno.', status: 500 }))
    renderizarTela('/gestor/equipe', { perfil: 'Gestor' })
    expect(
      await screen.findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
  })
})
