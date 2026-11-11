import { useContext } from 'react'
import { ContextoDeAutenticacao, type ValorDoContexto } from './contextoDeAutenticacao'

export function useAuth(): ValorDoContexto {
  const valor = useContext(ContextoDeAutenticacao)
  if (!valor) throw new Error('useAuth só funciona dentro de <AuthProvider>.')
  return valor
}
