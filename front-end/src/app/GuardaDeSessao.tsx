import { Navigate, Outlet, useLocation } from 'react-router'
import { useAuth } from '../features/autenticacao/useAuth'

export function GuardaDeSessao() {
  const { usuario, encerradaPelaPessoa } = useAuth()
  const { pathname, search } = useLocation()
  if (!usuario) {
    // Sessão vencida: depois de entrar a pessoa volta para onde estava. Sair de propósito não deixa a tela
    // anterior como destino de quem entrar em seguida neste navegador.
    const destino = encerradaPelaPessoa
      ? '/entrar'
      : `/entrar?voltar=${encodeURIComponent(pathname + search)}`
    return <Navigate to={destino} replace />
  }
  return <Outlet />
}
