import { screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { exemplos } from '../../shared/mocks/gerado/handlers'
import { capturar, problema } from '../../test/capturar'
import { preencherData } from '../../test/formulario'
import { renderizarTela } from '../../test/telas'
import { mensagemDeAusencia } from './gestores/mensagemDeAusencia'

type Lista = { itens: Record<string, unknown>[] }
const GESTORES = exemplos.listarGestores.corpo as Lista
const BENEFICIOS = exemplos.listarBeneficiosDoCatalogo.corpo as Lista & Record<string, unknown>
const AUSENCIA = exemplos.configurarAusenciaDoGestor.corpo as Record<string, unknown>

afterEach(() => vi.useRealTimers())

describe('Cadastros: Gestores e Gestor ausente', () => {
  it('mensagemDeAusencia: zeros, so pontos, so ferias, singular', () => {
    const com = (ajustesDePonto: number, ferias: number) =>
      mensagemDeAusencia({
        ...AUSENCIA,
        pedidosAprovadosAgora: { ajustesDePonto, ferias },
      } as never)
    expect(com(0, 0)).toBe('Marcação salva.')
    expect(com(2, 1)).toBe(
      '2 pedidos de ajuste de ponto e 1 pedido de férias passaram para a etapa do RH.',
    )
    expect(com(3, 0)).toBe(
      '3 pedidos de ajuste de ponto e 0 pedidos de férias passaram para a etapa do RH.',
    )
    expect(com(1, 0)).toBe(
      '1 pedido de ajuste de ponto e 0 pedidos de férias passou para a etapa do RH.',
    )
  })

  it('tabela: Gestor com cargo, Setor, Equipe e o interruptor; Paulo ligado com a data de volta', async () => {
    const lista = capturar('listarGestores')
    renderizarTela('/rh/cadastros/gestores')
    const tabela = await screen.findByRole('table', { name: 'Gestores' })
    expect(await within(tabela).findByText('Carlos Ribeiro')).toBeInTheDocument()
    expect(within(tabela).getByText('Gerente comercial')).toBeInTheDocument()
    expect(within(tabela).getByText('8 pessoas')).toBeInTheDocument()
    expect(within(tabela).getAllByText('Só decide pedidos de gestores')).toHaveLength(2)
    expect(screen.getByRole('switch', { name: 'Gestor ausente: Carlos Ribeiro' })).toHaveAttribute(
      'aria-checked',
      'false',
    )
    expect(screen.getByRole('switch', { name: 'Gestor ausente: Paulo Vieira' })).toHaveAttribute(
      'aria-checked',
      'true',
    )
    expect(within(tabela).getByText('Volta em 19/10/2026')).toBeInTheDocument()
    lista.conforme()
  })

  it('o interruptor reflete ausenteHoje: passada a data de volta fica desligado mesmo com ausente=true', async () => {
    capturar('listarGestores', {
      corpo: { itens: [{ ...GESTORES.itens[2], ausente: true, ausenteHoje: false }] },
    })
    renderizarTela('/rh/cadastros/gestores')
    expect(
      await screen.findByRole('switch', { name: 'Gestor ausente: Paulo Vieira' }),
    ).toHaveAttribute('aria-checked', 'false')
  })

  it('quem so decide pedidos de gestores tem o interruptor desabilitado com a dica', async () => {
    renderizarTela('/rh/cadastros/gestores')
    const chave = await screen.findByRole('switch', { name: 'Gestor ausente: Ana Souza' })
    expect(chave).toHaveAttribute('aria-disabled', 'true')
    expect(
      screen.getAllByText('Não tem equipe: não há etapa do gestor para aprovar sozinha.').length,
    ).toBeGreaterThan(0)
  })

  it('ligar abre o dialogo; a data precisa ser depois de hoje; Marcar envia { ausente: true, ausenteAte } conforme', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
    vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
    const ausencia = capturar('configurarAusenciaDoGestor')
    const { user } = renderizarTela('/rh/cadastros/gestores')
    await user.click(await screen.findByRole('switch', { name: 'Gestor ausente: Carlos Ribeiro' }))
    const dialogo = await screen.findByRole('dialog', {
      name: 'Marcar Carlos Ribeiro como ausente',
    })
    expect(
      within(dialogo).getByText(
        /a etapa do gestor nos pedidos de ponto e de férias da equipe é aprovada sozinha/,
      ),
    ).toBeInTheDocument()
    preencherData('Data de volta (opcional)', '2026-10-01')
    await user.click(within(dialogo).getByRole('button', { name: 'Marcar como ausente' }))
    expect(
      await within(dialogo).findByText('A data de volta precisa ser depois de hoje.'),
    ).toBeInTheDocument()
    expect(ausencia.quantas()).toBe(0)
    preencherData('Data de volta (opcional)', '2026-10-20')
    await user.click(within(dialogo).getByRole('button', { name: 'Marcar como ausente' }))
    await waitFor(() => expect(ausencia.quantas()).toBe(1))
    expect(ausencia.ultima().corpo).toEqual({ ausente: true, ausenteAte: '2026-10-20' })
    expect(ausencia.ultima().parametrosDoCaminho).toEqual({ gestorId: '1' })
    ausencia.conforme()
    expect(await screen.findByText('Marcação salva.')).toBeInTheDocument()
  })

  it('desligar envia { ausente: false, ausenteAte: null } na hora, sem dialogo; 422 mostra o detail', async () => {
    const ausencia = capturar(
      'configurarAusenciaDoGestor',
      problema(422, { codigo: 'GESTOR_SEM_EQUIPE', detail: 'Este gestor não tem equipe.' }),
    )
    const { user } = renderizarTela('/rh/cadastros/gestores')
    await user.click(await screen.findByRole('switch', { name: 'Gestor ausente: Paulo Vieira' }))
    await waitFor(() => expect(ausencia.quantas()).toBe(1))
    expect(screen.queryByRole('dialog', { name: /como ausente/ })).toBeNull()
    expect(ausencia.ultima().corpo).toEqual({ ausente: false, ausenteAte: null })
    expect(await screen.findByText('Este gestor não tem equipe.')).toBeInTheDocument()
  })

  it('Cadastrar gestor envia { funcionarioId } conforme; 422 mostra o detail', async () => {
    const cadastrar = capturar(
      'cadastrarGestor',
      problema(422, {
        codigo: 'GESTOR_SEM_SETOR_SO_PARA_RH',
        detail: 'Só quem é do RH é cadastrado como gestor sem setor.',
      }),
    )
    const { user } = renderizarTela('/rh/cadastros/gestores')
    await user.click(await screen.findByRole('button', { name: 'Cadastrar gestor' }))
    const modal = await screen.findByRole('dialog', { name: 'Cadastrar gestor' })
    expect(within(modal).getByText(/Só quem é do RH é cadastrado aqui/)).toBeInTheDocument()
    await user.type(within(modal).getByRole('combobox', { name: 'Pessoa' }), 'bru')
    await user.click(await screen.findByRole('option', { name: /Bruno Lima/ }, { timeout: 2000 }))
    await user.click(within(modal).getByRole('button', { name: 'Cadastrar gestor' }))
    await waitFor(() => expect(cadastrar.quantas()).toBe(1))
    expect(cadastrar.ultima().corpo).toEqual({ funcionarioId: 12 })
    cadastrar.conforme()
    expect(
      await within(modal).findByText('Só quem é do RH é cadastrado como gestor sem setor.'),
    ).toBeInTheDocument()
  })
})

