import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Esquema } from '../../shared/api/tipos'
import { servidor } from '../../shared/mocks/servidor'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { escolherOpcao, preencherData } from '../../test/formulario'
import { renderizarTela } from '../../test/telas'
import { enviarAtestado } from './enviarAtestado'

vi.mock('./enviarAtestado', () => ({ enviarAtestado: vi.fn() }))

type PontoHoje = Esquema<'PontoHoje'>
type Espelho = Esquema<'PontoEspelhoProprio'>
type PaginaDePedidos = Esquema<'PontoPedidoAjustePagina'>
const HOJE = exemploDoContrato<PontoHoje>('obterPontoHoje')
const ESPELHO = exemploDoContrato<Espelho>('obterMeuEspelhoDePonto')
const PEDIDOS = exemploDoContrato<PaginaDePedidos>('listarMeusPedidosDeAjuste')
const problemaDoContrato = (op: string, status: number, nome?: string) =>
  problema(status, exemploDoContrato<Record<string, unknown>>(op, status, nome))

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
})

afterEach(() => {
  vi.useRealTimers()
  vi.clearAllMocks()
})

async function abrirMeuPonto(perfil: 'Funcionario' | 'Gestor' | 'RH' = 'Funcionario') {
  const tela = renderizarTela('/eu/meu-ponto', { perfil })
  await screen.findByRole('heading', { level: 1, name: 'Meu ponto' })
  return tela
}

const linhaDoDia = async (dia: string) => {
  const tabela = await screen.findByRole('table', { name: 'Registros do mês' })
  return (await within(tabela).findByText(dia)).closest('tr')!
}

