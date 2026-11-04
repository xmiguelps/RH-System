import type { components } from './gerado/schema'

export type Esquema<K extends keyof components['schemas']> = components['schemas'][K]
export type Perfil = Esquema<'Perfil'>
export type Usuario = Esquema<'UsuarioResposta'>
export type Sessao = Esquema<'SessaoResposta'>

const PERFIS: readonly string[] = ['Funcionario', 'Gestor', 'RH'] satisfies Perfil[]

function ehTexto(x: unknown): x is string {
  return typeof x === 'string'
}

function ehTextoOuNulo(x: unknown): boolean {
  return x === null || typeof x === 'string'
}

function ehUsuario(x: unknown): x is Usuario {
  if (typeof x !== 'object' || x === null) return false
  const u = x as Record<string, unknown>
  return (
    typeof u.id === 'number' &&
    typeof u.funcionarioId === 'number' &&
    ehTexto(u.nome) &&
    ehTexto(u.email) &&
    ehTexto(u.perfil) &&
    PERFIS.includes(u.perfil) &&
    ehTextoOuNulo(u.cargo) &&
    ehTextoOuNulo(u.setor)
  )
}

export function ehSessao(x: unknown): x is Sessao {
  if (typeof x !== 'object' || x === null) return false
  const s = x as Record<string, unknown>
  return (
    ehTexto(s.tokenDeAcesso) &&
    ehTexto(s.acessoExpiraEmUtc) &&
    ehTexto(s.refreshToken) &&
    ehUsuario(s.usuario)
  )
}

export function ehUsuarioDoContrato(x: unknown): x is Usuario {
  return ehUsuario(x)
}
