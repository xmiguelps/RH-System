import { QueryClient } from '@tanstack/react-query'
import { ErroDeApi } from './erroDeApi'

function ehErroDoCliente(erro: unknown): boolean {
  return erro instanceof ErroDeApi && erro.status >= 400 && erro.status < 500
}

export function criarQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        // De propósito: quem precisa de dado novo chama invalidarRota à mão.
        refetchOnWindowFocus: false,
        retry: (tentativas, erro) => !ehErroDoCliente(erro) && tentativas < 2,
      },
      mutations: { retry: false },
    },
  })
}
