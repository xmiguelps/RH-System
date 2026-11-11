import { useAuth } from '../../features/autenticacao/useAuth'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { inicioDoPerfil } from '../caminhos'

export function Pagina403() {
  const { usuario } = useAuth()
  return (
    <EstadoVazio
      titulo="Você não tem permissão para ver esta página."
      acao={
        <BotaoLink to={usuario ? inicioDoPerfil(usuario.perfil) : '/'} variante="secundario">
          Ir para o início
        </BotaoLink>
      }
    />
  )
}