describe('Cadastros: Benefícios', () => {
  it('tabela: Beneficio com descricao, Quem pode pedir, Dependentes, Trajeto, Pessoas, Editar e Excluir', async () => {
    const lista = capturar('listarBeneficiosDoCatalogo')
    renderizarTela('/rh/cadastros/beneficios')
    const tabela = await screen.findByRole('table', { name: 'Benefícios' })
    expect(
      within(tabela)
        .getAllByRole('columnheader')
        .map((c) => c.textContent),
    ).toEqual(['Benefício', 'Quem pode pedir', 'Dependentes', 'Trajeto', 'Pessoas', 'Ações'])
    expect(await within(tabela).findAllByText('Só o RH concede')).not.toHaveLength(0)
    expect(within(tabela).getAllByRole('button', { name: 'Excluir' }).length).toBe(
      BENEFICIOS.itens.length,
    )
    lista.conforme()
  })

  it('Novo beneficio: as tres marcas com apoio e envia sempre as tres, conforme', async () => {
    const criar = capturar('criarBeneficioDoCatalogo')
    const { user } = renderizarTela('/rh/cadastros/beneficios')
    await user.click(await screen.findByRole('button', { name: 'Novo benefício' }))
    const modal = await screen.findByRole('dialog', { name: 'Novo benefício' })
    expect(
      within(modal).getByText(/A própria pessoa pode pedir este benefício/),
    ).toBeInTheDocument()
    await user.type(within(modal).getByLabelText('Nome do benefício'), 'Academia')
    await user.click(within(modal).getByText('Aceita dependentes'))
    await user.click(screen.getByRole('button', { name: 'Salvar benefício' }))
    await waitFor(() => expect(criar.quantas()).toBe(1))
    expect(criar.ultima().corpo).toEqual({
      nome: 'Academia',
      descricao: null,
      opcional: false,
      aceitaDependentes: true,
      aceitaTrajeto: false,
    })
    criar.conforme()
  })

  it('Editar abre o modal preenchido e envia o PUT com o id da linha, conforme', async () => {
    const primeiro = BENEFICIOS.itens[0] as { id: number; nome: string; opcional: boolean }
    const editar = capturar('editarBeneficioDoCatalogo')
    const { user } = renderizarTela('/rh/cadastros/beneficios')
    const tabela = await screen.findByRole('table', { name: 'Benefícios' })
    const linha = (await within(tabela).findByText(primeiro.nome)).closest('tr')!
    await user.click(within(linha).getByRole('button', { name: 'Editar' }))
    const modal = await screen.findByRole('dialog', { name: 'Editar benefício' })
    const nome = within(modal).getByLabelText('Nome do benefício')
    expect(nome).toHaveValue(primeiro.nome)
    await user.clear(nome)
    await user.type(nome, `${primeiro.nome} novo`)
    await user.click(screen.getByRole('button', { name: 'Salvar benefício' }))
    await waitFor(() => expect(editar.quantas()).toBe(1))
    expect(editar.ultima().parametrosDoCaminho).toEqual({ beneficioId: String(primeiro.id) })
    expect(editar.ultima().corpo).toMatchObject({
      nome: `${primeiro.nome} novo`,
      opcional: primeiro.opcional,
    })
    editar.conforme()
    await waitFor(() =>
      expect(screen.queryByRole('dialog', { name: 'Editar benefício' })).toBeNull(),
    )
  })

  it('Excluir pede confirmacao e chama a rota; 409 CADASTRO_EM_USO mostra o detail e nao fecha', async () => {
    const excluir = capturar(
      'excluirBeneficioDoCatalogo',
      problema(409, {
        codigo: 'CADASTRO_EM_USO',
        detail: 'Este benefício já foi dado a colaboradores e não pode ser excluído.',
      }),
    )
    const { user } = renderizarTela('/rh/cadastros/beneficios')
    const tabela = await screen.findByRole('table', { name: 'Benefícios' })
    const linha = (await within(tabela).findByText('Vale-refeição')).closest('tr')!
    await user.click(within(linha).getByRole('button', { name: 'Excluir' }))
    const dialogo = await screen.findByRole('alertdialog', {
      name: 'Excluir o benefício Vale-refeição?',
    })
    expect(
      within(dialogo).getByText('Só é possível se ele nunca foi dado a ninguém.'),
    ).toBeInTheDocument()
    await user.click(within(dialogo).getByRole('button', { name: 'Excluir' }))
    expect(await within(dialogo).findByText(/já foi dado a colaboradores/)).toBeInTheDocument()
    expect(excluir.ultima().parametrosDoCaminho).toEqual({ beneficioId: '1' })
    excluir.conforme()
  })

  it('excluir o unico item da pagina 2 volta para a pagina 1 (Review Focus 2)', async () => {
    const lista = capturar('listarBeneficiosDoCatalogo', {
      corpo: { ...BENEFICIOS, itens: [], pagina: 2, total: 20, totalPaginas: 1 },
    })
    const { roteador } = renderizarTela('/rh/cadastros/beneficios?pagina=2')
    await waitFor(() => expect(roteador.state.location.search).toBe(''))
    await waitFor(() => expect(lista.ultima().consulta.get('pagina')).toBe('1'))
  })
})
