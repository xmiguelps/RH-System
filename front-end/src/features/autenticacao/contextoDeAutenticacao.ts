import { createContext } from 'react'
import type { Usuario } from '../../shared/api/tipos'

export type ValorDoContexto = {
  usuario: Usuario | null
  carregando: boolean
  entrar(email: string, senha: string): Promise<void>
  sair(): Promise<void>
}

export const ContextoDeAutenticacao = createContext<ValorDoContexto | null>(null)
