import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { exemplos } from '../mocks/gerado/handlers'
import { servidor } from '../mocks/servidor'
import { criarClienteHttp } from './clienteApi'
import { ErroDeApi } from './erroDeApi'

const URL_BASE = 'https://api.exemplo.test'
const NAO_AUTENTICADO = () =>
  HttpResponse.json(
    { title: 'Não autenticado.', status: 401 },
    { status: 401, headers: { 'content-type': 'application/problem+json' } },
  )

function empresaPorToken(contador: { n: number }) {
  return http.get('*/api/eu/empresa', ({ request }) => {
    contador.n += 1
    if (request.headers.get('authorization') === 'Bearer novo') {
      return HttpResponse.json(exemplos.obterEmpresa.corpo as Record<string, unknown>)
    }
    return NAO_AUTENTICADO()
  })
}

describe('renovacao do token quando uma chamada recebe 401', () => {
  it('401 com token renova e repete a chamada uma vez com o token novo', async () => {
    const contador = { n: 0 }
    servidor.use(empresaPorToken(contador))
    let token = 'velho'
    const aoExpirarAcesso = vi.fn(async () => {
      token = 'novo'
      return token
    })
    const cliente = criarClienteHttp({
      urlBase: URL_BASE,
      obterToken: () => token,
      aoExpirarAcesso,
    })
    const { data, response } = await cliente.GET('/api/eu/empresa')
    expect(response.status).toBe(200)
    expect(data).toEqual(exemplos.obterEmpresa.corpo)
    expect(aoExpirarAcesso).toHaveBeenCalledTimes(1)
    expect(contador.n).toBe(2)
  })

  it('o corpo de um POST e reenviado na repeticao', async () => {
    const corpos: unknown[] = []
    servidor.use(
      http.post('*/api/auth/sair', async ({ request }) => {
        corpos.push(await request.json())
        if (request.headers.get('authorization') === 'Bearer novo') {
          return new HttpResponse(null, { status: 204 })
        }
        return NAO_AUTENTICADO()
      }),
    )
    const cliente = criarClienteHttp({
      urlBase: URL_BASE,
      obterToken: () => 'velho',
      aoExpirarAcesso: async () => 'novo',
    })
    const { response } = await cliente.POST('/api/auth/sair', { body: { refreshToken: 'r1' } })
    expect(response.status).toBe(204)
    expect(corpos).toEqual([{ refreshToken: 'r1' }, { refreshToken: 'r1' }])
  })

  it('aoExpirarAcesso devolvendo null lanca o ErroDeApi 401 sem repetir', async () => {
    const contador = { n: 0 }
    servidor.use(empresaPorToken(contador))
    const cliente = criarClienteHttp({
      urlBase: URL_BASE,
      obterToken: () => 'velho',
      aoExpirarAcesso: async () => null,
    })
    await expect(cliente.GET('/api/eu/empresa')).rejects.toMatchObject({ status: 401 })
    expect(contador.n).toBe(1)
  })

  it('repeticao que volta 401 de novo lanca o 401 e nao renova outra vez (Review Focus 1)', async () => {
    let chamadas = 0
    servidor.use(
      http.get('*/api/eu/empresa', () => {
        chamadas += 1
        return NAO_AUTENTICADO()
      }),
    )
    const aoExpirarAcesso = vi.fn(async () => 'novo')
    const cliente = criarClienteHttp({
      urlBase: URL_BASE,
      obterToken: () => 'velho',
      aoExpirarAcesso,
    })
    const erro = await cliente.GET('/api/eu/empresa').catch((e: unknown) => e)
    expect(erro).toBeInstanceOf(ErroDeApi)
    expect((erro as ErroDeApi).status).toBe(401)
    expect(aoExpirarAcesso).toHaveBeenCalledTimes(1)
    expect(chamadas).toBe(2)
  })

  it('401 sem Authorization (entrar com senha errada) nao chama aoExpirarAcesso', async () => {
    servidor.use(http.post('*/api/auth/entrar', () => NAO_AUTENTICADO()))
    const aoExpirarAcesso = vi.fn(async () => 'novo')
    const cliente = criarClienteHttp({ urlBase: URL_BASE, obterToken: () => null, aoExpirarAcesso })
    await expect(
      cliente.POST('/api/auth/entrar', { body: { email: 'a@b.c', senha: 'x' } }),
    ).rejects.toMatchObject({ status: 401 })
    expect(aoExpirarAcesso).not.toHaveBeenCalled()
  })

  it('duas chamadas simultaneas com 401 sao repetidas as duas', async () => {
    const contador = { n: 0 }
    servidor.use(empresaPorToken(contador))
    let token = 'velho'
    let promessa: Promise<string> | null = null
    const aoExpirarAcesso = vi.fn(() => {
      promessa ??= new Promise<string>((resolver) =>
        setTimeout(() => {
          token = 'novo'
          resolver(token)
        }, 10),
      )
      return promessa
    })
    const cliente = criarClienteHttp({
      urlBase: URL_BASE,
      obterToken: () => token,
      aoExpirarAcesso,
    })
    const [a, b] = await Promise.all([
      cliente.GET('/api/eu/empresa'),
      cliente.GET('/api/eu/empresa'),
    ])
    expect(a.response.status).toBe(200)
    expect(b.response.status).toBe(200)
    expect(contador.n).toBe(4)
  })
})
