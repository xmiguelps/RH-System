import type { QueryClient } from '@tanstack/react-query'
import type { RenderResult } from '@testing-library/react'
import userEvent, { type UserEvent } from '@testing-library/user-event'
import type { createMemoryRouter } from 'react-router'
import { tokenDeAcesso } from '../features/autenticacao/armazenamentoDaSessao'
import type { Perfil, Usuario } from '../shared/api/tipos'
import { renderizarApp } from './renderizarApp'
import { entrarComo } from './sessao'

/** Abre o app logado no perfil (RH por padrão) direto na rota da tela. */
export function renderizarTela(
  rota: string,
  opcoes: { perfil?: Perfil; usuario?: Partial<Usuario>; queryClient?: QueryClient } = {},
): RenderResult & { user: UserEvent; roteador: ReturnType<typeof createMemoryRouter> } {
  localStorage.clear()
  tokenDeAcesso.definir(null)
  entrarComo(opcoes.perfil ?? 'RH', opcoes.usuario)
  const user = userEvent.setup()
  const resultado = renderizarApp({ rota, queryClient: opcoes.queryClient })
  return { ...resultado, user }
}
