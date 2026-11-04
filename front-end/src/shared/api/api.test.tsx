import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it } from 'vitest'
import { tokenDeAcesso } from '../../features/autenticacao/armazenamentoDaSessao'
import { exemplos } from '../mocks/gerado/handlers'
import { servidor } from '../mocks/servidor'
import { $api } from './api'
import { criarQueryClient } from './consultas'
import { ErroDeApi } from './erroDeApi'

afterEach(() => {
  tokenDeAcesso.definir(null)
})

function Empresa() {
  const q = $api.useQuery('get', '/api/eu/empresa')
  if (q.error) return <p>erro {q.error.status}</p>
  return <p>{q.data?.nomeEmpresa ?? 'carregando'}</p>
}

describe('$api', () => {
  it('$api.useQuery mostra o example do contrato com o token de acesso e sem retry em 4xx', async () => {
    let autorizacao: string | null = null
    servidor.use(
      http.get('*/api/eu/empresa', ({ request }) => {
        autorizacao = request.headers.get('authorization')
        return HttpResponse.json(exemplos.obterEmpresa.corpo as Record<string, unknown>)
      }),
    )
    tokenDeAcesso.definir('tok-1')
    render(
      <QueryClientProvider client={criarQueryClient()}>
        <Empresa />
      </QueryClientProvider>,
    )
    const nome = (exemplos.obterEmpresa.corpo as { nomeEmpresa: string }).nomeEmpresa
    expect(await screen.findByText(nome)).toBeInTheDocument()
    expect(autorizacao).toBe('Bearer tok-1')

    let chamadas = 0
    servidor.use(
      http.get('*/api/eu/empresa', () => {
        chamadas += 1
        return HttpResponse.json(
          { title: 'Não encontrado.', status: 404 },
          { status: 404, headers: { 'content-type': 'application/problem+json' } },
        )
      }),
    )
    render(
      <QueryClientProvider client={criarQueryClient()}>
        <Empresa />
      </QueryClientProvider>,
    )
    expect(await screen.findByText('erro 404')).toBeInTheDocument()
    expect(chamadas).toBe(1)
  })

  it('criarQueryClient nao repete 404 e repete 500 no maximo duas vezes', () => {
    const cliente = criarQueryClient()
    const retry = cliente.getDefaultOptions().queries?.retry as (n: number, e: unknown) => boolean
    expect(retry(0, new ErroDeApi(404, 'x'))).toBe(false)
    expect(retry(0, new ErroDeApi(401, 'x'))).toBe(false)
    expect(retry(0, new ErroDeApi(500, 'x'))).toBe(true)
    expect(retry(1, new ErroDeApi(500, 'x'))).toBe(true)
    expect(retry(2, new ErroDeApi(500, 'x'))).toBe(false)
    expect(cliente.getDefaultOptions().queries?.refetchOnWindowFocus).toBe(false)
    expect(cliente.getDefaultOptions().mutations?.retry).toBe(false)
  })

  it('a consulta com erro chega ao componente como ErroDeApi', async () => {
    servidor.use(
      http.get('*/api/eu/empresa', () =>
        HttpResponse.json(
          { title: 'Proibido.', status: 403 },
          { status: 403, headers: { 'content-type': 'application/problem+json' } },
        ),
      ),
    )
    render(
      <QueryClientProvider client={criarQueryClient()}>
        <Empresa />
      </QueryClientProvider>,
    )
    await waitFor(() => expect(screen.getByText('erro 403')).toBeInTheDocument())
  })
})
