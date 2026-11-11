import { useCallback } from 'react'
import { useLocation, useNavigate, type To } from 'react-router'

function semAba(search: string, extras?: Record<string, string>): string {
  const parametros = new URLSearchParams(search)
  parametros.delete('aba')
  for (const [chave, valor] of Object.entries(extras ?? {})) parametros.set(chave, valor)
  const texto = parametros.toString()
  return texto ? `?${texto}` : ''
}

/** Painel ou janela sobre uma lista é rota filha: abrir e fechar mantêm os filtros da lista. */
export function usePainelSobreLista(): {
  paraPainel(destino: string, parametros?: Record<string, string>): To
  fechar(estado?: unknown): void
} {
  const { search } = useLocation()
  const navegar = useNavigate()
  const paraPainel = useCallback(
    (destino: string, parametros?: Record<string, string>): To => ({
      pathname: destino,
      search: semAba(search, parametros),
    }),
    [search],
  )
  const fechar = useCallback(
    (estado?: unknown) => navegar({ pathname: '..', search: semAba(search) }, { state: estado }),
    [navegar, search],
  )
  return { paraPainel, fechar }
}
