import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import createClient from 'openapi-react-query'
import type { ReactNode } from 'react'
import { describe, expect, it } from 'vitest'
import { servidor } from '../mocks/servidor'
import { criarClienteHttp } from './clienteApi'
import { ErroDeApi } from './erroDeApi'

const $api = createClient(criarClienteHttp({ urlBase: 'https://api.exemplo.test' }))

function comConsultas(filho: ReactNode) {
  const consultas = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={consultas}>{filho}</QueryClientProvider>
}

function Eu() {
  const q = $api.useQuery('get', '/api/auth/eu', {}, { retry: false })
  if (q.error) {
    const erro = q.error as unknown
    return (
      <p>
        erro {erro instanceof ErroDeApi ? 'ErroDeApi' : 'outro'} {q.error.status} {q.error.title}
      </p>
    )
  }
  return <p>{q.data?.nome ?? 'carregando'}</p>
}

describe('hooks do openapi-react-query sobre criarClienteHttp', () => {
  it('useQuery mostra o example do contrato', async () => {
    render(comConsultas(<Eu />))
    expect(await screen.findByText('Ana Souza')).toBeInTheDocument()
  })

  it('useQuery recebe ErroDeApi quando a API devolve problem+json', async () => {
    servidor.use(
      http.get('*/api/auth/eu', () =>
        HttpResponse.json(
          { title: 'Não autenticado.', status: 401 },
          { status: 401, headers: { 'content-type': 'application/problem+json' } },
        ),
      ),
    )
    render(comConsultas(<Eu />))
    await waitFor(() =>
      expect(screen.getByText('erro ErroDeApi 401 Não autenticado.')).toBeInTheDocument(),
    )
  })
})
