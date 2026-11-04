import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { $api } from '../../shared/api/api'
import type { ClienteApi } from '../../shared/api/clienteApi'
import { criarQueryClient } from '../../shared/api/consultas'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import type { Sessao } from '../../shared/api/tipos'
import { exemplos } from '../../shared/mocks/gerado/handlers'
import { servidor } from '../../shared/mocks/servidor'
import { tokenDeAcesso } from './armazenamentoDaSessao'
import { AuthProvider } from './AuthProvider'
import { useAuth } from './useAuth'

const sessaoBase = exemplos.entrar.corpo as Sessao
const NOME_EMPRESA = (exemplos.obterEmpresa.corpo as { nomeEmpresa: string }).nomeEmpresa

function sessaoCom(token: string, refresh: string): Sessao {
  return { ...sessaoBase, tokenDeAcesso: token, refreshToken: refresh }
}

function clienteFalso() {
  return {
    entrar: vi.fn<ClienteApi['entrar']>(),
    renovar: vi.fn<ClienteApi['renovar']>(),
    sair: vi.fn<ClienteApi['sair']>(),
    eu: vi.fn<ClienteApi['eu']>(),
  }
}

function Empresa({ id }: { id: number }) {
  const { usuario } = useAuth()
  const q = $api.useQuery('get', '/api/eu/empresa', {}, { enabled: !!usuario, retry: false })
  if (q.error) return <p>erro {id}</p>
  return <p>{q.data ? `${q.data.nomeEmpresa} ${id}` : `carregando ${id}`}</p>
}

let chamadasDeNegocio = 0

function empresaQueExigeTokenNovo() {
  chamadasDeNegocio = 0
  servidor.use(
    http.get('*/api/eu/empresa', ({ request }) => {
      chamadasDeNegocio += 1
      if (request.headers.get('authorization') === 'Bearer tok-novo') {
        return HttpResponse.json(exemplos.obterEmpresa.corpo as Record<string, unknown>)
      }
      return HttpResponse.json(
        { title: 'Não autenticado.', status: 401 },
        { status: 401, headers: { 'content-type': 'application/problem+json' } },
      )
    }),
  )
}

function montar(cliente: ReturnType<typeof clienteFalso>, quantas = 1) {
  return render(
    <QueryClientProvider client={criarQueryClient()}>
      <AuthProvider cliente={cliente}>
        {Array.from({ length: quantas }, (_, i) => (
          <Empresa key={i} id={i} />
        ))}
      </AuthProvider>
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  localStorage.clear()
  tokenDeAcesso.definir(null)
})

describe('AuthProvider com $api: renovacao quando a chamada de negocio recebe 401', () => {
  it('chamada de negocio com 401 renova pela API e repete', async () => {
    empresaQueExigeTokenNovo()
    localStorage.setItem('rh.refresh', 'r-1')
    const cliente = clienteFalso()
    cliente.renovar
      .mockResolvedValueOnce(sessaoCom('tok-velho', 'r-2'))
      .mockResolvedValueOnce(sessaoCom('tok-novo', 'r-3'))
    montar(cliente)
    expect(await screen.findByText(`${NOME_EMPRESA} 0`)).toBeInTheDocument()
    expect(cliente.renovar).toHaveBeenCalledTimes(2)
    expect(cliente.renovar).toHaveBeenLastCalledWith('r-2')
  })

  it('tres chamadas simultaneas com 401 renovam uma unica vez (single-flight)', async () => {
    empresaQueExigeTokenNovo()
    localStorage.setItem('rh.refresh', 'r-1')
    const cliente = clienteFalso()
    cliente.renovar
      .mockResolvedValueOnce(sessaoCom('tok-velho', 'r-2'))
      .mockImplementationOnce(
        () =>
          new Promise((resolver) => setTimeout(() => resolver(sessaoCom('tok-novo', 'r-3')), 20)),
      )
    render(
      <QueryClientProvider client={criarQueryClient()}>
        <AuthProvider cliente={cliente}>
          <Tres />
        </AuthProvider>
      </QueryClientProvider>,
    )
    expect(await screen.findByText(`a ${NOME_EMPRESA}`)).toBeInTheDocument()
    expect(await screen.findByText(`b ${NOME_EMPRESA}`)).toBeInTheDocument()
    expect(await screen.findByText(`c ${NOME_EMPRESA}`)).toBeInTheDocument()
    // uma renovação ao montar (sessão guardada) e uma única para as três chamadas com 401
    expect(cliente.renovar).toHaveBeenCalledTimes(2)
  })

  it('renovacao que falha (401 no renovar) limpa a sessao e o usuario fica null, sem laco', async () => {
    empresaQueExigeTokenNovo()
    localStorage.setItem('rh.refresh', 'r-1')
    const cliente = clienteFalso()
    cliente.renovar
      .mockResolvedValueOnce(sessaoCom('tok-velho', 'r-2'))
      .mockRejectedValue(new ErroDeApi(401, 'Não autenticado.'))
    montar(cliente)
    await waitFor(() => expect(localStorage.getItem('rh.refresh')).toBeNull())
    await new Promise((resolver) => setTimeout(resolver, 30))
    expect(chamadasDeNegocio).toBeLessThanOrEqual(2)
    expect(tokenDeAcesso.obter()).toBeNull()
  })
})

function Tres() {
  const { usuario } = useAuth()
  const opcoes = { enabled: !!usuario, retry: false }
  const a = $api.useQuery('get', '/api/eu/empresa', { params: { query: undefined } }, opcoes)
  const b = $api.useQuery('get', '/api/eu/empresa', { headers: { 'x-b': '1' } }, opcoes)
  const c = $api.useQuery('get', '/api/eu/empresa', { headers: { 'x-c': '1' } }, opcoes)
  return (
    <>
      <p>a {a.data?.nomeEmpresa ?? ''}</p>
      <p>b {b.data?.nomeEmpresa ?? ''}</p>
      <p>c {c.data?.nomeEmpresa ?? ''}</p>
    </>
  )
}
