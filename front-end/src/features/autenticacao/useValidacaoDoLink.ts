import { useQuery } from '@tanstack/react-query'
import { clienteHttpPublico } from '../../shared/api/apiPublica'
import { ErroDeApi } from '../../shared/api/erroDeApi'

/** Confere o link do e-mail sem gastá-lo. Uma vez só por token (o limite é de 5 por minuto). */
export function useValidacaoDoLink(token: string | null) {
  return useQuery({
    queryKey: ['validar-link', token],
    queryFn: async () => {
      const { data } = await clienteHttpPublico.POST('/api/auth/senha/validar-link', {
        body: { token: token ?? '' },
      })
      if (!data) throw new ErroDeApi(500, 'Resposta inesperada da API.')
      return data
    },
    retry: false,
    staleTime: Infinity,
    gcTime: 0,
    enabled: token !== null,
  })
}
