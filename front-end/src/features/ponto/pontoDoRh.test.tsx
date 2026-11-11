import { screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { escolherOpcao, preencherData } from '../../test/formulario'
import { renderizarTela } from '../../test/telas'

const problemaDoContrato = (op: string, status: number, nome?: string) =>
  problema(status, exemploDoContrato<Record<string, unknown>>(op, status, nome))

const COM_BRUNO = '/rh/ponto-e-feriados?funcionarioId=12&ano=2026&mes=10'

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
})

afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
})

async function abrir(rota = COM_BRUNO) {
  const tela = renderizarTela(rota)
  await screen.findByRole('heading', { level: 1, name: 'Ponto e feriados', hidden: true })
  return tela
}

const linhaDoEspelho = async (dia: string) => {
  const tabela = await screen.findByRole('table', { name: 'Espelho de ponto' })
  return (await within(tabela).findByText(dia)).closest('tr')!
}

describe('Ponto e feriados (tela 12)', { timeout: 15000 }, () => {
  it('sem colaborador: texto de escolher e Baixar PDF desligado', async () => {
    const espelho = capturar('obterEspelhoDePontoDeFuncionario')
    await abrir('/rh/ponto-e-feriados')
    expect(screen.getByText('Escolha um colaborador para ver o espelho.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Baixar PDF' })).toBeDisabled()
    expect(espelho.quantas()).toBe(0)
  })

  it('com o colaborador da URL: quatro numeros, batidas por ordem, corrigida pelo RH, Revisar pedido e Editar', async () => {
    const espelho = capturar('obterEspelhoDePontoDeFuncionario')
    await abrir()
    const numeros = await screen.findByRole('region', { name: 'Números do espelho' })
    expect(await within(numeros).findByText('26h40')).toBeInTheDocument()
    expect(within(numeros).getByText('+6h20')).toBeInTheDocument()
    expect(espelho.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '12' })
    expect(espelho.ultima().consulta.get('ano')).toBe('2026')
    expect(espelho.ultima().consulta.get('mes')).toBe('10')
    espelho.conforme()
    const dia6 = await linhaDoEspelho('06/10')
    expect(within(dia6).getByText('Saída corrigida pelo RH em 07/10')).toBeInTheDocument()
    expect(within(dia6).getByText('17:58')).toBeInTheDocument()
    expect(within(dia6).getByRole('button', { name: 'Editar o dia 06/10' })).toBeInTheDocument()
    const dia2 = await linhaDoEspelho('02/10')
    expect(within(dia2).getByText('Sem registro')).toBeInTheDocument()
    expect(
      within(dia2).getByRole('button', { name: 'Revisar pedido do dia 02/10' }),
    ).toBeInTheDocument()
    expect(within(dia2).queryByRole('button', { name: /Editar/ })).toBeNull()
    const dia5 = await linhaDoEspelho('05/10')
    expect(within(dia5).getByText('Atraso de 14 min')).toBeInTheDocument()
  })

  it('trocar o periodo refaz a chamada com ano e mes; Baixar PDF e Exportar usam o periodo', async () => {
    const espelho = capturar('obterEspelhoDePontoDeFuncionario')
    const pdf = capturar('baixarEspelhoDePontoPdf')
    const folha = capturar('exportarPontoParaFolha', {
      status: 200,
      corpo: 'a;b',
      cabecalhos: { 'content-type': 'text/csv' },
    })
    const baixados: string[] = []
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      baixados.push(this.download || this.href)
    })
    Object.defineProperty(URL, 'createObjectURL', {
      value: vi.fn(() => 'blob:x'),
      configurable: true,
    })
    Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true })
    const { user } = await abrir()
    await screen.findByRole('table', { name: 'Espelho de ponto' })
    await escolherOpcao(user, 'Período', 'Setembro de 2026')
    await waitFor(() => expect(espelho.ultima().consulta.get('mes')).toBe('9'))
    await user.click(screen.getByRole('button', { name: 'Baixar PDF' }))
    await waitFor(() => expect(pdf.quantas()).toBe(1))
    expect(pdf.ultima().consulta.get('mes')).toBe('9')
    expect(pdf.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '12' })
    pdf.conforme()
    await user.click(screen.getByRole('button', { name: 'Exportar para a folha' }))
    await waitFor(() => expect(baixados).toContain('ponto-2026-09.csv'))
    expect(folha.ultima().consulta.get('ano')).toBe('2026')
    expect(folha.ultima().consulta.get('mes')).toBe('9')
    folha.conforme()
  })

  it('escolher o colaborador pela busca poe o id na URL', async () => {
    const lista = capturar('listarFuncionarios')
    const { user, roteador } = await abrir('/rh/ponto-e-feriados')
    await user.type(screen.getByRole('combobox', { name: 'Colaborador' }), 'Bru')
    await user.click(await screen.findByRole('option', { name: /Bruno Lima/ }))
    await waitFor(() => expect(roteador.state.location.search).toContain('funcionarioId=12'))
    expect(lista.ultima().consulta.get('busca')).toBe('Bru')
  })

  it('o botao Pedidos de ajuste mostra resumo.comRh e abre o modal 13; Revisar pedido abre ja no pedido', async () => {
    const fila = capturar('listarPedidosDeAjusteParaRh')
    const detalhe = capturar('obterPedidoDeAjusteParaRh')
    const { user, roteador } = await abrir()
    const botao = await screen.findByRole('button', { name: /^Pedidos de ajuste/ })
    expect(await within(botao).findByText('4')).toBeInTheDocument()
    const linha = await linhaDoEspelho('02/10')
    await user.click(within(linha).getByRole('button', { name: 'Revisar pedido do dia 02/10' }))
    expect(roteador.state.location.search).toContain('pedido=301')
    await screen.findByRole('dialog', { name: 'Pedidos de ajuste de ponto' })
    await waitFor(() => expect(detalhe.ultima().parametrosDoCaminho).toEqual({ pedidoId: '301' }))
    detalhe.conforme()
    fila.conforme()
  })

  it('Editar abre o modal com as batidas e envia a lista completa com os ids (remover uma apaga)', async () => {
    const corrigir = capturar('corrigirMarcacoesDoDia')
    const { user } = await abrir()
    const linha = await linhaDoEspelho('06/10')
    await user.click(within(linha).getByRole('button', { name: 'Editar o dia 06/10' }))
    const modal = await screen.findByRole('dialog', { name: 'Corrigir ponto de 06/10' })
    expect(within(modal).getByLabelText('Hora da batida 4')).toHaveValue('17:58')
    await user.click(within(modal).getByRole('button', { name: 'Remover a batida 4' }))
    await user.click(within(modal).getByRole('button', { name: 'Incluir batida' }))
    await escolherOpcao(user, 'Tipo da batida 4', 'Saída')
    await user.type(within(modal).getByLabelText('Hora da batida 4'), '18:10')
    await user.click(screen.getByRole('button', { name: 'Salvar correção' }))
    await waitFor(() => expect(corrigir.quantas()).toBe(1))
    expect(corrigir.ultima().parametrosDoCaminho).toEqual({
      funcionarioId: '12',
      dia: '2026-10-06',
    })
    expect(corrigir.ultima().corpo).toEqual({
      marcacoes: [
        { id: 8812, tipo: 'ENTRADA', instanteUtc: '2026-10-06T10:58:00Z' },
        { id: 8813, tipo: 'SAIDA', instanteUtc: '2026-10-06T15:00:00Z' },
        { id: 8814, tipo: 'ENTRADA', instanteUtc: '2026-10-06T16:00:00Z' },
        { tipo: 'SAIDA', instanteUtc: '2026-10-06T21:10:00Z' },
      ],
    })
    corrigir.conforme()
  })

  it.each([
    [409, 'marcacaoEmUso'],
    [409, 'pedidoEmAnalise'],
    [422, 'sequenciaInvalida'],
  ] as const)('corrigir: %i %s mostra o detail', async (status, nome) => {
    capturar('corrigirMarcacoesDoDia', problemaDoContrato('corrigirMarcacoesDoDia', status, nome))
    const { user } = await abrir()
    const linha = await linhaDoEspelho('06/10')
    await user.click(within(linha).getByRole('button', { name: 'Editar o dia 06/10' }))
    await screen.findByRole('dialog', { name: 'Corrigir ponto de 06/10' })
    await user.click(screen.getByRole('button', { name: 'Salvar correção' }))
    const detail = exemploDoContrato<{ detail: string }>(
      'corrigirMarcacoesDoDia',
      status,
      nome,
    ).detail
    expect(await screen.findByText(detail)).toBeInTheDocument()
  })

  it('feriados: nacional sem Editar, municipal com Editar; criar envia { nome, data, repeteTodoAno }', async () => {
    const lista = capturar('listarFeriados')
    const criar = capturar('criarFeriado')
    const { user } = await abrir()
    const feriados = await screen.findByRole('list', { name: 'Feriados deste mês' })
    const nacional = within(feriados).getByText('Nossa Senhora Aparecida').closest('li')!
    expect(within(nacional).getByText('Nacional, importado automaticamente')).toBeInTheDocument()
    expect(within(nacional).queryByRole('button')).toBeNull()
    expect(
      within(feriados).getByRole('button', { name: 'Editar o feriado Dia de São Judas Tadeu' }),
    ).toBeInTheDocument()
    expect(lista.ultima().consulta.get('ano')).toBe('2026')
    expect(lista.ultima().consulta.get('mes')).toBe('10')
    lista.conforme()
    expect(screen.getByRole('checkbox', { name: /Repetir todos os anos/ })).toHaveAttribute(
      'aria-checked',
      'false',
    )
    await user.type(screen.getByLabelText('Nome do feriado'), 'Aniversário da cidade')
    preencherData('Data', '2026-10-25')
    await user.click(screen.getByRole('button', { name: 'Salvar feriado' }))
    await waitFor(() => expect(criar.quantas()).toBe(1))
    expect(criar.ultima().corpo).toEqual({
      nome: 'Aniversário da cidade',
      data: '2026-10-25',
      repeteTodoAno: false,
    })
    criar.conforme()
  })

  it('editar feriado envia ativo e desativar manda ativo: false', async () => {
    const editar = capturar('editarFeriado')
    const { user } = await abrir()
    await user.click(
      await screen.findByRole('button', { name: 'Editar o feriado Dia de São Judas Tadeu' }),
    )
    expect(await screen.findByRole('heading', { name: 'Editar feriado' })).toBeInTheDocument()
    await user.click(screen.getByText('Desativar este feriado'))
    await user.click(screen.getByRole('button', { name: 'Salvar feriado' }))
    await waitFor(() => expect(editar.quantas()).toBe(1))
    expect(editar.ultima().parametrosDoCaminho).toEqual({ feriadoId: '41' })
    expect(editar.ultima().corpo).toEqual({
      nome: 'Dia de São Judas Tadeu',
      data: '2026-10-28',
      repeteTodoAno: true,
      ativo: false,
    })
    editar.conforme()
  })

  it.each([409, 422])('criar feriado %i mostra o detail', async (status) => {
    capturar('criarFeriado', problemaDoContrato('criarFeriado', status))
    const { user } = await abrir()
    await user.type(await screen.findByLabelText('Nome do feriado'), 'Aniversário da cidade')
    preencherData('Data', '2026-10-25')
    await user.click(screen.getByRole('button', { name: 'Salvar feriado' }))
    const detail = exemploDoContrato<{ detail: string }>('criarFeriado', status).detail
    expect(await screen.findByText(detail)).toBeInTheDocument()
    if (status === 422) {
      expect(screen.getByRole('link', { name: 'Ir para Cadastros, Empresa' })).toHaveAttribute(
        'href',
        '/rh/cadastros/empresa',
      )
    }
  })

  it('Gestor e Funcionario veem o 403', async () => {
    for (const perfil of ['Gestor', 'Funcionario'] as const) {
      const t = renderizarTela('/rh/ponto-e-feriados', { perfil })
      expect(
        await screen.findByText('Você não tem permissão para ver esta página.'),
      ).toBeInTheDocument()
      t.unmount()
    }
  })
})

