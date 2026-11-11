import { act, renderHook } from '@testing-library/react'
import type { ReactNode } from 'react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { describe, expect, it } from 'vitest'
import { useEstadoDaLista } from './useEstadoDaLista'

type Opcoes = Parameters<typeof useEstadoDaLista<'situacao' | 'setorId'>>[0]

function montar(url: string, opcoes: Opcoes = { filtros: ['situacao', 'setorId'] }) {
  let roteador!: ReturnType<typeof createMemoryRouter>
  const wrapper = ({ children }: { children: ReactNode }) => {
    roteador ??= createMemoryRouter([{ path: '/lista', element: children }], {
      initialEntries: [url],
    })
    return <RouterProvider router={roteador} />
  }
  const resultado = renderHook(() => useEstadoDaLista(opcoes), { wrapper })
  return { ...resultado, url: () => roteador.state.location.search, roteador: () => roteador }
}

describe('useEstadoDaLista', () => {
  it('le busca, filtros, pagina e ordenacao da URL', () => {
    const { result } = montar(
      '/lista?busca=ana&situacao=ATIVO&pagina=2&ordenarPor=nome&direcao=DESC',
    )
    expect(result.current.consulta).toEqual({
      pagina: 2,
      tamanhoPagina: 20,
      busca: 'ana',
      ordenarPor: 'nome',
      direcao: 'DESC',
      situacao: 'ATIVO',
    })
    expect(result.current.busca).toBe('ana')
    expect(result.current.filtros).toEqual({ situacao: 'ATIVO' })
    expect(result.current.ordenacao).toEqual({ campo: 'nome', direcao: 'DESC' })
  })

  it('mudar a busca, um filtro ou a ordenacao volta a pagina 1 e tira pagina=1 da URL (Review Focus 5)', () => {
    const { result, url } = montar('/lista?pagina=3')
    act(() => result.current.definirBusca('bruno'))
    expect(url()).toBe('?busca=bruno')
    act(() => result.current.definirPagina(4))
    act(() => result.current.definirFiltro('situacao', 'INATIVO'))
    expect(result.current.pagina).toBe(1)
    expect(url()).not.toContain('pagina')
    act(() => result.current.definirPagina(2))
    act(() => result.current.alternarOrdenacao('nome'))
    expect(result.current.pagina).toBe(1)
    expect(url()).not.toContain('pagina')
  })

  it('definirPagina(3) escreve pagina=3 e definirPagina(1) a tira', () => {
    const { result, url } = montar('/lista')
    act(() => result.current.definirPagina(3))
    expect(url()).toBe('?pagina=3')
    act(() => result.current.definirPagina(1))
    expect(url()).toBe('')
  })

  it.each(['?pagina=0', '?pagina=abc', '?pagina=-3', '?pagina=1.5'])(
    'pagina invalida na URL vale 1 (%s)',
    (q) => {
      expect(montar(`/lista${q}`).result.current.pagina).toBe(1)
    },
  )

  it('alternarOrdenacao: campo novo ASC, o mesmo inverte, e usa a ordenacaoPadrao no inicio', () => {
    const { result } = montar('/lista', {
      filtros: [],
      ordenacaoPadrao: { campo: 'nome', direcao: 'ASC' },
    })
    expect(result.current.ordenacao).toEqual({ campo: 'nome', direcao: 'ASC' })
    expect(result.current.consulta).toMatchObject({ ordenarPor: 'nome', direcao: 'ASC' })
    act(() => result.current.alternarOrdenacao('nome'))
    expect(result.current.ordenacao).toEqual({ campo: 'nome', direcao: 'DESC' })
    act(() => result.current.alternarOrdenacao('admissao'))
    expect(result.current.ordenacao).toEqual({ campo: 'admissao', direcao: 'ASC' })
  })

  it('definirFiltro com null ou vazio tira o filtro da URL', () => {
    const { result, url } = montar('/lista?situacao=ATIVO&setorId=2')
    act(() => result.current.definirFiltro('situacao', null))
    expect(url()).toBe('?setorId=2')
    act(() => result.current.definirFiltro('setorId', ''))
    expect(url()).toBe('')
  })

  it('so os filtros declarados entram na consulta (outros parametros da URL sao ignorados)', () => {
    const { result } = montar('/lista?situacao=ATIVO&invasor=1')
    expect(result.current.consulta).toEqual({ pagina: 1, tamanhoPagina: 20, situacao: 'ATIVO' })
  })

  it('definirBusca troca a entrada do historico (replace)', () => {
    const { result, roteador } = montar('/lista')
    act(() => result.current.definirPagina(2))
    const chaveAntes = roteador().state.location.key
    act(() => result.current.definirBusca('a'))
    act(() => result.current.definirBusca('an'))
    act(() => roteador().navigate(-1))
    expect(roteador().state.location.key).not.toBe(chaveAntes)
    expect(roteador().state.location.search).toBe('')
  })
})
