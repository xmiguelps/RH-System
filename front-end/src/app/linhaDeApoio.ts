import type { Usuario } from '../shared/api/tipos'

/** A linha abaixo do nome no rodapé do menu (PNG 00, 32 e 46). */
export function linhaDeApoio(usuario: Usuario): string {
  if (usuario.perfil === 'RH') return 'Perfil RH, acesso total'
  if (usuario.perfil === 'Gestor') {
    return usuario.setor ? `Perfil gestor, setor ${usuario.setor}` : 'Perfil gestor'
  }
  return [usuario.cargo, usuario.setor].filter(Boolean).join(', ')
}
