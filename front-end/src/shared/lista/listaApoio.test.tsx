import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { createMemoryRouter, Outlet, RouterProvider } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { FiltroDeSelecao } from './FiltroDeSelecao'
import { usePainelSobreLista } from './usePainelSobreLista'
import { inteiroDaUrl, valorDeEnum } from './valoresDeLista'

describe('valoresDeLista', () => {
  it.each([
    ['INATIVO', 'INATIVO'],
    ['XYZ', undefined],
    [undefined, undefined],
  ])('valorDeEnum aceita so valores da lista (%s)', (entrada, saida) => {
    expect(valorDeEnum(entrada, ['ATIVO', 'EM_ADMISSAO', 'INATIVO'] as const)).toBe(saida)
  })

  it.each([
    ['2', 2],
    ['0', undefined],
    ['-3', undefined],
    ['abc', undefined],
    ['1.5', undefined],
    ['', undefined],
    [undefined, undefined],
  ])('inteiroDaUrl aceita so inteiro maior ou igual a 1 (%s)', (entrada, saida) => {
    expect(inteiroDaUrl(entrada)).toBe(saida)
  })
})

describe('FiltroDeSelecao', () => {
  it('mostra o texto de todos, escolhe uma opcao (aoMudar com o valor) e volta a todos (aoMudar(null))', async () => {
    const user = userEvent.setup()
    const aoMudar = vi.fn()
    const { rerender } = render(
      <FiltroDeSelecao
        rotulo="Setor"
        todos="Todos os setores"
        opcoes={[{ valor: '1', rotulo: 'Comercial' }]}
        valor={undefined}
        aoMudar={aoMudar}
      />,
    )
    const filtro = screen.getByRole('combobox', { name: 'Setor' })
    expect(filtro).toHaveTextContent('Todos os setores')
    await user.click(filtro)
    await user.click(await screen.findByRole('option', { name: 'Comercial' }))
    expect(aoMudar).toHaveBeenLastCalledWith('1')
    rerender(
      <FiltroDeSelecao
        rotulo="Setor"
        todos="Todos os setores"
        opcoes={[{ valor: '1', rotulo: 'Comercial' }]}
        valor="1"
        aoMudar={aoMudar}
      />,
    )
    await user.click(screen.getByRole('combobox', { name: 'Setor' }))
    await user.click(await screen.findByRole('option', { name: 'Todos os setores' }))
    expect(aoMudar).toHaveBeenLastCalledWith(null)
  })
})

describe('usePainelSobreLista', () => {
  function montar() {
    let painel!: ReturnType<typeof usePainelSobreLista>
    function Captura({ children }: { children?: ReactNode }) {
      painel = usePainelSobreLista()
      return <>{children}</>
    }
    const roteador = createMemoryRouter(
      [
        {
          path: '/colaboradores',
          element: <Outlet />,
          children: [{ path: 'novo', element: <Captura /> }],
        },
      ],
      { initialEntries: ['/colaboradores/novo?situacao=INATIVO&aba=documentos'] },
    )
    render(<RouterProvider router={roteador} />)
    return { roteador, painel: () => painel }
  }

  it('paraPainel mantem a busca da lista e tira aba', () => {
    const { painel } = montar()
    expect(painel().paraPainel('12')).toEqual({ pathname: '12', search: '?situacao=INATIVO' })
    expect(painel().paraPainel('12', { aba: 'ponto' })).toEqual({
      pathname: '12',
      search: '?situacao=INATIVO&aba=ponto',
    })
  })

  it('fechar volta a rota pai com os filtros e sem aba, e leva o estado', () => {
    const { roteador, painel } = montar()
    act(() => painel().fechar({ x: 1 }))
    expect(roteador.state.location.pathname).toBe('/colaboradores')
    expect(roteador.state.location.search).toBe('?situacao=INATIVO')
    expect(roteador.state.location.state).toEqual({ x: 1 })
  })
})