describe('Seu ponto hoje (cartão das telas 32 e 33)', { timeout: 15000 }, () => {
  it('o botao vem de proximaMarcacao; um clique envia { tipoEsperado } uma vez e usa o cartao devolvido', async () => {
    const hoje = capturar('obterPontoHoje')
    const registrar = capturar('registrarMarcacaoDePonto')
    const { user } = await abrirMeuPonto()
    const cartao = await screen.findByRole('region', { name: 'Seu ponto hoje' })
    const botao = await within(cartao).findByRole('button', { name: 'Registrar entrada' })
    expect(within(cartao).getByText('Hoje, quarta-feira 7/10')).toBeInTheDocument()
    expect(within(cartao).getByText('Saída: aguardando registro')).toBeInTheDocument()
    await user.dblClick(botao)
    expect(
      await within(cartao).findByRole('button', { name: 'Registrar saída para almoço' }),
    ).toBeInTheDocument()
    expect(registrar.quantas()).toBe(1)
    expect(registrar.ultima().corpo).toEqual({ tipoEsperado: 'ENTRADA' })
    registrar.conforme()
    hoje.conforme()
    expect(within(cartao).getByText('Entrada 09:42')).toBeInTheDocument()
  })

  it.each([
    ['sequenciaMudou', 409],
    ['duplicada', 409],
  ])('409 %s mostra o detail e recarrega o cartao', async (nome, status) => {
    const hoje = capturar('obterPontoHoje')
    capturar(
      'registrarMarcacaoDePonto',
      problemaDoContrato('registrarMarcacaoDePonto', status, nome),
    )
    const { user } = await abrirMeuPonto()
    const cartao = await screen.findByRole('region', { name: 'Seu ponto hoje' })
    await user.click(await within(cartao).findByRole('button', { name: 'Registrar entrada' }))
    const detail = exemploDoContrato<{ detail: string }>(
      'registrarMarcacaoDePonto',
      status,
      nome,
    ).detail
    expect(await screen.findByText(detail)).toBeInTheDocument()
    await waitFor(() => expect(hoje.quantas()).toBeGreaterThan(1))
  })

  it('422 FUNCIONARIO_INATIVO mostra o detail', async () => {
    capturar(
      'registrarMarcacaoDePonto',
      problemaDoContrato('registrarMarcacaoDePonto', 422, 'funcionarioInativo'),
    )
    const { user } = await abrirMeuPonto()
    await user.click(await screen.findByRole('button', { name: 'Registrar entrada' }))
    expect(
      await screen.findByText('Este colaborador está inativo e não pode registrar ponto.'),
    ).toBeInTheDocument()
  })

  it('o relogio usa agoraUtc do servidor e uma batida as 22:30 de Brasilia aparece no dia certo', async () => {
    vi.setSystemTime(new Date('2026-10-07T15:00:00Z')) // o navegador está 2h17 adiantado
    servidor.use(
      http.get('*/api/eu/ponto/hoje', () =>
        HttpResponse.json({
          ...HOJE,
          agoraUtc: '2026-10-08T01:40:00Z',
          periodos: [
            {
              entrada: {
                id: 1,
                ordem: 1,
                tipo: 'ENTRADA',
                rotulo: 'Entrada',
                instanteUtc: '2026-10-08T01:30:00Z',
                origem: 'SISTEMA',
                corrigidaEmUtc: null,
              },
              saida: null,
            },
          ],
          proximaMarcacao: { tipo: 'SAIDA', rotulo: 'Saída', textoDoBotao: 'Registrar saída' },
        } satisfies PontoHoje),
      ),
    )
    renderizarTela('/eu/inicio', { perfil: 'Funcionario' })
    const cartao = await screen.findByRole('region', { name: 'Seu ponto hoje' })
    expect(await within(cartao).findByText('22:30')).toBeInTheDocument()
    expect(within(cartao).getByLabelText('Agora')).toHaveTextContent('22:40')
  })

  it('tela 32: quatro quadros, a proxima batida destacada, a jornada e o banco "Atualizado até ontem"', async () => {
    const { user } = renderizarTela('/eu/inicio', { perfil: 'Funcionario' })
    const cartao = await screen.findByRole('region', { name: 'Seu ponto hoje' })
    expect(await within(cartao).findByText('Jornada das 08:00 às 17:48')).toBeInTheDocument()
    const quadros = within(cartao).getAllByRole('listitem')
    expect(quadros.map((q) => q.firstChild?.textContent)).toEqual([
      'Entrada',
      'Saída almoço',
      'Volta',
      'Saída',
    ])
    expect(quadros[0]).toHaveAttribute('aria-current', 'step')
    const banco = screen.getByRole('region', { name: 'Banco de horas' })
    expect(within(banco).getByText('+6h20')).toBeInTheDocument()
    expect(within(banco).getByText('Atualizado até ontem')).toBeInTheDocument()
    expect(within(banco).getByRole('link', { name: 'Ver extrato' })).toHaveAttribute(
      'href',
      '/eu/meu-ponto/extrato',
    )
    await user.click(within(cartao).getByRole('button', { name: 'Pedir ajuste de ponto' }))
    expect(await screen.findByRole('dialog', { name: 'Pedir ajuste de ponto' })).toBeInTheDocument()
  })

  it('tela 32: o aviso do pedido em analise usa status=PENDENTE e a etapa; Ver pedido abre o detalhe', async () => {
    const lista = capturar('listarMeusPedidosDeAjuste', {
      corpo: { ...PEDIDOS, itens: [PEDIDOS.itens[0]], total: 1 },
    })
    const detalhe = capturar('obterMeuPedidoDeAjuste')
    const { user } = renderizarTela('/eu/inicio', { perfil: 'Funcionario' })
    expect(
      await screen.findByText(
        'Seu pedido de ajuste do dia 02/10 está em análise pelo RH. Você recebe um e-mail quando houver resposta.',
      ),
    ).toBeInTheDocument()
    expect(lista.ultima().consulta.get('status')).toBe('PENDENTE')
    lista.conforme()
    await user.click(screen.getByRole('button', { name: 'Ver pedido' }))
    const modal = await screen.findByRole('dialog', { name: 'Pedido de ajuste de ponto' })
    expect(await within(modal).findByText('Incluir o período 13:05 - 17:50')).toBeInTheDocument()
    expect(detalhe.ultima().parametrosDoCaminho).toEqual({ pedidoId: '301' })
    detalhe.conforme()
  })

  it('tela 32: mais de um pedido pendente vira a contagem', async () => {
    renderizarTela('/eu/inicio', { perfil: 'Funcionario' })
    expect(await screen.findByText('Você tem 2 pedidos de ajuste em análise.')).toBeInTheDocument()
  })
})