describe('Pedidos de ajuste do RH (tela 13)', { timeout: 15000 }, () => {
  async function abrirModal(pedido?: number) {
    const tela = await abrir(
      pedido ? `/rh/ponto-e-feriados?pedido=${pedido}` : '/rh/ponto-e-feriados?pedidos=abertos',
    )
    const modal = await screen.findByRole('dialog', { name: 'Pedidos de ajuste de ponto' })
    return { ...tela, modal }
  }

  it('a lista abre com status=PENDENTE, CRIADO_EM ASC e o rodape usa resumo.comRh', async () => {
    const fila = capturar('listarPedidosDeAjusteParaRh')
    const { modal } = await abrirModal()
    const lista = await within(modal).findByRole('list', { name: 'Pedidos em análise' })
    expect(within(lista).getAllByRole('listitem')).toHaveLength(6)
    const c = fila.ultima().consulta
    expect(c.get('status')).toBe('PENDENTE')
    expect(c.get('ordenarPor')).toBe('CRIADO_EM')
    expect(c.get('direcao')).toBe('ASC')
    fila.conforme()
    expect(
      screen.getByText('4 pedidos esperando o RH. O gestor do setor também pode analisar.'),
    ).toBeInTheDocument()
    expect(within(lista).getAllByText('Com o gestor')).toHaveLength(2)
    expect(within(lista).getByText('Aprovado pelo gestor')).toBeInTheDocument()
  })

  it('pedido com o gestor: botoes desligados com o motivo', async () => {
    capturar('obterPedidoDeAjusteParaRh', {
      corpo: exemploDoContrato('obterPedidoDeAjusteParaRh', 200, 'comOGestor'),
    })
    const { modal } = await abrirModal(303)
    const detalhe = await within(modal).findByRole('region', { name: 'Pedido de Júlia Nunes' })
    expect(
      within(detalhe).getByRole('button', { name: 'Aprovar e corrigir o ponto' }),
    ).toBeDisabled()
    expect(within(detalhe).getByRole('button', { name: 'Recusar' })).toBeDisabled()
    expect(within(detalhe).getByText('Aguardando a análise do gestor.')).toBeInTheDocument()
    expect(within(detalhe).getByText('Carlos Ribeiro ainda não analisou.')).toBeInTheDocument()
  })

  it('pedido 301: aprovar envia a observacao, avisa, atualiza a lista e passa ao proximo', async () => {
    const aprovar = capturar('aprovarPedidoDeAjusteComoRh')
    const fila = capturar('listarPedidosDeAjusteParaRh')
    const { modal, user } = await abrirModal(301)
    const detalhe = await within(modal).findByRole('region', { name: 'Pedido de Bruno Lima' })
    expect(within(detalhe).getByText('Sexta, 02/10')).toBeInTheDocument()
    expect(within(detalhe).getByText('Incluir a saída às 17:52')).toBeInTheDocument()
    expect(within(detalhe).getByText('13:02 - sem saída')).toBeInTheDocument()
    await user.type(
      within(detalhe).getByLabelText('Observação para Bruno (opcional)'),
      'Conferido.',
    )
    const antes = fila.quantas()
    await user.click(within(detalhe).getByRole('button', { name: 'Aprovar e corrigir o ponto' }))
    expect(await screen.findByText('Pedido aprovado e ponto corrigido.')).toBeInTheDocument()
    expect(aprovar.ultima().parametrosDoCaminho).toEqual({ pedidoId: '301' })
    expect(aprovar.ultima().corpo).toEqual({ observacao: 'Conferido.' })
    aprovar.conforme()
    await waitFor(() => expect(fila.quantas()).toBeGreaterThan(antes))
  })

  it('recusar sem observacao envia null', async () => {
    const recusar = capturar('recusarPedidoDeAjusteComoRh')
    const { modal, user } = await abrirModal(301)
    const detalhe = await within(modal).findByRole('region', { name: 'Pedido de Bruno Lima' })
    await user.click(within(detalhe).getByRole('button', { name: 'Recusar' }))
    expect(await screen.findByText('Pedido recusado.')).toBeInTheDocument()
    expect(recusar.ultima().corpo).toEqual({ observacao: null })
    recusar.conforme()
  })

  it.each([
    [409, 'jaDecidido'],
    [409, 'etapaIncorreta'],
    [422, 'rhForaDeGestores'],
    [422, 'pedidoProprio'],
  ] as const)('aprovar %i %s mostra o detail', async (status, nome) => {
    capturar(
      'aprovarPedidoDeAjusteComoRh',
      problemaDoContrato('aprovarPedidoDeAjusteComoRh', status, nome),
    )
    const { modal, user } = await abrirModal(301)
    const detalhe = await within(modal).findByRole('region', { name: 'Pedido de Bruno Lima' })
    await user.click(within(detalhe).getByRole('button', { name: 'Aprovar e corrigir o ponto' }))
    const detail = exemploDoContrato<{ detail: string }>(
      'aprovarPedidoDeAjusteComoRh',
      status,
      nome,
    ).detail
    expect(await within(modal).findByText(detail)).toBeInTheDocument()
  })

  it('fechar tira o pedido da URL', async () => {
    const { roteador, user } = await abrirModal(301)
    await user.click(screen.getByText('Fechar', { selector: 'button' }))
    await waitFor(() => expect(roteador.state.location.search).not.toContain('pedido'))
  })
})

