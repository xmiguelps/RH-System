import { Outlet } from 'react-router'
import { useAuth } from '../features/autenticacao/useAuth'
import type { Perfil } from '../shared/api/tipos'
import { Pagina403 } from './paginas/Pagina403'

/** Perfil errado vê o 403 no lugar do conteúdo; o endereço e o menu continuam. */
export function GuardaDePerfil({ perfis }: { perfis: readonly Perfil[] }) {
  const { usuario } = useAuth()
  if (!usuario || !perfis.includes(usuario.perfil)) return <Pagina403 />
  return <Outlet />
}
