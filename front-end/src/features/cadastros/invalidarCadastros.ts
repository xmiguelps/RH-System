import type { QueryClient } from '@tanstack/react-query'
import { invalidarRota } from '../../shared/api/invalidar'

/** Depois de gravar um cadastro: a lista, os números do menu e as opções que usam a mesma rota. */
export async function invalidarCadastros(
  queryClient: QueryClient,
  ...prefixos: readonly string[]
): Promise<void> {
  await Promise.all(prefixos.map((prefixo) => invalidarRota(queryClient, prefixo)))
}
