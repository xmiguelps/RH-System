import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
import { useAuth } from '../features/autenticacao/useAuth'

// Sem sessão, nenhum dado da pessoa anterior pode ficar no cache para a próxima no mesmo navegador.
// E se outra pessoa assume a sessão sem passar por "sem sessão" (entrou em outra aba), o que estava na tela
// era da anterior: as consultas voltam ao zero e as que estão na tela buscam de novo.
export function LimparCacheAoSair() {
  const { usuario } = useAuth()
  const queryClient = useQueryClient()
  const anterior = useRef<number | null>(null)
  const id = usuario?.id ?? null
  useEffect(() => {
    if (id === null) queryClient.clear()
    else if (anterior.current !== null && anterior.current !== id) void queryClient.resetQueries()
    anterior.current = id
  }, [id, queryClient])
  return null
}