describe('Meu ponto (telas 33 e 47)', { timeout: 15000 }, () => {
  it('subtitulo, quatro numeros e a requisicao do espelho sem ano e mes (mes atual)', async () => {
    const espelho = capturar('obterMeuEspelhoDePonto')
    await abrirMeuPonto()
    expect(
      await screen.findByText('Outubro de 2026. Jornada de 44 horas semanais, das 08:00 às 17:48.'),
    ).toBeInTheDocument()
    const numeros = screen.getByRole('region', { name: 'Números do mês' })
    expect(within(numeros).getByText('+6h20')).toBeInTheDocument()
    expect(within(numeros).getByText('26h40')).toBeInTheDocument()
    expect(within(numeros).getByText('8h48')).toBeInTheDocument()
    expect(within(numeros).getByText('1')).toHaveAttribute('data-tom', 'alerta')
    expect(screen.getByRole('heading', { name: 'Registros de outubro' })).toBeInTheDocument()
    expect([...espelho.ultima().consulta.keys()]).toEqual([])
    espelho.conforme()
  })

  it('as linhas do mes do exemplo: saldos, Ajuste em analise, Folga com Sem expediente, Pedir ajuste e Falta compensada', async () => {
    await abrirMeuPonto()
    const dia1 = await linhaDoDia('01/10')
    expect(within(dia1).getByText('quinta')).toBeInTheDocument()
    expect(within(dia1).getByText('07:56 - 12:01')).toBeInTheDocument()
    expect(within(dia1).getByText('8h54')).toBeInTheDocument()
    expect(within(dia1).getByText('+0h06')).toBeInTheDocument()
    const dia2 = await linhaDoDia('02/10')
    expect(within(dia2).getByText('Ajuste em análise')).toBeInTheDocument()
    expect(within(dia2).getByText('13:02 - sem saída')).toBeInTheDocument()
    for (const d of ['03/10', '04/10']) {
      const folga = await linhaDoDia(d)
      expect(within(folga).getByText('Folga')).toBeInTheDocument()
      expect(within(folga).getByText('Sem expediente')).toBeInTheDocument()
    }
    const dia5 = await linhaDoDia('05/10')
    expect(within(dia5).getByText('−0h02')).toBeInTheDocument()
    expect(
      within(dia5).getByRole('button', { name: 'Pedir ajuste do dia 05/10' }),
    ).toBeInTheDocument()
    expect(within(await linhaDoDia('06/10')).getByText('+0h12')).toBeInTheDocument()
    expect(within(await linhaDoDia('07/10')).getByText('Falta compensada')).toBeInTheDocument()
  })

  it('o dia de hoje em andamento mostra "Hoje" e "em aberto"', async () => {
    const hoje = ESPELHO.dias.at(-1)!
    servidor.use(
      http.get('*/api/eu/ponto/espelho', () =>
        HttpResponse.json({
          ...ESPELHO,
          dias: [
            {
              ...hoje,
              ehHoje: true,
              situacao: 'EM_ANDAMENTO',
              podePedirAjuste: false,
              periodos: [
                {
                  entrada: {
                    id: 9,
                    ordem: 1,
                    tipo: 'ENTRADA',
                    rotulo: 'Entrada',
                    instanteUtc: '2026-10-07T10:58:00Z',
                    origem: 'SISTEMA',
                    corrigidaEmUtc: null,
                  },
                  saida: null,
                },
              ],
            },
          ],
        } satisfies Espelho),
      ),
    )
    await abrirMeuPonto()
    const linha = await linhaDoDia('07/10')
    expect(within(linha).getByText('Hoje')).toBeInTheDocument()
    expect(within(linha).getByText('07:58 - em aberto')).toBeInTheDocument()
  })

  it('mes vazio, pedidos vazios e erro do espelho com Tentar de novo', async () => {
    capturar('obterMeuEspelhoDePonto', { corpo: { ...ESPELHO, dias: [] } })
    capturar('listarMeusPedidosDeAjuste', { corpo: { ...PEDIDOS, itens: [], total: 0 } })
    const a = await abrirMeuPonto()
    expect(await screen.findByText('Nada registrado neste mês.')).toBeInTheDocument()
    expect(await screen.findByText('Você ainda não pediu ajustes.')).toBeInTheDocument()
    a.unmount()
    capturar('obterMeuEspelhoDePonto', problema(500))
    await abrirMeuPonto()
    expect(await screen.findAllByRole('button', { name: 'Tentar de novo' })).not.toHaveLength(0)
  })

  it('Meus pedidos: etapa RH sem nome do RH, etapa GESTOR com o gestor e a resposta de quem decidiu', async () => {
    const meu = exemploDoContrato<Esquema<'PontoPedidoAjuste'>>(
      'obterMeuPedidoDeAjuste',
      200,
      'esperaOGestor',
    )
    capturar('listarMeusPedidosDeAjuste', {
      corpo: { ...PEDIDOS, itens: [...PEDIDOS.itens, meu], total: 3 },
    })
    await abrirMeuPonto()
    const tabela = await screen.findByRole('table', { name: 'Meus pedidos de ajuste' })
    expect(await within(tabela).findByText('Aguardando a análise do RH')).toBeInTheDocument()
    expect(within(tabela).getByText('Aguardando a análise de Carlos Ribeiro')).toBeInTheDocument()
    expect(
      within(tabela).getByText('Roberto Alves: Conferido com o controle da portaria.'),
    ).toBeInTheDocument()
    expect(within(tabela).queryByText(/Aguardando a análise de Roberto/)).toBeNull()
    expect(within(tabela).getAllByText('Em análise')).toHaveLength(2)
    expect(within(tabela).getByText('Aprovado')).toBeInTheDocument()
    expect(within(tabela).getByText('05/10, 08:20')).toBeInTheDocument()
  })

  it('o bloco Banco de horas pede tamanhoPagina=4 e mostra so Credito e Debito', async () => {
    const banco = capturar('obterMeuBancoDeHoras')
    await abrirMeuPonto()
    const bloco = await screen.findByRole('region', { name: 'Banco de horas' })
    const lista = await within(bloco).findByRole('list', { name: 'Últimos lançamentos' })
    expect(within(lista).getAllByText('Crédito')).toHaveLength(2)
    expect(within(lista).getAllByText('Débito')).toHaveLength(2)
    expect(within(lista).getByText('06/10, hora extra')).toBeInTheDocument()
    expect(within(lista).getByText('+0h12')).toBeInTheDocument()
    expect(within(lista).getByText('−4h00')).toBeInTheDocument()
    expect(banco.ultima().consulta.get('tamanhoPagina')).toBe('4')
    banco.conforme()
  })

  it('o extrato completo abre em /eu/meu-ponto/extrato e filtra por periodo e tipo', async () => {
    const banco = capturar('obterMeuBancoDeHoras')
    const { user, roteador } = await abrirMeuPonto()
    await user.click(await screen.findByRole('link', { name: 'Ver extrato completo' }))
    const painel = await screen.findByRole('dialog', { name: 'Banco de horas' })
    expect(roteador.state.location.pathname).toBe('/eu/meu-ponto/extrato')
    expect(await within(painel).findByText('Atualizado até 06/10')).toBeInTheDocument()
    fireEvent.change(within(painel).getByLabelText('De'), { target: { value: '2026-09-01' } })
    fireEvent.change(within(painel).getByLabelText('Até'), { target: { value: '2026-09-30' } })
    await escolherOpcao(user, 'Tipo', 'Débito')
    await waitFor(() => expect(banco.ultima().consulta.get('tipo')).toBe('DEBITO'))
    const c = banco.ultima().consulta
    expect(c.get('de')).toBe('2026-09-01')
    expect(c.get('ate')).toBe('2026-09-30')
    banco.conforme()
    expect(within(painel).getAllByText('Saldo negativo do dia').length).toBeGreaterThan(0)
  })

  it('Meus atestados: periodo de dias e de horas, situacoes com motivo e sem link para o arquivo', async () => {
    const lista = capturar('listarMeusAtestados')
    await abrirMeuPonto()
    const tabela = await screen.findByRole('table', { name: 'Meus atestados' })
    expect(await within(tabela).findByText('07/10 a 08/10')).toBeInTheDocument()
    expect(within(tabela).getByText('Validado')).toBeInTheDocument()
    expect(within(tabela).getByText('O dia não conta como falta')).toBeInTheDocument()
    expect(within(tabela).getByText('25/09, das 13:00 às 17:48')).toBeInTheDocument()
    expect(within(tabela).getByText('Recusado')).toBeInTheDocument()
    expect(
      within(tabela).getByText(/O documento não informa o horário da consulta/),
    ).toBeInTheDocument()
    expect(within(tabela).queryAllByRole('link')).toHaveLength(0)
    lista.conforme()
  })

  it('o gestor usa a mesma tela com as rotas /api/eu', async () => {
    const espelho = capturar('obterMeuEspelhoDePonto')
    await abrirMeuPonto('Gestor')
    await waitFor(() => expect(espelho.quantas()).toBeGreaterThan(0))
    const menu = screen.getByRole('navigation', { name: 'Menu principal' })
    expect(within(menu).getByRole('link', { name: 'Meu ponto' })).toHaveAttribute(
      'aria-current',
      'page',
    )
  })
})

