import { $api } from '../shared/api/api'

/** Nome da empresa do menu; sem empresa configurada (404) ou com erro, null e nenhuma mensagem. */
export function useEmpresa(): string | null {
  const consulta = $api.useQuery(
    'get',
    '/api/eu/empresa',
    {},
    { staleTime: 5 * 60_000, retry: false },
  )
  return consulta.data?.nomeEmpresa ?? null
}
