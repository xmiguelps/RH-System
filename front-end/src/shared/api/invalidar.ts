import type { QueryClient } from '@tanstack/react-query'

/** Invalida toda consulta do $api cujo caminho (queryKey[1]) começa com o prefixo. */
export function invalidarRota(queryClient: QueryClient, prefixoDoCaminho: string): Promise<void> {
  return queryClient.invalidateQueries({
    predicate: (consulta) => {
      const caminho = consulta.queryKey[1]
      return typeof caminho === 'string' && caminho.startsWith(prefixoDoCaminho)
    },
  })
}
