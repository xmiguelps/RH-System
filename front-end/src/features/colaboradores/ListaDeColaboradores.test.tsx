import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { exemplos } from '../../shared/mocks/gerado/handlers'
import { capturar, problema } from '../../test/capturar'
import { escolherOpcao } from '../../test/formulario'
import { renderizarTela } from '../../test/telas'

type Lista = { itens: unknown[]; total: number; totalPaginas: number }
const LISTA = exemplos.listarFuncionarios.corpo as Lista

afterEach(() => {
  vi.useRealTimers()
})

describe('Lista de colaboradores (tela 01)', () => {
  it('mostra o titulo, o subtitulo com os totais e as tres abas com contagem', async () => {
    renderizarTela('/rh/colaboradores')
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Colaboradores' }),
    ).toBeInTheDocument()
    expect(await screen.findByText('101 ativos, 3 em admissão e 7 inativos')).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'Ativos 101' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('tab', { name: 'Em admissão 3' })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'Inativos 7' })).toBeInTheDocument()
  })

  it('mostra as seis colunas e uma linha por colaborador, sem a etiqueta Experiencia', async () => {
    renderizarTela('/rh/colaboradores')
    const tabela = await screen.findByRole('table', { name: 'Colaboradores' })
    const cabecalhos = within(tabela)
      .getAllByRole('columnheader')
      .map((c) => c.textContent)
    expect(cabecalhos).toEqual(['Nome', 'Cargo', 'Setor', 'Admissão', 'Situação hoje', 'Ações'])
    const bruno = (await within(tabela).findByText('Bruno Lima')).closest('tr')!
    expect(within(bruno).getByText('bruno.lima@empresa.com.br')).toBeInTheDocument()
    expect(within(bruno).getByText('BL')).toBeInTheDocument()
    expect(within(bruno).getByText('14/03/2022')).toBeInTheDocument()
    expect(within(bruno).getByText('Atestado')).toBeInTheDocument()
    expect(
      within(within(tabela).getByText('Carla Mendes').closest('tr')!).getByText('Ativo'),
    ).toBeInTheDocument()
    expect(
      within(within(tabela).getByText('Sofia Ramos').closest('tr')!).getByText('Ativo'),
    ).toBeInTheDocument()
    expect(screen.queryByText('Experiência')).toBeNull()
  })

  it('a requisicao e conforme o contrato e nao pede ordenacao nem tem controle de ordenacao', async () => {
    const lista = capturar('listarFuncionarios')
    renderizarTela('/rh/colaboradores')
    await screen.findByText('Bruno Lima')
    const consulta = lista.ultima().consulta
    expect(consulta.get('pagina')).toBe('1')
    expect(consulta.get('tamanhoPagina')).toBe('20')
    expect(consulta.get('situacao')).toBe('ATIVO')
    expect(consulta.has('ordenarPor')).toBe(false)
    expect(consulta.has('direcao')).toBe(false)
    lista.conforme()
    for (const c of screen.getAllByRole('columnheader')) expect(c).not.toHaveAttribute('aria-sort')
  })

  it('aba Inativos manda situacao=INATIVO, poe situacao na URL e volta a pagina 1', async () => {
    const lista = capturar('listarFuncionarios')
    const { user, roteador } = renderizarTela('/rh/colaboradores?pagina=2')
    await screen.findByText('Bruno Lima')
    await user.click(screen.getByRole('tab', { name: /^Inativos/ }))
    await waitFor(() => expect(lista.ultima().consulta.get('situacao')).toBe('INATIVO'))
    expect(lista.ultima().consulta.get('pagina')).toBe('1')
    expect(roteador.state.location.search).toBe('?situacao=INATIVO')
    await user.click(screen.getByRole('tab', { name: /^Ativos/ }))
    await waitFor(() => expect(roteador.state.location.search).toBe(''))
  })

  it('busca espera 300 ms, manda uma requisicao e corta em 100 caracteres', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const lista = capturar('listarFuncionarios')
    renderizarTela('/rh/colaboradores')
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime, delay: null })
    const busca = await screen.findByPlaceholderText('Buscar por nome, CPF ou matrícula')
    await screen.findByText('Bruno Lima')
    const antes = lista.quantas()
    await user.type(busca, 'a'.repeat(101))
    expect(lista.quantas()).toBe(antes)
    await act(async () => {
      vi.advanceTimersByTime(300)
    })
    await waitFor(() => expect(lista.quantas()).toBe(antes + 1))
    expect(lista.ultima().consulta.get('busca')).toHaveLength(100)
  })

  it('filtros de setor e de contrato mandam setorId (numero) e tipoContrato', async () => {
    const lista = capturar('listarFuncionarios')
    const setores = capturar('listarSetores')
    const { user } = renderizarTela('/rh/colaboradores')
    await screen.findByText('Bruno Lima')
    expect(screen.getByRole('combobox', { name: 'Setor' })).toHaveTextContent('Todos os setores')
    expect(screen.getByRole('combobox', { name: 'Contrato' })).toHaveTextContent(
      'Todos os contratos',
    )
    await waitFor(() => expect(setores.quantas()).toBeGreaterThan(0))
    expect(setores.ultima().consulta.get('tamanhoPagina')).toBe('100')
    setores.conforme()
    await escolherOpcao(user, 'Setor', 'Financeiro')
    await waitFor(() => expect(lista.ultima().consulta.get('setorId')).toBe('2'))
    await escolherOpcao(user, 'Contrato', 'Estágio')
    await waitFor(() => expect(lista.ultima().consulta.get('tipoContrato')).toBe('ESTAGIO'))
    lista.conforme()
  })

  it('o estado vem da URL e valor desconhecido vale o padrao', async () => {
    const lista = capturar('listarFuncionarios')
    renderizarTela('/rh/colaboradores?situacao=XYZ&setorId=abc&pagina=2')
    await screen.findByText('Bruno Lima')
    const consulta = lista.ultima().consulta
    expect(consulta.get('situacao')).toBe('ATIVO')
    expect(consulta.has('setorId')).toBe(false)
    expect(consulta.get('pagina')).toBe('2')
  })

  it('rodape escreve Mostrando 1 a 20 de 101 e Proxima manda pagina=2', async () => {
    const lista = capturar('listarFuncionarios')
    const { user } = renderizarTela('/rh/colaboradores')
    expect(await screen.findByText('Mostrando 1 a 20 de 101')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Próxima' }))
    await waitFor(() => expect(lista.ultima().consulta.get('pagina')).toBe('2'))
  })

  it('sem rodape quando total 0', async () => {
    capturar('listarFuncionarios', { corpo: { ...LISTA, itens: [], total: 0, totalPaginas: 0 } })
    renderizarTela('/rh/colaboradores')
    expect(await screen.findByText('Nenhum colaborador nesta aba.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Próxima' })).toBeNull()
  })

  it('carregando mostra o esqueleto, abas sem numero e subtitulo com traco', async () => {
    let liberar: () => void = () => {}
    capturar('listarFuncionarios', { espera: new Promise<void>((r) => (liberar = r)) })
    renderizarTela('/rh/colaboradores')
    expect(await screen.findByText('— ativos, — em admissão e — inativos')).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'Ativos' })).toBeInTheDocument()
    liberar()
    expect(await screen.findByText('101 ativos, 3 em admissão e 7 inativos')).toBeInTheDocument()
  })

  it('vazio com busca ou filtro', async () => {
    capturar('listarFuncionarios', { corpo: { ...LISTA, itens: [], total: 0, totalPaginas: 0 } })
    renderizarTela('/rh/colaboradores?busca=zzz')
    expect(await screen.findByText('Nenhum colaborador encontrado.')).toBeInTheDocument()
  })

  it('erro 500 mostra a mensagem com Tentar de novo e refaz a chamada; 403 da API mostra a mensagem de permissao', async () => {
    const lista = capturar('listarFuncionarios', problema(500))
    const { user, unmount } = renderizarTela('/rh/colaboradores')
    expect(
      await screen.findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
    const antes = lista.quantas()
    await user.click(screen.getByRole('button', { name: 'Tentar de novo' }))
    await waitFor(() => expect(lista.quantas()).toBe(antes + 1))
    unmount()
    capturar('listarFuncionarios', problema(403))
    renderizarTela('/rh/colaboradores')
    expect(await screen.findByText('Você não tem permissão para esta ação.')).toBeInTheDocument()
  })

  it('Funcionario em /rh/colaboradores ve o 403 da guarda e o menu dele', async () => {
    renderizarTela('/rh/colaboradores', { perfil: 'Funcionario' })
    expect(
      await screen.findByText('Você não tem permissão para ver esta página.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Analista pleno, Financeiro')).toBeInTheDocument()
  })

  it('os links Adicionar colaborador e Abrir ficha mantem o filtro da URL', async () => {
    renderizarTela('/rh/colaboradores?situacao=INATIVO')
    const adicionar = await screen.findByRole('link', { name: 'Adicionar colaborador' })
    expect(adicionar).toHaveAttribute('href', '/rh/colaboradores/novo?situacao=INATIVO')
    const bruno = (await screen.findByText('Bruno Lima')).closest('tr')!
    expect(within(bruno).getByRole('link', { name: 'Abrir ficha' })).toHaveAttribute(
      'href',
      '/rh/colaboradores/12?situacao=INATIVO',
    )
  })
})
