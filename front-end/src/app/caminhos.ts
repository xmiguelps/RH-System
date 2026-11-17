import type { Perfil } from '../shared/api/tipos'

export function inicioDoPerfil(perfil: Perfil): string {
  return perfil === 'RH' ? '/rh/painel' : '/eu/inicio'
}

/** Só aceita um caminho interno do próprio site (nada de //outro.site, esquema ou volta ao /entrar). */
export function destinoSeguro(voltar: string | null): string | null {
  if (!voltar || voltar.length >= 512) return null
  if (!voltar.startsWith('/') || voltar.startsWith('//') || voltar.startsWith('/\\')) return null
  if (/^\/[a-z][a-z0-9+.-]*:/i.test(voltar)) return null
  const caminho = voltar.split(/[?#]/)[0]
  if (caminho === '/entrar') return null
  return voltar
}

export const PERFIS_POR_FAMILIA: { rh: Perfil[]; gestor: Perfil[]; eu: Perfil[] } = {
  rh: ['RH'],
  gestor: ['Gestor'],
  eu: ['Funcionario', 'Gestor', 'RH'],
}

const FAMILIAS_DE_TELAS = ['rh', 'gestor', 'eu'] as const

/** `destinoSeguro` e, além disso, só uma tela de uma família que o perfil pode abrir (quem entra com `?voltar=/rh/painel` sendo funcionário cairia num 403). */
export function destinoDoPerfil(voltar: string | null, perfil: Perfil): string | null {
  const destino = destinoSeguro(voltar)
  if (!destino) return null
  const primeiro = destino.split(/[?#]/)[0]!.split('/')[1] ?? ''
  const familia = FAMILIAS_DE_TELAS.find((f) => f === primeiro)
  if (familia && !PERFIS_POR_FAMILIA[familia].includes(perfil)) return null
  return destino
}
