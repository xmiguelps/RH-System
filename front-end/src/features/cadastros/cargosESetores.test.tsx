import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { exemplos } from '../../shared/mocks/gerado/handlers'
import { capturar, problema } from '../../test/capturar'
import { renderizarTela } from '../../test/telas'
import { formatarCargaSemanal } from './cargos/rotulos'

declare global {
  interface Window {
    __x?: number
  }
}

type Lista = { itens: Record<string, unknown>[]; total: number; totalPaginas: number }
const CARGOS = exemplos.listarCargos.corpo as Lista

describe('Cadastros: moldura (tela 29)', () => {
  it('/rh/cadastros leva a Cargos e mostra o titulo, o subtitulo e o item ativo', async () => {
    const { roteador } = renderizarTela('/rh/cadastros')
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/rh/cadastros/cargos'))
    expect(await screen.findByRole('heading', { level: 1, name: 'Cadastros' })).toBeInTheDocument()
    expect(
      screen.getByText(
        'Tabelas de apoio que o RH mantém. Elas viram as opções que aparecem no resto do sistema.',
      ),
    ).toBeInTheDocument()
    const menu = screen.getByRole('navigation', { name: 'Cadastros' })
    expect(within(menu).getByRole('link', { name: /Cargos/ })).toHaveAttribute(
      'aria-current',
      'page',
    )
  })

  it('o menu do cadastro tem titulo, apoio e o numero de cada lista e nao tem Niveis de acesso', async () => {
    const contagem = capturar('listarCargos')
    renderizarTela('/rh/cadastros/empresa')
    const menu = await screen.findByRole('navigation', { name: 'Cadastros' })
    expect(await within(menu).findByText('14')).toBeInTheDocument()
    expect(within(menu).getByText('Nome e carga horária')).toBeInTheDocument()
    expect(await within(menu).findByText('104')).toBeInTheDocument()
    expect(within(menu).getByText('Nome, horário e dia de pagamento')).toBeInTheDocument()
    expect(screen.queryByText('Níveis de acesso')).toBeNull()
    await waitFor(() => expect(contagem.quantas()).toBeGreaterThan(0))
    expect(contagem.ultima().consulta.get('tamanhoPagina')).toBe('1')
    contagem.conforme()
  })

  it('erro numa contagem some o numero sem mensagem', async () => {
    capturar('listarUsuariosDoSistema', problema(500))
    renderizarTela('/rh/cadastros/empresa')
    const menu = await screen.findByRole('navigation', { name: 'Cadastros' })
    await within(menu).findByText('14')
    expect(
      within(within(menu).getByRole('link', { name: /Usuários do sistema/ })).queryByText(/\d/),
    ).toBeNull()
  })
})

