import { Navigate, useSearchParams } from 'react-router'
import { PaginaEntrar } from '../features/autenticacao/PaginaEntrar'
import { useAuth } from '../features/autenticacao/useAuth'
import { destinoSeguro, inicioDoPerfil } from './caminhos'

export function PaginaDeEntrada() {
  const { usuario } = useAuth()
  const [parametros] = useSearchParams()
  if (usuario) {
    const destino = destinoSeguro(parametros.get('voltar')) ?? inicioDoPerfil(usuario.perfil)
    return <Navigate to={destino} replace />
  }
  return <PaginaEntrar />
}
