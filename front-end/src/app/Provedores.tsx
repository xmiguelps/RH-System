import { CSPProvider } from '@base-ui/react/csp-provider'
import { QueryClientProvider, type QueryClient } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'
import { AuthProvider } from '../features/autenticacao/AuthProvider'
import { criarClienteApi, type ClienteApi } from '../shared/api/clienteApi'
import { URL_DA_API } from '../shared/api/config'
import { criarQueryClient } from '../shared/api/consultas'
import { NotificacoesProvider } from '../shared/ui/NotificacoesProvider'
import { LimparCacheAoSair } from './LimparCacheAoSair'

export function Provedores({
  children,
  queryClient,
  clienteApi,
}: {
  children: ReactNode
  queryClient?: QueryClient
  clienteApi?: ClienteApi
}) {
  const [consultas] = useState(() => queryClient ?? criarQueryClient())
  const [cliente] = useState(() => clienteApi ?? criarClienteApi(URL_DA_API))
  return (
    <CSPProvider disableStyleElements>
      <QueryClientProvider client={consultas}>
        <NotificacoesProvider>
          <AuthProvider cliente={cliente}>
            <LimparCacheAoSair />
            {children}
          </AuthProvider>
        </NotificacoesProvider>
      </QueryClientProvider>
    </CSPProvider>
  )
}
