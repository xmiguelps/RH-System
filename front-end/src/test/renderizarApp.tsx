import { QueryClient } from '@tanstack/react-query'
import { render, type RenderResult } from '@testing-library/react'
import { createMemoryRouter } from 'react-router'
import { App } from '../app/App'
import { Provedores } from '../app/Provedores'
import { criarRotas } from '../app/rotas'
import { criarClienteApi } from '../shared/api/clienteApi'

export function renderizarApp(
  opcoes: { rota?: string; queryClient?: QueryClient } = {},
): RenderResult & { roteador: ReturnType<typeof createMemoryRouter> } {
  const roteador = createMemoryRouter(criarRotas(), { initialEntries: [opcoes.rota ?? '/'] })
  const queryClient =
    opcoes.queryClient ?? new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const resultado = render(
    <Provedores queryClient={queryClient} clienteApi={criarClienteApi('https://api.exemplo.test')}>
      <App roteador={roteador} />
    </Provedores>,
  )
  return { ...resultado, roteador }
}
