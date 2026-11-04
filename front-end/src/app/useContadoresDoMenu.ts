import type { QueryClient } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
import { useLocation } from 'react-router'
import { $api } from '../shared/api/api'
import { invalidarRota } from '../shared/api/invalidar'
import type { Perfil } from '../shared/api/tipos'
import type { ChaveDeContador } from './menu'

const A_CADA_MINUTO = 60_000

/** As bolinhas do menu: refazem ao mudar de página e a cada 60 s. Erro nunca mostra mensagem. */
export function useContadoresDoMenu(perfil: Perfil): Partial<Record<ChaveDeContador, number>> {
  const { pathname } = useLocation()
  const opcoes = { refetchInterval: A_CADA_MINUTO, retry: false } as const
  const doFuncionario = $api.useQuery('get', '/api/eu/contadores-do-menu', {}, opcoes)
  const doGestor = $api.useQuery(
    'get',
    '/api/gestor/contadores-do-menu',
    {},
    { ...opcoes, enabled: perfil === 'Gestor' },
  )
  const doRh = $api.useQuery(
    'get',
    '/api/rh/contadores-do-menu',
    {},
    { ...opcoes, enabled: perfil === 'RH' },
  )

  const refazerFuncionario = doFuncionario.refetch
  const refazerGestor = doGestor.refetch
  const refazerRh = doRh.refetch
  const caminhoAnterior = useRef(pathname)
  useEffect(() => {
    if (caminhoAnterior.current === pathname) return
    caminhoAnterior.current = pathname
    void refazerFuncionario()
    if (perfil === 'Gestor') void refazerGestor()
    if (perfil === 'RH') void refazerRh()
  }, [pathname, perfil, refazerFuncionario, refazerGestor, refazerRh])

  return {
    ...(doFuncionario.data ?? {}),
    ...(perfil === 'Gestor' ? (doGestor.data ?? {}) : {}),
    ...(perfil === 'RH' ? (doRh.data ?? {}) : {}),
  }
}

export async function invalidarContadoresDoMenu(queryClient: QueryClient): Promise<void> {
  await Promise.all([
    invalidarRota(queryClient, '/api/eu/contadores-do-menu'),
    invalidarRota(queryClient, '/api/gestor/contadores-do-menu'),
    invalidarRota(queryClient, '/api/rh/contadores-do-menu'),
  ])
}
