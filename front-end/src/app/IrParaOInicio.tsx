import { Navigate } from 'react-router'
import { useAuth } from '../features/autenticacao/useAuth'
import { inicioDoPerfil } from './caminhos'

export function IrParaOInicio() {
  const { usuario } = useAuth()
  return <Navigate to={usuario ? inicioDoPerfil(usuario.perfil) : '/entrar'} replace />
}