describe('Aba Ponto da ficha (tela 04)', { timeout: 15000 }, () => {
  it('tres numeros, sem folgas, sem Lancar no banco de horas e Abrir espelho completo com o mes', async () => {
    const espelho = capturar('obterEspelhoDePontoDeFuncionario')
    renderizarTela('/rh/colaboradores/12?aba=ponto')
    const numeros = await screen.findByRole('region', { name: 'Números do ponto' })
    expect(await within(numeros).findByText('Horas em outubro')).toBeInTheDocument()
    expect(within(numeros).getByText('Pedidos de ajuste em análise')).toBeInTheDocument()
    const tabela = screen.getByRole('table', { name: 'Ponto do mês' })
    expect(within(tabela).queryByText('Folga')).toBeNull()
    expect(screen.queryByRole('button', { name: /Lançar no banco de horas/ })).toBeNull()
    expect(screen.getByRole('link', { name: 'Abrir espelho completo' })).toHaveAttribute(
      'href',
      '/rh/ponto-e-feriados?funcionarioId=12&ano=2026&mes=10',
    )
    expect(espelho.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '12' })
    espelho.conforme()
  })
})

describe('Atestados do RH (sem print)', { timeout: 15000 }, () => {
  it('abre em /rh/ponto-e-feriados/atestados com situacao=ENVIADO; o botao mostra quantos aguardam', async () => {
    const lista = capturar('listarAtestados', {
      corpo: exemploDoContrato('listarAtestados', 200, 'aValidar'),
    })
    const { user, roteador } = await abrir('/rh/ponto-e-feriados')
    await user.click(await screen.findByRole('link', { name: /^Atestados/ }))
    await waitFor(() =>
      expect(roteador.state.location.pathname).toBe('/rh/ponto-e-feriados/atestados'),
    )
    const painel = await screen.findByRole('dialog', { name: 'Atestados' })
    expect(await within(painel).findByText('Carla Mendes')).toBeInTheDocument()
    expect(within(painel).getByText('01/10 a 02/10')).toBeInTheDocument()
    expect(lista.ultima().consulta.get('situacao')).toBe('ENVIADO')
    lista.conforme()
    expect(within(painel).getByText('O acesso a este arquivo fica registrado.')).toBeInTheDocument()
  })

  it('Ver arquivo pede o link temporario; Validar confirma; Recusar exige motivo', async () => {
    capturar('listarAtestados', { corpo: exemploDoContrato('listarAtestados', 200, 'aValidar') })
    const baixar = capturar('baixarAtestado')
    const validar = capturar('validarAtestado')
    const recusar = capturar('recusarAtestado')
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    const { user } = await abrir('/rh/ponto-e-feriados/atestados')
    const painel = await screen.findByRole('dialog', { name: 'Atestados' })
    await user.click(
      await within(painel).findByRole('button', {
        name: 'Ver arquivo do atestado de Carla Mendes',
      }),
    )
    await waitFor(() => expect(baixar.ultima().parametrosDoCaminho).toEqual({ atestadoId: '43' }))
    baixar.conforme()
    await user.click(
      within(painel).getByRole('button', { name: 'Validar o atestado de Carla Mendes' }),
    )
    const confirmar = await screen
      .findByRole('alertdialog', { name: 'Validar este atestado?' })
      .catch(() => screen.findByRole('dialog', { name: 'Validar este atestado?' }))
    await user.click(within(confirmar).getByRole('button', { name: 'Validar' }))
    await waitFor(() => expect(validar.ultima().parametrosDoCaminho).toEqual({ atestadoId: '43' }))
    validar.conforme()
    expect(await screen.findByText('Atestado validado.')).toBeInTheDocument()
    await user.click(
      within(painel).getByRole('button', { name: 'Recusar o atestado de Carla Mendes' }),
    )
    const dialogo = await screen.findByRole('dialog', {
      name: 'Recusar o atestado de Carla Mendes?',
    })
    await user.click(within(dialogo).getByRole('button', { name: 'Recusar atestado' }))
    expect(await within(dialogo).findByText('Escreva o motivo da recusa.')).toBeInTheDocument()
    expect(recusar.quantas()).toBe(0)
    await user.type(within(dialogo).getByLabelText('Motivo'), 'Sem o horário da consulta.')
    await user.click(within(dialogo).getByRole('button', { name: 'Recusar atestado' }))
    await waitFor(() => expect(recusar.quantas()).toBe(1))
    expect(recusar.ultima().corpo).toEqual({ motivo: 'Sem o horário da consulta.' })
    recusar.conforme()
  })

  it('409 ATESTADO_JA_DECIDIDO mostra o detail', async () => {
    capturar('listarAtestados', { corpo: exemploDoContrato('listarAtestados', 200, 'aValidar') })
    capturar('validarAtestado', problemaDoContrato('validarAtestado', 409))
    const { user } = await abrir('/rh/ponto-e-feriados/atestados')
    const painel = await screen.findByRole('dialog', { name: 'Atestados' })
    await user.click(
      await within(painel).findByRole('button', { name: 'Validar o atestado de Carla Mendes' }),
    )
    const confirmar = await screen
      .findByRole('alertdialog', { name: 'Validar este atestado?' })
      .catch(() => screen.findByRole('dialog', { name: 'Validar este atestado?' }))
    await user.click(within(confirmar).getByRole('button', { name: 'Validar' }))
    expect(
      await screen.findByText('Este atestado já foi respondido. Atualize a lista.'),
    ).toBeInTheDocument()
  })
})
