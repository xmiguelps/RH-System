import { Navigate, Outlet, useLocation } from 'react-router'
import { useAuth } from '../features/autenticacao/useAuth'

export function GuardaDeSessao() {
  const { usuario } = useAuth()
  const { pathname, search } = useLocation()
  if (!usuario) {
    return <Navigate to={`/entrar?voltar=${encodeURIComponent(pathname + search)}`} replace />
  }
  return <Outlet />
}
