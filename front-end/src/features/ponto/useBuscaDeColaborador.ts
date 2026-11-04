import { useEffect, useRef, useState } from 'react'
import { $api } from '../../shared/api/api'
import type { OpcaoDoCombo } from '../../shared/ui/ComboDeBusca'

/** Busca de colaborador ativo para o espelho do RH (espera 300 ms depois da última tecla). */
export function useBuscaDeColaborador(atual?: { id: number; nome: string }): {
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
  const opcoes: OpcaoDoCombo[] = atual ? [{ valor: String(atual.id), rotulo: atual.nome }] : []
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
