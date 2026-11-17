import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { criarRotas } from '../rotas'
import { PaginaDeErro } from './PaginaDeErro'

function Quebra(): never {
  throw new Error('Detalhe tecnico que a pessoa nao precisa ver: SELECT * FROM x')
}

afterEach(() => vi.restoreAllMocks())

describe('PaginaDeErro', () => {
  it('uma tela que quebra mostra o aviso em portugues, sem o detalhe tecnico, com recarregar e voltar ao inicio', () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
    const roteador = createMemoryRouter(
      [{ path: '/', element: <Quebra />, errorElement: <PaginaDeErro /> }],
      { initialEntries: ['/'] },
    )
    render(<RouterProvider router={roteador} />)
    expect(screen.getByRole('alert')).toHaveTextContent('Algo deu errado.')
    expect(screen.getByText(/Tente recarregar a página/)).toBeInTheDocument()
    expect(screen.queryByText(/SELECT/)).toBeNull()
    expect(screen.getByRole('button', { name: 'Recarregar a página' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Ir para o início' })).toHaveAttribute('href', '/')
  })

  it('o app inteiro e cada familia do portal tem a pagina de erro (a familia mantem o menu em volta)', () => {
    const raiz = criarRotas()[0]!
    expect(raiz.errorElement).toBeDefined()
    const portal = raiz
      .children!.find((r) => r.children?.some((c) => c.path === '/'))!
      .children!.find((r) => r.element !== undefined && r.path === undefined)!
    const familias = portal.children!.filter((r) =>
      ['/rh', '/gestor', '/eu'].includes(r.path ?? ''),
    )
    expect(familias).toHaveLength(3)
    for (const familia of familias) expect(familia.errorElement).toBeDefined()
  })
})