describe('Pedir ajuste de ponto (modal sem print)', { timeout: 15000 }, () => {
  async function abrirDoDia5() {
    const tela = await abrirMeuPonto()
    const linha = await linhaDoDia('05/10')
    await tela.user.click(within(linha).getByRole('button', { name: 'Pedir ajuste do dia 05/10' }))
    const modal = await screen.findByRole('dialog', { name: 'Pedir ajuste de ponto' })
    return { ...tela, modal }
  }

  async function preencherPeriodo(tela: Awaited<ReturnType<typeof abrirDoDia5>>) {
    const { user, modal } = tela
    await user.type(within(modal).getByLabelText('Hora da batida 1'), '13:05')
    await user.click(within(modal).getByRole('button', { name: 'Incluir uma batida' }))
    await escolherOpcao(user, 'Tipo da batida 2', 'Saída')
    await user.type(within(modal).getByLabelText('Hora da batida 2'), '17:50')
    await user.type(
      within(modal).getByLabelText('Justificativa'),
      'Esqueci de registrar a volta do almoço.',
    )
  }

  it('aberto pela linha, o dia vem fixo, mostra o registro atual e envia os instantes em UTC (-03:00), conforme', async () => {
    const criar = capturar('criarPedidoDeAjuste')
    const tela = await abrirDoDia5()
    const { modal, user } = tela
    expect(within(modal).getByLabelText('Dia do ponto')).toHaveValue('2026-10-05')
    expect(within(modal).getByLabelText('Dia do ponto')).toHaveAttribute('readonly')
    expect(within(modal).getByLabelText('Dia do ponto')).toHaveAttribute('max', '2026-10-06')
    expect(await within(modal).findByText('08:14 - 12:05')).toBeInTheDocument()
    await preencherPeriodo(tela)
    await user.click(screen.getByRole('button', { name: 'Enviar pedido' }))
    await waitFor(() => expect(criar.quantas()).toBe(1))
    expect(criar.ultima().corpo).toEqual({
      data: '2026-10-05',
      justificativa: 'Esqueci de registrar a volta do almoço.',
      itens: [
        { tipo: 'ENTRADA', instanteUtc: '2026-10-05T16:05:00Z' },
        { tipo: 'SAIDA', instanteUtc: '2026-10-05T20:50:00Z' },
      ],
    })
    criar.conforme()
    expect(
      await screen.findByText('Pedido enviado. Você recebe um e-mail a cada resposta.'),
    ).toBeInTheDocument()
    await waitFor(() =>
      expect(screen.queryByRole('dialog', { name: 'Pedir ajuste de ponto' })).toBeNull(),
    )
  })

  it('corrigir uma batida do dia manda o marcacaoId e o tipo dela; "No dia seguinte" soma um dia', async () => {
    const criar = capturar('criarPedidoDeAjuste')
    const tela = await abrirDoDia5()
    const { modal, user } = tela
    await within(modal).findByText('08:14 - 12:05')
    await escolherOpcao(user, 'Corrigir uma batida deste dia (1)', 'Saída 18:00')
    await user.type(within(modal).getByLabelText('Hora da batida 1'), '00:30')
    await user.click(within(modal).getByText('No dia seguinte (1)'))
    await user.type(within(modal).getByLabelText('Justificativa'), 'Fiquei até mais tarde.')
    await user.click(screen.getByRole('button', { name: 'Enviar pedido' }))
    await waitFor(() => expect(criar.quantas()).toBe(1))
    expect(criar.ultima().corpo).toMatchObject({
      itens: [{ tipo: 'SAIDA', instanteUtc: '2026-10-06T03:30:00Z', marcacaoId: 8811 }],
    })
    criar.conforme()
  })

  it('valida o dia ate ontem, a hora e a justificativa de 5 a 500 sem chamar a API', async () => {
    const criar = capturar('criarPedidoDeAjuste')
    const { user } = await abrirMeuPonto()
    await user.click(screen.getByRole('button', { name: 'Pedir ajuste' }))
    const modal = await screen.findByRole('dialog', { name: 'Pedir ajuste de ponto' })
    preencherData('Dia do ponto', '2026-10-07')
    await user.type(within(modal).getByLabelText('Justificativa'), 'abc')
    await user.click(screen.getByRole('button', { name: 'Enviar pedido' }))
    expect(
      await within(modal).findByText('Escolha um dia que já passou (até ontem).'),
    ).toBeInTheDocument()
    expect(within(modal).getByText('Hora inválida.')).toBeInTheDocument()
    expect(within(modal).getByText('Escreva de 5 a 500 caracteres.')).toBeInTheDocument()
    expect(criar.quantas()).toBe(0)
  })

  it.each([
    [409, undefined, 'Dia do ponto'],
    [422, 'diaNaoPassou', 'Dia do ponto'],
    [422, 'instanteNoFuturo', 'itens'],
    [422, 'instanteForaDoDia', 'itens'],
    [422, 'sequenciaInvalida', 'itens'],
  ] as const)('%i %s mostra o detail junto do campo certo (%s)', async (status, nome, campo) => {
    capturar('criarPedidoDeAjuste', problemaDoContrato('criarPedidoDeAjuste', status, nome))
    const tela = await abrirDoDia5()
    await preencherPeriodo(tela)
    await tela.user.click(screen.getByRole('button', { name: 'Enviar pedido' }))
    const detail = exemploDoContrato<{ detail: string }>('criarPedidoDeAjuste', status, nome).detail
    const mensagem = await within(tela.modal).findByText(detail)
    if (campo === 'itens') {
      expect(mensagem.closest('fieldset')).toHaveTextContent('Correção pedida')
    } else {
      expect(mensagem.closest('fieldset')).toBeNull()
      expect(within(tela.modal).getByLabelText('Dia do ponto')).toHaveAttribute('data-invalid')
    }
  })
})