describe('Cadastros: Cargos e o modal (telas 29 e 30)', () => {
  it('formatarCargaSemanal', () => {
    expect(formatarCargaSemanal(44)).toBe('44 h')
    expect(formatarCargaSemanal(37.5)).toBe('37,5 h')
  })

  it('mostra o texto de apoio, o botao Novo cargo, a busca e as colunas, sem Excluir', async () => {
    renderizarTela('/rh/cadastros/cargos')
    expect(
      await screen.findByText(/A carga horária do cargo é usada para calcular o saldo do ponto/),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Novo cargo' })).toBeInTheDocument()
    expect(screen.getByRole('searchbox', { name: 'Buscar neste cadastro' })).toBeInTheDocument()
    const tabela = await screen.findByRole('table', { name: 'Cargos' })
    expect(
      within(tabela)
        .getAllByRole('columnheader')
        .map((c) => c.textContent),
    ).toEqual(['Cargo', 'Descrição', 'Carga semanal', 'Pessoas', 'Ações'])
    expect(await within(tabela).findAllByText('44 h')).toHaveLength(2)
    expect(screen.queryByRole('button', { name: 'Excluir' })).toBeNull()
    expect(screen.getByText('Mostrando 3 de 14')).toBeInTheDocument()
  })

  it('a descricao e mostrada como texto, nunca como HTML (Review Focus 4)', async () => {
    capturar('listarCargos', {
      corpo: {
        ...CARGOS,
        itens: [{ ...CARGOS.itens[0], descricao: '<script>window.__x=1</script>' }],
      },
    })
    renderizarTela('/rh/cadastros/cargos')
    expect(await screen.findByText('<script>window.__x=1</script>')).toBeInTheDocument()
    expect(window.__x).toBeUndefined()
  })

  it('com totalPaginas 2 aparecem Anterior e Proxima', async () => {
    capturar('listarCargos', { corpo: { ...CARGOS, totalPaginas: 2 } })
    renderizarTela('/rh/cadastros/cargos')
    expect(await screen.findByRole('button', { name: 'Próxima' })).toBeInTheDocument()
  })

  it('busca manda busca= e e conforme; vazio com busca; erro mantem o cabecalho e o botao', async () => {
    const lista = capturar('listarCargos', {
      corpo: { ...CARGOS, itens: [], total: 0, totalPaginas: 0 },
    })
    const { unmount } = renderizarTela('/rh/cadastros/cargos?busca=xyz')
    expect(await screen.findByText('Nenhum cargo encontrado.')).toBeInTheDocument()
    await waitFor(() =>
      expect(lista.todas.some((r) => r.consulta.get('busca') === 'xyz')).toBe(true),
    )
    lista.conforme()
    unmount()
    capturar('listarCargos', problema(500))
    renderizarTela('/rh/cadastros/cargos')
    expect(await screen.findByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Novo cargo' })).toBeInTheDocument()
  })

  it('Novo cargo: titulo, tres campos, a nota, e envia o corpo conforme; descricao vazia vira null', async () => {
    const criar = capturar('criarCargo')
    const { user } = renderizarTela('/rh/cadastros/cargos')
    await user.click(await screen.findByRole('button', { name: 'Novo cargo' }))
    const modal = await screen.findByRole('dialog', { name: 'Novo cargo' })
    expect(
      within(modal).getByText('A carga horária vale para todas as pessoas do cargo.'),
    ).toBeInTheDocument()
    expect(within(modal).getByText('horas')).toBeInTheDocument()
    await user.type(within(modal).getByLabelText('Nome do cargo'), 'Analista de compras')
    await user.type(within(modal).getByLabelText('Carga horária semanal'), '44')
    await user.click(screen.getByRole('button', { name: 'Salvar cargo' }))
    await waitFor(() => expect(criar.quantas()).toBe(1))
    expect(criar.ultima().corpo).toEqual({
      nome: 'Analista de compras',
      descricao: null,
      cargaSemanalHoras: 44,
    })
    criar.conforme()
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Novo cargo' })).toBeNull())
  })

  it('valida o nome e a carga (maior que zero ate 168, 2 casas)', async () => {
    const criar = capturar('criarCargo')
    const { user } = renderizarTela('/rh/cadastros/cargos')
    await user.click(await screen.findByRole('button', { name: 'Novo cargo' }))
    const modal = await screen.findByRole('dialog', { name: 'Novo cargo' })
    await user.type(within(modal).getByLabelText('Carga horária semanal'), '200')
    await user.click(screen.getByRole('button', { name: 'Salvar cargo' }))
    expect(await within(modal).findByText('Informe o nome do cargo.')).toBeInTheDocument()
    expect(
      within(modal).getByText('A carga semanal precisa ser maior que zero e no máximo 168 horas.'),
    ).toBeInTheDocument()
    expect(criar.quantas()).toBe(0)
  })

  it('Editar cargo vem preenchido, avisa da nova carga e manda PUT; 409 marca o Nome', async () => {
    const editar = capturar(
      'editarCargo',
      problema(409, {
        codigo: 'NOME_JA_CADASTRADO',
        detail: 'Já existe um cadastro com este nome.',
      }),
    )
    const { user } = renderizarTela('/rh/cadastros/cargos')
    const tabela = await screen.findByRole('table', { name: 'Cargos' })
    const linha = (await within(tabela).findByText('Vendedor')).closest('tr')!
    await user.click(within(linha).getByRole('button', { name: 'Editar' }))
    const modal = await screen.findByRole('dialog', { name: 'Editar cargo' })
    expect(within(modal).getByLabelText('Nome do cargo')).toHaveValue('Vendedor')
    expect(
      within(modal).getByText(/A nova carga vale para os dias de ponto criados depois/),
    ).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Salvar cargo' }))
    expect(
      await within(modal).findByText('Já existe um cadastro com este nome.'),
    ).toBeInTheDocument()
    expect(editar.ultima().parametrosDoCaminho).toEqual({ cargoId: '2' })
    editar.conforme()
  })
})

describe('Cadastros: Setores', () => {
  it('tabela: Setor, Descricao, Gestor (Sem gestor quando null), Pessoas e Editar; todos numa pagina', async () => {
    const lista = capturar('listarSetores')
    renderizarTela('/rh/cadastros/setores')
    const tabela = await screen.findByRole('table', { name: 'Setores' })
    expect(
      within(tabela)
        .getAllByRole('columnheader')
        .map((c) => c.textContent),
    ).toEqual(['Setor', 'Descrição', 'Gestor', 'Pessoas', 'Ações'])
    expect(await within(tabela).findByText('Carlos Ribeiro')).toBeInTheDocument()
    expect(within(tabela).getAllByText('Sem gestor').length).toBeGreaterThan(0)
    expect(screen.queryByRole('button', { name: 'Excluir' })).toBeNull()
    await waitFor(() =>
      expect(lista.todas.some((r) => r.consulta.get('tamanhoPagina') === '100')).toBe(true),
    )
    lista.conforme()
  })

  it('Novo setor: campos, frase de apoio e envia null para Sem gestor, conforme', async () => {
    const criar = capturar('criarSetor')
    const { user } = renderizarTela('/rh/cadastros/setores')
    await user.click(await screen.findByRole('button', { name: 'Novo setor' }))
    const modal = await screen.findByRole('dialog', { name: 'Novo setor' })
    expect(
      within(modal).getByText(/A pessoa escolhida passa a ter o nível Gestor/),
    ).toBeInTheDocument()
    await user.type(within(modal).getByLabelText('Nome do setor'), 'Jurídico')
    await user.click(screen.getByRole('button', { name: 'Salvar setor' }))
    await waitFor(() => expect(criar.quantas()).toBe(1))
    expect(criar.ultima().corpo).toEqual({
      nome: 'Jurídico',
      descricao: null,
      gestorFuncionarioId: null,
    })
    criar.conforme()
  })

  it('busca de gestor espera, manda situacao=ATIVO e envia o id do colaborador; trocar avisa dos pedidos', async () => {
    const pessoas = capturar('listarFuncionarios')
    const editar = capturar('editarSetor')
    const { user } = renderizarTela('/rh/cadastros/setores')
    const tabela = await screen.findByRole('table', { name: 'Setores' })
    const linha = (await within(tabela).findByText('Comercial')).closest('tr')!
    await user.click(within(linha).getByRole('button', { name: 'Editar' }))
    const modal = await screen.findByRole('dialog', { name: 'Editar setor' })
    const antes = pessoas.quantas()
    await user.clear(within(modal).getByRole('combobox', { name: 'Gestor' }))
    await user.type(within(modal).getByRole('combobox', { name: 'Gestor' }), 'bru')
    await waitFor(() => expect(pessoas.quantas()).toBe(antes + 1), { timeout: 2000 })
    expect(pessoas.ultima().consulta.get('situacao')).toBe('ATIVO')
    expect(pessoas.ultima().consulta.get('busca')).toBe('bru')
    expect(pessoas.ultima().consulta.has('perfil')).toBe(false)
    await user.click(await screen.findByRole('option', { name: /Bruno Lima/ }))
    expect(
      within(modal).getByText(
        'Os pedidos de ponto e de férias que esperavam pelo gestor atual passam para o RH.',
      ),
    ).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Salvar setor' }))
    await waitFor(() => expect(editar.quantas()).toBe(1))
    expect(editar.ultima().corpo?.gestorFuncionarioId).toBe(12)
    expect(editar.ultima().parametrosDoCaminho).toEqual({ setorId: '1' })
    editar.conforme()
  })

  it('409 GESTOR_JA_RESPONDE_POR_OUTRO_SETOR marca o Gestor com o detail', async () => {
    capturar(
      'criarSetor',
      problema(409, {
        codigo: 'GESTOR_JA_RESPONDE_POR_OUTRO_SETOR',
        detail:
          'Carlos Ribeiro já é o gestor do setor Comercial. Tire-o de lá antes de escolhê-lo aqui.',
      }),
    )
    const { user } = renderizarTela('/rh/cadastros/setores')
    await user.click(await screen.findByRole('button', { name: 'Novo setor' }))
    const modal = await screen.findByRole('dialog', { name: 'Novo setor' })
    await user.type(within(modal).getByLabelText('Nome do setor'), 'Jurídico')
    await user.click(screen.getByRole('button', { name: 'Salvar setor' }))
    expect(
      await within(modal).findByText(/Carlos Ribeiro já é o gestor do setor Comercial/),
    ).toBeInTheDocument()
  })
})
