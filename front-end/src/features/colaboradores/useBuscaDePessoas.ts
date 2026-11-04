import { useEffect, useRef, useState } from 'react'
import { $api } from '../../shared/api/api'
import type { OpcaoDoCombo } from '../../shared/ui/ComboDeBusca'

export const SEM_GESTOR = 'SEM_GESTOR'

/** Busca de pessoas ativas para escolher um gestor (espera 300 ms depois da última tecla). */
export function useBuscaDePessoas(gestorAtual?: { funcionarioId: number; nome: string }): {
  opcoes: readonly OpcaoDoCombo[]
  aoDigitar(texto: string): void
  carregando: boolean
} {
  const [busca, setBusca] = useState('')
  const espera = useRef<ReturnType<typeof setTimeout> | null>(null)
  useEffect(
    () => () => {
      if (espera.current) clearTimeout(espera.current)
    },
    [],
  )
  const consulta = $api.useQuery(
    'get',
    '/api/rh/funcionarios',
    {
      params: {
        query: { pagina: 1, tamanhoPagina: 20, situacao: 'ATIVO', busca: busca || undefined },
      },
    },
    { enabled: busca.length > 0 },
  )

  const opcoes: OpcaoDoCombo[] = [{ valor: SEM_GESTOR, rotulo: 'Sem gestor' }]
  if (gestorAtual) {
    opcoes.push({ valor: String(gestorAtual.funcionarioId), rotulo: gestorAtual.nome })
  }
  for (const pessoa of consulta.data?.itens ?? []) {
    if (opcoes.some((o) => o.valor === String(pessoa.id))) continue
    opcoes.push({ valor: String(pessoa.id), rotulo: pessoa.nome, apoio: pessoa.cargo ?? undefined })
  }

  return {
    opcoes,
    aoDigitar(texto) {
      if (espera.current) clearTimeout(espera.current)
      espera.current = setTimeout(() => setBusca(texto.trim().slice(0, 100)), 300)
    },
    carregando: consulta.isFetching,
  }
}
