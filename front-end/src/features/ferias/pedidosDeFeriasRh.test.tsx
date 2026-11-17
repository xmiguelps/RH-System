import { screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Esquema } from '../../shared/api/tipos'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { escolherOpcao } from '../../test/formulario'
import { renderizarTela } from '../../test/telas'

type Lista = Esquema<'FeriasListaAnalise'>
const ex = <T,>(op: string, status?: number, nome?: string) =>
  exemploDoContrato<T>(op, status, nome)
const LISTA = ex<Lista>('listarFeriasParaORh')
const PRIMEIRO = LISTA.itens[0]!
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

async function abrir(rota = '/rh/pedidos-de-ferias') {
  const tela = renderizarTela(rota)
  await screen.findByRole('heading', { level: 1, name: 'Pedidos de férias', hidden: true })
  const lista = await screen.findByRole('list', { name: 'Pedidos de férias' })
  return { ...tela, lista }
}
const cartao = (lista: HTMLElement, nome: string) =>
  within(lista).getByRole('listitem', { name: `Férias de ${nome}` })

describe('Pedidos de férias para o RH (tela 7.2)', { timeout: 20000 }, () => {
  it('abre em "Aguardando o RH", pede a pagina de 8 e mostra um cartao por pedido com cargo e setor, conforme o contrato', async () => {
    const lista = capturar('listarFeriasParaORh')
    const { lista: itens } = await abrir()
    expect(screen.getByText('Os que esperam a confirmação do RH')).toBeInTheDocument()
    expect(lista.ultima().consulta.get('situacao')).toBe('COM_O_RH')
    expect(lista.ultima().consulta.get('pagina')).toBe('1')
    expect(lista.ultima().consulta.get('tamanhoPagina')).toBe('8')
    expect(lista.ultima().consulta.has('busca')).toBe(false)
    expect(lista.ultima().consulta.has('setorId')).toBe(false)
    lista.conforme()
    expect(within(itens).getAllByRole('listitem')).toHaveLength(LISTA.itens.length)
    const primeiro = cartao(itens, PRIMEIRO.funcionario.nome)
    expect(
      within(primeiro).getByText(/pediu \d+ dias?, de \d{2}\/\d{2} a \d{2}\/\d{2}\./),
    ).toBeInTheDocument()
    expect(within(primeiro).getByRole('button', { name: 'Confirmar' })).toBeInTheDocument()
    expect(within(primeiro).getByRole('button', { name: 'Recusar' })).toBeInTheDocument()
    expect(within(primeiro).queryByRole('button', { name: 'Aprovar' })).toBeNull()
  })

  it('mostra a frase da decisao do gestor, o motivo e o rotulo da situacao no cartao', async () => {
    const { lista: itens } = await abrir()
    const primeiro = cartao(itens, PRIMEIRO.funcionario.nome)
    expect(within(primeiro).getByText('Aguardando o RH')).toBeInTheDocument()
    expect(within(primeiro).getByText(/^Aprovado pelo gestor /)).toBeInTheDocument()
  })

  it('"Confirmar" chama confirmarFeriasComoRh sem corpo e pede a lista e os contadores de novo', async () => {
    const confirmar = capturar('confirmarFeriasComoRh')
    const lista = capturar('listarFeriasParaORh')
    const contadores = capturar('obterContadoresDoMenuDoRh')
    const { user, lista: itens } = await abrir()
    const antes = lista.quantas()
    await user.click(
      within(cartao(itens, PRIMEIRO.funcionario.nome)).getByRole('button', { name: 'Confirmar' }),
    )
    await waitFor(() => expect(confirmar.quantas()).toBe(1))
    expect(confirmar.ultima().parametrosDoCaminho).toEqual({ feriasId: String(PRIMEIRO.id) })
    confirmar.conforme()
    expect(await screen.findByText('Férias confirmadas.')).toBeInTheDocument()
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(antes))
    expect(contadores.quantas()).toBeGreaterThan(0)
  })

  it('"Recusar" abre o motivo (5 a 500 caracteres) e manda { motivo } pela rota do RH', async () => {
    const recusar = capturar('recusarFeriasComoRh')
    const { user, lista: itens } = await abrir()
    await user.click(
      within(cartao(itens, PRIMEIRO.funcionario.nome)).getByRole('button', { name: 'Recusar' }),
    )
    const modal = await screen.findByRole('dialog', { name: 'Recusar pedido de férias' })
    await user.type(within(modal).getByLabelText('Motivo'), 'ab')
    await user.click(within(modal).getByRole('button', { name: 'Recusar' }))
    expect(
      await within(modal).findByText('Escreva o motivo com 5 a 500 caracteres.'),
    ).toBeInTheDocument()
    expect(recusar.quantas()).toBe(0)
    await user.clear(within(modal).getByLabelText('Motivo'))
    await user.type(within(modal).getByLabelText('Motivo'), 'Período de fechamento da folha')
    await user.click(within(modal).getByRole('button', { name: 'Recusar' }))
    await waitFor(() => expect(recusar.quantas()).toBe(1))
    expect(recusar.ultima().corpo).toEqual({ motivo: 'Período de fechamento da folha' })
    expect(recusar.ultima().parametrosDoCaminho).toEqual({ feriasId: String(PRIMEIRO.id) })
    recusar.conforme()
  })

  it.each(['pedidoProprio', 'foraDeGestores'] as const)(
    '422 %s mostra o titulo e o detalhe do contrato dentro do cartao (sem mexer na lista)',
    async (nome) => {
      capturar('confirmarFeriasComoRh', problemaDoContrato('confirmarFeriasComoRh', 422, nome))
      const { user, lista: itens } = await abrir()
      const c = cartao(itens, PRIMEIRO.funcionario.nome)
      await user.click(within(c).getByRole('button', { name: 'Confirmar' }))
      const erro = ex<{ title: string; detail: string }>('confirmarFeriasComoRh', 422, nome)
      expect(
        await within(c).findByText(new RegExp(erro.title.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'))),
      ).toBeInTheDocument()
    },
  )

  it.each(['etapaIncorreta', 'jaDecidido'] as const)(
    '409 %s mostra o texto do contrato e pede a lista de novo',
    async (nome) => {
      capturar('confirmarFeriasComoRh', problemaDoContrato('confirmarFeriasComoRh', 409, nome))
      const lista = capturar('listarFeriasParaORh')
      const { user, lista: itens } = await abrir()
      const antes = lista.quantas()
      await user.click(
        within(cartao(itens, PRIMEIRO.funcionario.nome)).getByRole('button', { name: 'Confirmar' }),
      )
      const detalhe = ex<{ detail: string }>('confirmarFeriasComoRh', 409, nome).detail
      expect(await screen.findByText(detalhe)).toBeInTheDocument()
      await waitFor(() => expect(lista.quantas()).toBeGreaterThan(antes))
    },
  )

  it('podeDecidir false desliga os botoes e mostra motivoNaoPodeDecidir como veio', async () => {
    capturar('listarFeriasParaORh', {
      corpo: {
        ...LISTA,
        itens: [
          {
            ...PRIMEIRO,
            podeDecidir: false,
            motivoNaoPodeDecidir:
              'Este pedido não passa pelo gestor e o banco exige que quem decide também esteja cadastrado como gestor. Peça a outra pessoa do RH.',
          },
        ],
      },
    })
    const { lista: itens } = await abrir()
    const c = cartao(itens, PRIMEIRO.funcionario.nome)
    expect(within(c).getByText(/Peça a outra pessoa do RH\./)).toBeInTheDocument()
    expect(within(c).getByRole('button', { name: 'Confirmar' })).toBeDisabled()
    expect(within(c).getByRole('button', { name: 'Recusar' })).toBeDisabled()
  })

  it('?pedido=<id> destaca o cartao do pedido (o painel do RH leva para ca)', async () => {
    const { lista: itens } = await abrir(`/rh/pedidos-de-ferias?pedido=${PRIMEIRO.id}`)
    expect(cartao(itens, PRIMEIRO.funcionario.nome).className).toMatch(/border-2/)
    const outro = LISTA.itens.find((i) => i.id !== PRIMEIRO.id)
    if (outro) expect(cartao(itens, outro.funcionario.nome).className).not.toMatch(/border-2/)
  })

  it('situacao, setor e busca viram parametros da chamada (e ficam na URL)', async () => {
    const lista = capturar('listarFeriasParaORh')
    const { user, roteador } = await abrir()
    await escolherOpcao(user, 'Situação', 'Aprovada')
    await waitFor(() => expect(lista.ultima().consulta.get('situacao')).toBe('APROVADA'))
    expect(roteador.state.location.search).toContain('situacao=APROVADA')
    await escolherOpcao(user, 'Setor', 'Comercial')
    await waitFor(() => expect(lista.ultima().consulta.get('setorId')).toBe('1'))
    await user.type(screen.getByRole('searchbox', { name: 'Buscar colaborador' }), 'Pedro')
    await waitFor(() => expect(lista.ultima().consulta.get('busca')).toBe('Pedro'))
    expect(lista.ultima().consulta.get('situacao')).toBe('APROVADA')
    expect(lista.ultima().consulta.get('setorId')).toBe('1')
  })

  it('vazio: "Nenhum pedido de férias esperando o RH." e, com filtro, a mensagem do filtro', async () => {
    capturar('listarFeriasParaORh', { corpo: { ...LISTA, itens: [], total: 0, totalPaginas: 0 } })
    renderizarTela('/rh/pedidos-de-ferias')
    expect(await screen.findByText('Nenhum pedido de férias esperando o RH.')).toBeInTheDocument()
    capturar('listarFeriasParaORh', { corpo: { ...LISTA, itens: [], total: 0, totalPaginas: 0 } })
    renderizarTela('/rh/pedidos-de-ferias?busca=zzz')
    expect(
      await screen.findByText('Nenhum pedido de férias encontrado para este filtro.'),
    ).toBeInTheDocument()
  })

  it('erro mostra o aviso com "Tentar de novo"', async () => {
    capturar('listarFeriasParaORh', problema(500, { title: 'Erro interno.', status: 500 }))
    renderizarTela('/rh/pedidos-de-ferias')
    expect(
      await screen.findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
  })

  it('com varias paginas a paginacao pede a pagina 2', async () => {
    const lista = capturar('listarFeriasParaORh', {
      corpo: { ...LISTA, pagina: 1, tamanhoPagina: 8, total: 20, totalPaginas: 3 },
    })
    const { user } = await abrir()
    await user.click(await screen.findByRole('button', { name: 'Próxima' }))
    await waitFor(() => expect(lista.ultima().consulta.get('pagina')).toBe('2'))
  })
})
