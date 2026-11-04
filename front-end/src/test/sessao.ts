import { http, HttpResponse } from 'msw'
import type { Perfil, Sessao, Usuario } from '../shared/api/tipos'
import { exemplos } from '../shared/mocks/gerado/handlers'
import { servidor } from '../shared/mocks/servidor'

// As pessoas do contrato\ELENCO.md: Ana Souza (RH), Bruno Lima (Funcionário), Carlos Ribeiro (Gestor).
const PESSOAS: Record<Perfil, Usuario> = {
  RH: {
    id: 1,
    funcionarioId: 1,
    nome: 'Ana Souza',
    email: 'ana.souza@empresa.com.br',
    perfil: 'RH',
    cargo: 'Gerente de RH',
    setor: 'Recursos Humanos',
  },
  Funcionario: {
    id: 12,
    funcionarioId: 12,
    nome: 'Bruno Lima',
    email: 'bruno.lima@empresa.com.br',
    perfil: 'Funcionario',
    cargo: 'Analista pleno',
    setor: 'Financeiro',
  },
  Gestor: {
    id: 7,
    funcionarioId: 7,
    nome: 'Carlos Ribeiro',
    email: 'carlos.ribeiro@empresa.com.br',
    perfil: 'Gestor',
    cargo: 'Gerente comercial',
    setor: 'Comercial',
  },
}

export function sessaoDeTeste(perfil: Perfil, usuario?: Partial<Usuario>): Sessao {
  const base = exemplos.entrar.corpo as Sessao
  return { ...base, usuario: { ...PESSOAS[perfil], ...usuario } }
}

/** Grava um refresh no localStorage e faz o POST renovar responder com a sessão do perfil. */
export function entrarComo(perfil: Perfil, usuario?: Partial<Usuario>): void {
  localStorage.setItem('rh.refresh', `r-${perfil}`)
  servidor.use(
    http.post('*/api/auth/renovar', () => HttpResponse.json(sessaoDeTeste(perfil, usuario))),
  )
}