describe('Enviar atestado (sem print)', { timeout: 15000 }, () => {
  const pdf = () => new File(['%PDF'], 'atestado.pdf', { type: 'application/pdf' })

  it('dias inteiros envia dataInicio e dataFim; algumas horas envia o par de horas', async () => {
    vi.mocked(enviarAtestado).mockResolvedValue(
      exemploDoContrato('enviarAtestado', 201) as Esquema<'Atestado'>,
    )
    const { user } = await abrirMeuPonto()
    await user.click(screen.getByRole('button', { name: 'Enviar atestado' }))
    let modal = await screen.findByRole('dialog', { name: 'Enviar atestado' })
    expect(
      within(modal).getByText(
        'O RH e o seu gestor veem o arquivo. O sistema não guarda diagnóstico.',
      ),
    ).toBeInTheDocument()
    preencherData('De', '2026-10-07')
    preencherData('Até', '2026-10-08')
    const arquivo = pdf()
    await user.upload(within(modal).getByLabelText('Escolher arquivo'), arquivo)
    await user.click(
      within(screen.getByRole('dialog', { name: 'Enviar atestado' })).getByText('Enviar atestado', {
        selector: 'button',
      }),
    )
    await waitFor(() =>
      expect(enviarAtestado).toHaveBeenCalledWith({
        dataInicio: '2026-10-07',
        dataFim: '2026-10-08',
        arquivo,
      }),
    )
    await waitFor(() =>
      expect(screen.queryByRole('dialog', { name: 'Enviar atestado' })).toBeNull(),
    )

    await user.click(screen.getByRole('button', { name: 'Enviar atestado' }))
    modal = await screen.findByRole('dialog', { name: 'Enviar atestado' })
    await user.click(within(modal).getByText('Algumas horas'))
    preencherData('Data', '2026-09-25')
    await user.type(within(modal).getByLabelText('Das'), '13:00')
    await user.type(within(modal).getByLabelText('Até a hora'), '17:48')
    await user.upload(within(modal).getByLabelText('Escolher arquivo'), arquivo)
    await user.click(within(modal).getByText('Enviar atestado', { selector: 'button' }))
    await waitFor(() =>
      expect(enviarAtestado).toHaveBeenLastCalledWith({
        dataInicio: '2026-09-25',
        dataFim: '2026-09-25',
        horaInicio: '13:00',
        horaFim: '17:48',
        arquivo,
      }),
    )
  })

  it('fim antes do inicio e sem arquivo nao envia', async () => {
    const { user } = await abrirMeuPonto()
    await user.click(screen.getByRole('button', { name: 'Enviar atestado' }))
    const modal = await screen.findByRole('dialog', { name: 'Enviar atestado' })
    preencherData('De', '2026-10-08')
    preencherData('Até', '2026-10-07')
    await user.click(within(modal).getByText('Enviar atestado', { selector: 'button' }))
    expect(
      await within(modal).findByText('O fim não pode ser antes do início.'),
    ).toBeInTheDocument()
    expect(within(modal).getByText('Escolha o arquivo do atestado.')).toBeInTheDocument()
    expect(enviarAtestado).not.toHaveBeenCalled()
  })
})

