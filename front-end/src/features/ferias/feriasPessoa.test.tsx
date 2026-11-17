import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Esquema } from '../../shared/api/tipos'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { renderizarTela } from '../../test/telas'

type Minhas = Esquema<'FeriasEuResposta'>
type Pedido = Esquema<'FeriasPedido'>
const ex = <T,>(op: string, status?: number, nome?: string) =>
  exemploDoContrato<T>(op, status, nome)
const MINHAS = ex<Minhas>('obterMinhasFerias')
const PEDIDO = MINHAS.historico[0]!
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

async function abrir(rota = '/eu/ferias', perfil: 'Funcionario' | 'Gestor' | 'RH' = 'Funcionario') {
  const tela = renderizarTela(rota, { perfil })
  await screen.findByRole('heading', { level: 1, name: 'Férias' })
  return tela
}
const comHistorico = (historico: Pedido[], extra: Partial<Minhas> = {}) =>
  capturar('obterMinhasFerias', { corpo: { ...MINHAS, ...extra, historico } })

describe('Férias da pessoa (telas 34 e 48)', { timeout: 20000 }, () => {
  it('quatro numeros, periodos, historico e "Como funciona", conforme o contrato', async () => {
    const minhas = capturar('obterMinhasFerias')
    await abrir()
    const numeros = await screen.findByRole('region', { name: 'Números das férias' })
    expect(await within(numeros).findByText('18 dias')).toBeInTheDocument()
    expect(within(numeros).getByText('Do período 2025 a 2026')).toBeInTheDocument()
    expect(within(numeros).getByText('13/03/2027')).toBeInTheDocument()
    expect(within(numeros).getByText('Nenhuma')).toBeInTheDocument()
    expect(within(numeros).getByText('Nada agendado ainda')).toBeInTheDocument()
    const periodos = screen.getByRole('region', { name: /períodos aquisitivos/ })
    expect(within(periodos).getByText('Tire até 13/03/2027')).toBeInTheDocument()
    expect(within(periodos).getByText(/12 dias tirados e 18 dias disponíveis/)).toBeInTheDocument()
    expect(within(periodos).getByText('Em andamento')).toBeInTheDocument()
    expect(
      within(periodos).getByText(
        /6 de 12 meses trabalhados\. Os 30 dias desse período ficam disponíveis a partir de 14\/03\/2027\./,
      ),
    ).toBeInTheDocument()
    const historico = screen.getByRole('table', { name: 'Histórico' })
    expect(within(historico).getByText('16/11/2026 a 30/11/2026')).toBeInTheDocument()
    expect(within(historico).getByText('Aguardando o gestor')).toBeInTheDocument()
    expect(within(historico).getAllByText('Concluída').length).toBeGreaterThan(0)
    expect(
      screen.getByText(
        'Peça com pelo menos 30 dias de antecedência. Marina Costa analisa e depois o RH confirma.',
      ),
    ).toBeInTheDocument()
    expect(minhas.ultima().consulta.size).toBe(0)
    minhas.conforme()
  })

  it('sem gestor que analisa (gestora, setor sem gestor ou gestor desligado) a ultima linha e "O RH analisa o pedido."', async () => {
    capturar('obterMinhasFerias', { corpo: { ...MINHAS, gestorQueAnalisa: null } })
    await abrir('/eu/ferias', 'Gestor')
    expect(
      await screen.findByText(
        'Peça com pelo menos 30 dias de antecedência. O RH analisa o pedido.',
      ),
    ).toBeInTheDocument()
    expect(screen.queryByText(/analisa e depois o RH confirma/)).toBeNull()
  })

  it('o historico mostra a frase de cada decisao, inclusive o motivo da recusa e o "gestor ausente"', async () => {
    comHistorico([
      {
        ...PEDIDO,
        id: 901,
        situacao: 'REJEITADA',
        decisaoGestor: {
          decisao: 'REJEITADA',
          decididaPor: { id: 3, nome: 'Marina Costa', ativo: true },
          decididaEmUtc: '2026-10-08T12:00:00Z',
          automatica: false,
          observacao: 'Fechamento do mês: nesse período não dá.',
        },
        decisaoRh: null,
      },
      {
        ...PEDIDO,
        id: 902,
        dataInicio: '2026-12-01',
        dataFim: '2026-12-10',
        situacao: 'COM_O_RH',
        decisaoGestor: {
          decisao: 'APROVADA',
          decididaPor: null,
          decididaEmUtc: '2026-10-09T12:00:00Z',
          automatica: true,
        },
        decisaoRh: null,
      },
    ])
    await abrir()
    const historico = await screen.findByRole('table', { name: 'Histórico' })
    expect(
      await within(historico).findByText(
        'Recusado por Marina Costa em 08/10: Fechamento do mês: nesse período não dá.',
      ),
    ).toBeInTheDocument()
    expect(within(historico).getByText('Recusada')).toBeInTheDocument()
    expect(
      within(historico).getByText('Aprovado automaticamente (gestor ausente)'),
    ).toBeInTheDocument()
    expect(within(historico).getByText('Aguardando o RH')).toBeInTheDocument()
  })

  it('sem pedidos o historico diz "Você ainda não pediu férias."', async () => {
    comHistorico([])
    await abrir()
    expect(await screen.findByText('Você ainda não pediu férias.')).toBeInTheDocument()
  })

  it('"Pedir férias" abre o painel sobre a tela (/eu/ferias/pedir) e "Cancelar" volta', async () => {
    const { user, roteador } = await abrir()
    await user.click(screen.getByRole('link', { name: 'Pedir férias' }))
    const painel = await screen.findByRole('dialog', { name: 'Pedir férias' })
    expect(roteador.state.location.pathname).toBe('/eu/ferias/pedir')
    expect(
      screen.getByRole('heading', { level: 1, name: 'Férias', hidden: true }),
    ).toBeInTheDocument()
    await user.click(within(painel).getByRole('button', { name: 'Cancelar' }))
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/eu/ferias'))
  })

  it('erro ao carregar mostra o aviso com "Tentar de novo"', async () => {
    capturar('obterMinhasFerias', problema(500, { title: 'Erro interno.', status: 500 }))
    renderizarTela('/eu/ferias', { perfil: 'Funcionario' })
    expect(
      await screen.findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
  })

  describe('sugestao de outra data', () => {
    const SUGESTAO: Pedido = {
      ...PEDIDO,
      id: 905,
      situacao: 'SUGESTAO_DE_DATA',
      podeDecidir: false,
      motivoNaoPodeDecidir: 'Esperando a resposta de Bruno à sugestão de outra data.',
      sugestao: {
        dataInicio: '2026-12-07',
        dataFim: '2026-12-21',
        dias: 15,
        sugeridaPor: { id: 3, nome: 'Marina Costa', ativo: true },
        sugeridaEmUtc: '2026-10-07T12:00:00Z',
        respostaDaPessoa: null,
        respondidaEmUtc: null,
        expiraEm: '2026-10-12',
      },
    }
    async function abrirComSugestao() {
      comHistorico([SUGESTAO])
      const tela = await abrir()
      const bloco = await screen.findByRole('region', { name: 'Sugestão de outra data' })
      return { ...tela, bloco }
    }

    it('mostra quem sugeriu, as datas, os dias e ate quando responder, e os dois botoes', async () => {
      const { bloco } = await abrirComSugestao()
      expect(
        within(bloco).getByText(
          'Marina Costa sugeriu outras datas: 07/12 a 21/12 (15 dias). Responda até 12/10/2026.',
        ),
      ).toBeInTheDocument()
      expect(
        within(bloco).getByRole('button', { name: 'Aceitar as novas datas' }),
      ).toBeInTheDocument()
      expect(within(bloco).getByRole('button', { name: 'Recusar' })).toBeInTheDocument()
    })

    it('"Aceitar as novas datas" chama aceitarSugestaoDeDataDasFerias e pede as ferias de novo', async () => {
      const minhas = comHistorico([SUGESTAO])
      const aceitar = capturar('aceitarSugestaoDeDataDasFerias')
      const tela = await abrir()
      const bloco = await screen.findByRole('region', { name: 'Sugestão de outra data' })
      const antes = minhas.quantas()
      await tela.user.click(within(bloco).getByRole('button', { name: 'Aceitar as novas datas' }))
      await waitFor(() => expect(aceitar.quantas()).toBe(1))
      expect(aceitar.ultima().parametrosDoCaminho).toEqual({ feriasId: '905' })
      aceitar.conforme()
      await waitFor(() => expect(minhas.quantas()).toBeGreaterThan(antes))
    })

    it('"Recusar" chama recusarSugestaoDeDataDasFerias e o texto exato de que o pedido foi encerrado continua na tela depois de a lista vir de novo (o pedido ja e CANCELADA)', async () => {
      const recusar = capturar('recusarSugestaoDeDataDasFerias')
      const { user, bloco } = await abrirComSugestao()
      const depois = comHistorico([
        {
          ...SUGESTAO,
          situacao: 'CANCELADA',
          sugestao: { ...SUGESTAO.sugestao!, respostaDaPessoa: 'RECUSOU' },
        },
      ])
      await user.click(within(bloco).getByRole('button', { name: 'Recusar' }))
      await waitFor(() => expect(recusar.quantas()).toBe(1))
      expect(recusar.ultima().parametrosDoCaminho).toEqual({ feriasId: '905' })
      recusar.conforme()
      await waitFor(() => expect(depois.quantas()).toBeGreaterThan(0))
      await waitFor(() =>
        expect(screen.queryByRole('region', { name: 'Sugestão de outra data' })).toBeNull(),
      )
      expect(
        screen.getByText(
          'Seu pedido foi encerrado. Converse com o seu gestor antes de abrir outro pedido de férias.',
        ),
      ).toBeInTheDocument()
    })

    it('422 ao aceitar (as datas novas nao cabem) mostra o detalhe e as regras que vierem', async () => {
      capturar(
        'aceitarSugestaoDeDataDasFerias',
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
      const { user, bloco } = await abrirComSugestao()
      await user.click(within(bloco).getByRole('button', { name: 'Aceitar as novas datas' }))
      expect(
        await within(bloco).findByText('As datas sugeridas não atendem às regras de férias.'),
      ).toBeInTheDocument()
      expect(within(bloco).getByText('Passa 4 dias do saldo.')).toBeInTheDocument()
    })

    it.each(['etapaIncorreta', 'jaDecidido'] as const)(
      '409 %s mostra o texto do contrato e pede as ferias de novo (outra pessoa decidiu antes)',
      async (nome) => {
        const minhas = comHistorico([SUGESTAO])
        capturar(
          'aceitarSugestaoDeDataDasFerias',
          problemaDoContrato('aceitarSugestaoDeDataDasFerias', 409, nome),
        )
        const tela = await abrir()
        const bloco = await screen.findByRole('region', { name: 'Sugestão de outra data' })
        const antes = minhas.quantas()
        await tela.user.click(within(bloco).getByRole('button', { name: 'Aceitar as novas datas' }))
        const detalhe = ex<{ detail: string }>('aceitarSugestaoDeDataDasFerias', 409, nome).detail
        expect(await within(bloco).findByText(detalhe)).toBeInTheDocument()
        await waitFor(() => expect(minhas.quantas()).toBeGreaterThan(antes))
      },
    )
  })
})

describe('Painel "Pedir férias" (tela 35)', { timeout: 20000 }, () => {
  const abrirPainel = async (perfil: 'Funcionario' | 'Gestor' | 'RH' = 'Funcionario') => {
    const tela = renderizarTela('/eu/ferias/pedir', { perfil })
    const painel = await screen.findByRole('dialog', { name: 'Pedir férias' })
    return { ...tela, painel }
  }
  const datas = (painel: HTMLElement, inicio: string, fim: string) => {
    fireEvent.change(within(painel).getByLabelText('Primeiro dia'), { target: { value: inicio } })
    fireEvent.change(within(painel).getByLabelText('Último dia'), { target: { value: fim } })
  }

  it('abre com o saldo, a pergunta da mensagem pelo nome do gestor e o rodape sem prometer e-mail de pedido criado', async () => {
    const { painel } = await abrirPainel()
    expect(
      await within(painel).findByText('Você tem 18 dias disponíveis para tirar até 13/03/2027.'),
    ).toBeInTheDocument()
    expect(
      within(painel).getByLabelText('Mensagem para Marina Costa (opcional)'),
    ).toBeInTheDocument()
    expect(
      within(painel).getByText(
        'Marina Costa analisa primeiro e depois o RH confirma. Você recebe um e-mail a cada decisão.',
      ),
    ).toBeInTheDocument()
    expect(within(painel).getByRole('button', { name: 'Enviar pedido' })).toBeDisabled()
  })

  it('sem gestor na primeira etapa o rotulo e o rodape falam do RH', async () => {
    capturar('obterMinhasFerias', { corpo: { ...MINHAS, gestorQueAnalisa: null } })
    const { painel } = await abrirPainel('Gestor')
    expect(
      await within(painel).findByLabelText('Mensagem para o RH (opcional)'),
    ).toBeInTheDocument()
    expect(
      within(painel).getByText('O RH analisa o pedido. Você recebe um e-mail a cada decisão.'),
    ).toBeInTheDocument()
  })

  it('nao confere com datas vazias nem com o fim antes do inicio; confere com as duas datas e mostra todas as regras que vierem', async () => {
    const conferir = capturar('conferirPedidoDeFerias')
    const { painel } = await abrirPainel()
    await within(painel).findByText(/Você tem 18 dias/)
    datas(painel, '2026-11-16', '2026-11-10')
    expect(
      within(painel).getByText('O último dia não pode ser antes do primeiro.'),
    ).toBeInTheDocument()
    await new Promise((r) => setTimeout(r, 450))
    expect(conferir.quantas()).toBe(0)
    datas(painel, '2026-11-16', '2026-11-30')
    await waitFor(() => expect(conferir.quantas()).toBe(1))
    expect(conferir.ultima().corpo).toEqual({
      dataInicio: '2026-11-16',
      dataFim: '2026-11-30',
      abonoPecuniario: false,
      diasAbono: 0,
    })
    conferir.conforme()
    const exemplo = ex<Esquema<'FeriasConferencia'>>('conferirPedidoDeFerias')
    expect(
      await within(painel).findByText(
        `${exemplo.totalDias} dias corridos. Sobram ${exemplo.diasQueSobram} dias.`,
      ),
    ).toBeInTheDocument()
    const lista = within(painel).getByRole('list', { name: 'Verificações' })
    expect(within(lista).getAllByRole('listitem')).toHaveLength(exemplo.regras.length)
    for (const regra of exemplo.regras)
      expect(within(lista).getByText(regra.mensagem)).toBeInTheDocument()
  })

  it('"Vender parte das férias" abre "Quantos dias" e a conferencia leva abonoPecuniario e diasAbono', async () => {
    const conferir = capturar('conferirPedidoDeFerias')
    const { user, painel } = await abrirPainel()
    datas(painel, '2026-11-16', '2026-11-30')
    await user.click(
      within(painel).getByRole('checkbox', {
        name: 'Vender parte das férias (abono de até 10 dias)',
      }),
    )
    fireEvent.change(within(painel).getByLabelText('Quantos dias'), { target: { value: '5' } })
    await waitFor(() =>
      expect(conferir.ultima().corpo).toMatchObject({ abonoPecuniario: true, diasAbono: 5 }),
    )
  })

  it('"Enviar pedido" fica desligado com podeEnviar false e o painel mostra as regras nao atendidas', async () => {
    const exemplo = ex<Esquema<'FeriasConferencia'>>('conferirPedidoDeFerias')
    capturar('conferirPedidoDeFerias', {
      corpo: {
        ...exemplo,
        podeEnviar: false,
        regras: [{ codigo: 'CABE_NO_SALDO', atendida: false, mensagem: 'Passa 4 dias do saldo.' }],
      },
    })
    const { painel } = await abrirPainel()
    datas(painel, '2026-11-16', '2026-11-30')
    expect(await within(painel).findByText('Passa 4 dias do saldo.')).toBeInTheDocument()
    expect(within(painel).getByRole('button', { name: 'Enviar pedido' })).toBeDisabled()
  })

  it('resposta antiga e descartada: vale a ultima conferencia', async () => {
    const exemplo = ex<Esquema<'FeriasConferencia'>>('conferirPedidoDeFerias')
    let chamadas = 0
    const liberar: Array<() => void> = []
    capturar('conferirPedidoDeFerias', {
      espera: new Promise<void>((resolver) => liberar.push(resolver)),
      corpo: {
        ...exemplo,
        regras: [
          {
            codigo: 'CABE_NO_SALDO',
            atendida: true,
            mensagem: 'Resposta da primeira conferência.',
          },
        ],
      },
    })
    chamadas += 1
    const { painel } = await abrirPainel()
    datas(painel, '2026-11-16', '2026-11-30')
    await waitFor(() => expect(chamadas).toBe(1))
    await new Promise((r) => setTimeout(r, 400))
    capturar('conferirPedidoDeFerias', {
      corpo: {
        ...exemplo,
        regras: [
          { codigo: 'CABE_NO_SALDO', atendida: true, mensagem: 'Resposta da segunda conferência.' },
        ],
      },
    })
    datas(painel, '2026-11-16', '2026-12-04')
    expect(await within(painel).findByText('Resposta da segunda conferência.')).toBeInTheDocument()
    liberar.forEach((l) => l())
    await new Promise((r) => setTimeout(r, 100))
    expect(within(painel).queryByText('Resposta da primeira conferência.')).toBeNull()
  })

  it('enviar manda o corpo do contrato (com a mensagem), avisa, fecha o painel e pede as ferias e o painel de novo', async () => {
    const pedir = capturar('pedirFerias')
    const minhas = capturar('obterMinhasFerias')
    const { user, painel, roteador } = await abrirPainel()
    await within(painel).findByText(/Você tem 18 dias/)
    datas(painel, '2026-11-16', '2026-11-30')
    await user.type(
      within(painel).getByLabelText('Mensagem para Marina Costa (opcional)'),
      'Queria emendar com o feriado de 20/11',
    )
    await waitFor(() =>
      expect(within(painel).getByRole('button', { name: 'Enviar pedido' })).toBeEnabled(),
    )
    const antes = minhas.quantas()
    await user.click(within(painel).getByRole('button', { name: 'Enviar pedido' }))
    await waitFor(() => expect(pedir.quantas()).toBe(1))
    expect(pedir.ultima().corpo).toEqual({
      dataInicio: '2026-11-16',
      dataFim: '2026-11-30',
      abonoPecuniario: false,
      diasAbono: 0,
      adiantar13Salario: false,
      mensagem: 'Queria emendar com o feriado de 20/11',
    })
    pedir.conforme()
    expect(await screen.findByText('Pedido de férias enviado.')).toBeInTheDocument()
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/eu/ferias'))
    await waitFor(() => expect(minhas.quantas()).toBeGreaterThan(antes))
  })

  it('422 FERIAS_REGRAS_NAO_ATENDIDAS mostra as regras do erro e mantem o painel aberto', async () => {
    capturar('pedirFerias', problemaDoContrato('pedirFerias', 422, 'padrao'))
    const { user, painel } = await abrirPainel()
    await within(painel).findByText(/Você tem 18 dias/)
    datas(painel, '2026-11-16', '2026-11-30')
    await waitFor(() =>
      expect(within(painel).getByRole('button', { name: 'Enviar pedido' })).toBeEnabled(),
    )
    await user.click(within(painel).getByRole('button', { name: 'Enviar pedido' }))
    const erro = ex<{ detail: string; regras: { mensagem: string }[] }>(
      'pedirFerias',
      422,
      'padrao',
    )
    expect(await within(painel).findByText(erro.detail)).toBeInTheDocument()
    for (const regra of erro.regras)
      expect(within(painel).getByText(regra.mensagem)).toBeInTheDocument()
    expect(screen.getByRole('dialog', { name: 'Pedir férias' })).toBeInTheDocument()
  })
})
