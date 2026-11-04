import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { useAuth } from '../features/autenticacao/useAuth'

// Sem sessão, nenhum dado da pessoa anterior pode ficar no cache para a próxima no mesmo navegador.
export function LimparCacheAoSair() {
  const { usuario } = useAuth()
  const queryClient = useQueryClient()
  useEffect(() => {
    if (!usuario) queryClient.clear()
  }, [usuario, queryClient])
  return null
}
