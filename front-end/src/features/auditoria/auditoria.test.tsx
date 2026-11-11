import { screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { formatarDataHoraComSegundos, somarDias } from '../../shared/formato/datas'
import { exemplos } from '../../shared/mocks/gerado/handlers'
import { capturar, problema } from '../../test/capturar'
import { escolherOpcao, preencherData } from '../../test/formulario'
import { renderizarTela } from '../../test/telas'
import { intervaloDoPeriodo, textoDoPeriodo } from './periodoDaAuditoria'
import { OPCOES_DE_ACAO, OPCOES_DE_AREA, rotuloDoQuem, tomDaAcao } from './rotulosDeAuditoria'

declare global {
  interface Window {
    __x?: number
  }
}

type Lista = { itens: Record<string, unknown>[] } & Record<string, unknown>
const LISTA = exemplos.listarEventosDeAuditoria.corpo as Lista
const DETALHE = exemplos.obterEventoDeAuditoria.corpo as Record<string, unknown>

function hoje() {
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
}

afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
})

describe('auditoria: funcoes', () => {
  it('formatarDataHoraComSegundos e somarDias nao dependem do fuso da maquina', () => {
    expect(formatarDataHoraComSegundos('2026-10-07T12:38:12Z')).toBe('07/10/2026 09:38:12')
    expect(somarDias('2026-10-07', -29)).toBe('2026-09-08')
    expect(somarDias('2026-12-31', 1)).toBe('2027-01-01')
  })

  it('periodo e textos', () => {
    expect(intervaloDoPeriodo('7', undefined, undefined, '2026-10-07')).toEqual({})
    expect(intervaloDoPeriodo('hoje', undefined, undefined, '2026-10-07')).toEqual({
      de: '2026-10-07',
      ate: '2026-10-07',
    })
    expect(intervaloDoPeriodo('30', undefined, undefined, '2026-10-07')).toEqual({
      de: '2026-09-08',
      ate: '2026-10-07',
    })
    expect(intervaloDoPeriodo('personalizado', '2026-10-01', undefined, '2026-10-07')).toBeNull()
    expect(intervaloDoPeriodo('personalizado', '2026-10-05', '2026-10-01', '2026-10-07')).toBeNull()
    expect(textoDoPeriodo('personalizado', '2026-10-01', '2026-10-05')).toBe(
      'de 01/10/2026 a 05/10/2026',
    )
    expect(textoDoPeriodo('7', undefined, undefined)).toBe('dos últimos 7 dias')
  })

  it('rotulos: Colaborador, Automatico, Desligado; as tres acoes vermelhas; 15 areas e 14 acoes', () => {
    expect(
      rotuloDoQuem({
        funcionarioId: 1,
        nome: 'A',
        tipo: 'PESSOA',
        perfil: 'Funcionario',
        desligado: false,
      }),
    ).toBe('Colaborador')
    expect(
      rotuloDoQuem({
        funcionarioId: null,
        nome: 'Sistema',
        tipo: 'SISTEMA',
        perfil: null,
        desligado: true,
      } as never),
    ).toBe('Automático')
    expect(
      rotuloDoQuem({ funcionarioId: 1, nome: 'A', tipo: 'PESSOA', perfil: 'RH', desligado: true }),
    ).toBe('Desligado')
    expect(['EXCLUIU', 'ACESSO_NEGADO', 'INATIVOU'].map((a) => tomDaAcao(a as never))).toEqual([
      'erro',
      'erro',
      'erro',
    ])
    expect(tomDaAcao('ALTEROU')).toBe('neutro')
    expect(OPCOES_DE_AREA).toHaveLength(15)
    expect(OPCOES_DE_ACAO).toHaveLength(14)
  })
})