describe('Menu "Meu espaço" (decisão do dono)', { timeout: 15000 }, () => {
  it('RH: "Meu ponto" em "Meu espaço" leva a /eu/meu-ponto com o cartao e o menu do RH', async () => {
    const { user, roteador } = renderizarTela('/rh/painel', { perfil: 'RH' })
    const menu = await screen.findByRole('navigation', { name: 'Menu principal' })
    const grupo = within(menu).getByText('Meu espaço').closest('div')!
    await user.click(within(grupo).getByRole('link', { name: 'Meu ponto' }))
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/eu/meu-ponto'))
    expect(await screen.findByRole('region', { name: 'Seu ponto hoje' })).toBeInTheDocument()
    expect(within(menu).getByRole('link', { name: /^Colaboradores/ })).toBeInTheDocument()
  })

  it('Gestor: "Meu ponto" fica em "Seu dia a dia" e leva ao mesmo endereco', async () => {
    renderizarTela('/eu/inicio', { perfil: 'Gestor' })
    const menu = await screen.findByRole('navigation', { name: 'Menu principal' })
    const grupo = within(menu).getByText('Seu dia a dia').closest('div')!
    expect(within(grupo).getByRole('link', { name: 'Meu ponto' })).toHaveAttribute(
      'href',
      '/eu/meu-ponto',
    )
  })
})
