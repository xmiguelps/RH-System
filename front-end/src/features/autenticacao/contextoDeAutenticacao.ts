import { createContext } from 'react'
import type { Usuario } from '../../shared/api/tipos'

export type ValorDoContexto = {
  usuario: Usuario | null
  carregando: boolean
  /** A renovação da sessão falhou por rede ou servidor (não por 401): o refresh guardado ainda pode valer. */
  falhaDeConexao: boolean
  /** A sessão acabou porque a pessoa clicou em Sair (e não por vencer): a entrada não guarda a tela de onde ela saiu. */
  encerradaPelaPessoa: boolean
  tentarDeNovo(): Promise<void>
  entrar(email: string, senha: string): Promise<void>
  sair(): Promise<void>
}

export const ContextoDeAutenticacao = createContext<ValorDoContexto | null>(null)