describe('Log de auditoria (tela 31)', () => {
  it('subtitulo com a frase nova e filtros padrao; a requisicao e conforme', async () => {
    const lista = capturar('listarEventosDeAuditoria')
    renderizarTela('/rh/auditoria')
    expect(
      await screen.findByText(
        'Tudo o que foi criado, alterado ou excluído no portal: quem fez, quando e o que mudou. Nenhuma tela do sistema edita ou apaga estes registros.',
      ),
    ).toBeInTheDocument()
    expect(screen.queryByText(/Ninguém consegue editar nem apagar/)).toBeNull()
    expect(screen.getByRole('combobox', { name: 'Período' })).toHaveTextContent('Últimos 7 dias')
    expect(screen.getByRole('combobox', { name: 'Área' })).toHaveTextContent('Todas as áreas')
    expect(screen.getByRole('combobox', { name: 'Ação' })).toHaveTextContent('Todas as ações')
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(0))
    const c = lista.ultima().consulta
    expect(c.get('tamanhoPagina')).toBe('20')
    for (const chave of ['de', 'ate', 'area', 'acao', 'apenasAlteracoes', 'periodo'])
      expect(c.has(chave)).toBe(false)
    lista.conforme()
  })

  it('Hoje, Ultimos 30 dias e Periodo personalizado mandam as datas certas; periodo nunca vai a API', async () => {
    hoje()
    const lista = capturar('listarEventosDeAuditoria')
    const { user } = renderizarTela('/rh/auditoria')
    await screen.findByRole('table', { name: 'Log de auditoria' })
    await escolherOpcao(user, 'Período', 'Hoje')
    await waitFor(() => expect(lista.ultima().consulta.get('de')).toBe('2026-10-07'))
    expect(lista.ultima().consulta.get('ate')).toBe('2026-10-07')
    await escolherOpcao(user, 'Período', 'Últimos 30 dias')
    await waitFor(() => expect(lista.ultima().consulta.get('de')).toBe('2026-09-08'))
    await escolherOpcao(user, 'Período', 'Período personalizado')
    expect(await screen.findByText('Escolha o período.')).toBeInTheDocument()
    const antes = lista.quantas()
    preencherData('De', '2026-10-01')
    preencherData('Até', '2026-10-05')
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(antes))
    expect(lista.ultima().consulta.get('de')).toBe('2026-10-01')
    expect(lista.ultima().consulta.get('ate')).toBe('2026-10-05')
    expect(lista.ultima().consulta.has('periodo')).toBe(false)
    lista.conforme()
  })

  it('area e acao mandam o valor do contrato e voltam a pagina 1', async () => {
    const lista = capturar('listarEventosDeAuditoria')
    const { user } = renderizarTela('/rh/auditoria?pagina=3')
    await screen.findByRole('table', { name: 'Log de auditoria' })
    await escolherOpcao(user, 'Área', 'Colaboradores')
    await waitFor(() => expect(lista.ultima().consulta.get('area')).toBe('COLABORADORES'))
    expect(lista.ultima().consulta.get('pagina')).toBe('1')
    await escolherOpcao(user, 'Ação', 'Alterou')
    await waitFor(() => expect(lista.ultima().consulta.get('acao')).toBe('ALTEROU'))
    await escolherOpcao(user, 'Área', 'Todas as áreas')
    await waitFor(() => expect(lista.ultima().consulta.has('area')).toBe(false))
  })

  it('colunas, Quem com o perfil, Desligado e etiqueta vermelha de Acesso negado', async () => {
    hoje()
    renderizarTela('/rh/auditoria')
    const tabela = await screen.findByRole('table', { name: 'Log de auditoria' })
    expect(
      within(tabela)
        .getAllByRole('columnheader')
        .map((c) => c.textContent),
    ).toEqual(['Quando', 'Quem', 'Área', 'Ação', 'O que aconteceu', 'Detalhes'])
    expect(await within(tabela).findByText('Hoje, 09:38')).toBeInTheDocument()
    expect(within(tabela).getByText('Desligado')).toBeInTheDocument()
    expect(within(tabela).getByText('Acesso negado')).toHaveAttribute('data-tom', 'erro')
    expect(within(tabela).getByText('Alterou')).toHaveAttribute('data-tom', 'neutro')
  })

  it('Detalhes chama o detalhe so ao abrir, mostra os campos, vira Fechar; uma linha aberta por vez', async () => {
    const detalhe = capturar('obterEventoDeAuditoria')
    const { user } = renderizarTela('/rh/auditoria')
    const tabela = await screen.findByRole('table', { name: 'Log de auditoria' })
    expect(detalhe.quantas()).toBe(0)
    const linha = (await within(tabela).findByText('Cargo de Bruno Lima')).closest('tr')!
    await user.click(within(linha).getByRole('button', { name: 'Detalhes' }))
    expect(await screen.findByText('07/10/2026 09:38:12')).toBeInTheDocument()
    expect(screen.getByText('Colaborador Bruno Lima, matrícula 00482')).toBeInTheDocument()
    expect(screen.getByText('Portal no navegador, IP 10.0.4.23')).toBeInTheDocument()
    const mudou = screen.getByRole('table', { name: 'O que mudou' })
    expect(within(mudou).getByText('Analista pleno')).toBeInTheDocument()
    expect(detalhe.ultima().parametrosDoCaminho).toEqual({ eventoId: '48213' })
    detalhe.conforme()
    expect(within(linha).getByRole('button', { name: 'Fechar' })).toBeInTheDocument()
    const outra = within(tabela).getByText('Setor Compras').closest('tr')!
    await user.click(within(outra).getByRole('button', { name: 'Detalhes' }))
    await waitFor(() =>
      expect(within(linha).getByRole('button', { name: 'Detalhes' })).toBeInTheDocument(),
    )
  })

  it('valorOculto, mudancas vazia e origem SISTEMA', async () => {
    capturar('obterEventoDeAuditoria', {
      corpo: {
        ...DETALHE,
        origem: { canal: 'SISTEMA', ip: null },
        mudancas: [{ campo: 'CPF', antes: null, depois: null, valorOculto: true }],
      },
    })
    const { user, unmount } = renderizarTela('/rh/auditoria')
    const linha = (await screen.findByText('Cargo de Bruno Lima')).closest('tr')!
    await user.click(within(linha).getByRole('button', { name: 'Detalhes' }))
    expect(await screen.findAllByText('valor não registrado')).toHaveLength(2)
    expect(screen.getByText('Automático', { selector: 'dd' })).toBeInTheDocument()
    unmount()
    capturar('obterEventoDeAuditoria', { corpo: { ...DETALHE, mudancas: [] } })
    const outra = renderizarTela('/rh/auditoria')
    const l2 = (await screen.findByText('Cargo de Bruno Lima')).closest('tr')!
    await outra.user.click(within(l2).getByRole('button', { name: 'Detalhes' }))
    expect(await screen.findByText('Este evento não altera dados.')).toBeInTheDocument()
  })

  it('a descricao do evento e texto, nunca HTML (Review Focus 4)', async () => {
    capturar('listarEventosDeAuditoria', {
      corpo: {
        ...LISTA,
        itens: [{ ...LISTA.itens[1], descricao: '<img src=x onerror="window.__x=1">' }],
      },
    })
    renderizarTela('/rh/auditoria')
    expect(await screen.findByText('<img src=x onerror="window.__x=1">')).toBeInTheDocument()
    expect(window.__x).toBeUndefined()
  })

  it('rodape "Mostrando 4 de 1.284 registros dos últimos 7 dias" e Proxima muda a pagina', async () => {
    const lista = capturar('listarEventosDeAuditoria')
    const { user } = renderizarTela('/rh/auditoria')
    expect(
      await screen.findByText('Mostrando 4 de 1.284 registros dos últimos 7 dias'),
    ).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Próxima' }))
    await waitFor(() => expect(lista.ultima().consulta.get('pagina')).toBe('2'))
    for (const c of screen.getAllByRole('columnheader')) expect(c).not.toHaveAttribute('aria-sort')
  })

  it('Exportar CSV baixa com os mesmos filtros e o nome do cabecalho; 500 avisa e nada baixa', async () => {
    const baixados: string[] = []
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      baixados.push(this.download)
    })
    Object.defineProperty(URL, 'createObjectURL', {
      value: vi.fn(() => 'blob:x'),
      configurable: true,
    })
    Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true })
    const exportar = capturar('exportarAuditoria', {
      status: 200,
      corpo: 'a;b',
      cabecalhos: {
        'content-type': 'text/csv',
        'content-disposition': 'attachment; filename="auditoria-2026-10-07.csv"',
      },
    })
    const { user, unmount } = renderizarTela('/rh/auditoria?area=PONTO&busca=ana')
    await screen.findByRole('table', { name: 'Log de auditoria' })
    await user.click(screen.getByRole('button', { name: 'Exportar CSV' }))
    await waitFor(() => expect(baixados).toEqual(['auditoria-2026-10-07.csv']))
    const c = exportar.ultima().consulta
    expect(c.get('area')).toBe('PONTO')
    expect(c.get('busca')).toBe('ana')
    expect(c.has('pagina')).toBe(false)
    expect(c.has('apenasAlteracoes')).toBe(false)
    exportar.conforme()
    unmount()
    capturar('exportarAuditoria', problema(500))
    const outra = renderizarTela('/rh/auditoria')
    await screen.findByRole('table', { name: 'Log de auditoria' })
    await outra.user.click(screen.getByRole('button', { name: 'Exportar CSV' }))
    expect(
      await screen.findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
    expect(baixados).toHaveLength(1)
  })

  it('vazio, erro com Tentar de novo e nenhum botao de editar ou apagar', async () => {
    capturar('listarEventosDeAuditoria', {
      corpo: { ...LISTA, itens: [], total: 0, totalPaginas: 0 },
    })
    const { unmount } = renderizarTela('/rh/auditoria')
    expect(await screen.findByText('Nenhum registro neste período.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Editar|Apagar|Excluir/ })).toBeNull()
    unmount()
    capturar('listarEventosDeAuditoria', problema(500))
    renderizarTela('/rh/auditoria')
    expect(await screen.findByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
  })
})
