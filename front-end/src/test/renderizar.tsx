import { CSPProvider } from '@base-ui/react/csp-provider'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, type RenderResult } from '@testing-library/react'
import type { ReactElement } from 'react'
import { MemoryRouter } from 'react-router'
import { NotificacoesProvider } from '../shared/ui/NotificacoesProvider'

/** Provedores de uma tela, sem AuthProvider (a tela recebe dados pelo MSW). */
export function renderizarComProvedores(
  ui: ReactElement,
  opcoes: { rota?: string; queryClient?: QueryClient } = {},
): RenderResult {
  const queryClient =
    opcoes.queryClient ?? new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <CSPProvider disableStyleElements>
      <QueryClientProvider client={queryClient}>
        <NotificacoesProvider>
          <MemoryRouter initialEntries={[opcoes.rota ?? '/']}>{ui}</MemoryRouter>
        </NotificacoesProvider>
      </QueryClientProvider>
    </CSPProvider>,
  )
}
