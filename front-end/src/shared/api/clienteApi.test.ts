import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { exemplos } from '../mocks/gerado/handlers'
import { servidor } from '../mocks/servidor'
import { criarClienteApi, criarClienteHttp } from './clienteApi'
import { ErroDeApi } from './erroDeApi'

const URL_BASE = 'https://api.exemplo.test'

function problema(
  status: number,
  corpo: Record<string, unknown>,
  headers?: Record<string, string>,
) {
  return HttpResponse.json(corpo, {
    status,
    headers: { 'content-type': 'application/problem+json', ...headers },
  })
}

async function capturarErro(promessa: Promise<unknown>): Promise<ErroDeApi> {
  try {
    await promessa
  } catch (erro) {
    expect(erro).toBeInstanceOf(ErroDeApi)
    return erro as ErroDeApi
  }
  throw new Error('a promessa devia ter falhado')
}

describe('clienteApi', () => {
  it('clienteApi.entrar envia POST /api/auth/entrar e devolve a sessao', async () => {
    let recebido: unknown
    servidor.use(
      http.post('*/api/auth/entrar', async ({ request }) => {
        recebido = await request.json()
        return HttpResponse.json(exemplos.entrar.corpo as Record<string, unknown>)
      }),
    )
    const sessao = await criarClienteApi(URL_BASE).entrar(
      'ana.souza@empresa.com.br',
      'Senha#Forte123',
    )
    expect(recebido).toEqual({ email: 'ana.souza@empresa.com.br', senha: 'Senha#Forte123' })
    expect(sessao.usuario).toMatchObject({
      nome: 'Ana Souza',
      perfil: 'RH',
      cargo: 'Gerente de RH',
    })
  })

  it.each([401, 403, 400, 429, 500])(
    'clienteApi transforma resposta %i em ErroDeApi com o status',
    async (status) => {
      servidor.use(
        http.post('*/api/auth/entrar', () =>
          problema(status, { title: 'Titulo do erro.', status }),
        ),
      )
      const erro = await capturarErro(criarClienteApi(URL_BASE).entrar('a@b.c', 'x'))
      expect(erro).toMatchObject({ status, title: 'Titulo do erro.', message: 'Titulo do erro.' })
    },
  )

  it('clienteApi guarda detail, codigo, errors e Retry-After do problem+json', async () => {
    servidor.use(
      http.post('*/api/auth/entrar', () =>
        problema(
          429,
          {
            title: 'Muitas tentativas.',
            status: 429,
            detail: 'Aguarde.',
            codigo: 'LIMITE',
            errors: { email: ['Obrigatório.'] },
          },
          { 'Retry-After': '60' },
        ),
      ),
    )
    const erro = await capturarErro(criarClienteApi(URL_BASE).entrar('a@b.c', 'x'))
    expect(erro).toMatchObject({
      detail: 'Aguarde.',
      codigo: 'LIMITE',
      errors: { email: ['Obrigatório.'] },
      retryAfterSegundos: 60,
    })
  })

  it('clienteApi transforma falha de rede em ErroDeApi com status 0', async () => {
    servidor.use(http.post('*/api/auth/entrar', () => HttpResponse.error()))
    const erro = await capturarErro(criarClienteApi(URL_BASE).entrar('a@b.c', 'x'))
    expect(erro.status).toBe(0)
  })

  it('clienteApi transforma 502 sem corpo JSON em ErroDeApi so com o status', async () => {
    servidor.use(
      http.post('*/api/auth/entrar', () => new HttpResponse('Bad Gateway', { status: 502 })),
    )
    const erro = await capturarErro(criarClienteApi(URL_BASE).entrar('a@b.c', 'x'))
    expect(erro).toMatchObject({ status: 502, detail: null })
  })

  it('clienteApi.entrar com 200 que nao e uma sessao (defeito do back-end) lanca ErroDeApi 500', async () => {
    servidor.use(http.post('*/api/auth/entrar', () => HttpResponse.json({ token: 'x' })))
    const erro = await capturarErro(criarClienteApi(URL_BASE).entrar('a@b.c', 'x'))
    expect(erro).toMatchObject({ status: 500, title: 'Resposta inesperada da API.' })
  })

  it('clienteApi.renovar e sair usam as rotas do contrato', async () => {
    const corpos: Record<string, unknown> = {}
    servidor.use(
      http.post('*/api/auth/renovar', async ({ request }) => {
        corpos.renovar = await request.json()
        return HttpResponse.json(exemplos.renovarSessao.corpo as Record<string, unknown>)
      }),
      http.post('*/api/auth/sair', async ({ request }) => {
        corpos.sair = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const cliente = criarClienteApi(URL_BASE)
    await cliente.renovar('r1')
    await cliente.sair('r1')
    expect(corpos).toEqual({ renovar: { refreshToken: 'r1' }, sair: { refreshToken: 'r1' } })
  })

  it('clienteApi.eu envia GET /api/auth/eu com Authorization Bearer', async () => {
    let autorizacao: string | null = null
    servidor.use(
      http.get('*/api/auth/eu', ({ request }) => {
        autorizacao = request.headers.get('authorization')
        return HttpResponse.json(exemplos.obterUsuarioLogado.corpo as Record<string, unknown>)
      }),
    )
    const usuario = await criarClienteApi(URL_BASE).eu('tok-123')
    expect(autorizacao).toBe('Bearer tok-123')
    expect(usuario.nome).toBe('Ana Souza')
  })

  it('criarClienteHttp manda o token de obterToken em toda requisicao', async () => {
    let autorizacao: string | null = null
    servidor.use(
      http.get('*/api/auth/eu', ({ request }) => {
        autorizacao = request.headers.get('authorization')
        return HttpResponse.json(exemplos.obterUsuarioLogado.corpo as Record<string, unknown>)
      }),
    )
    const cliente = criarClienteHttp({ urlBase: URL_BASE, obterToken: () => 'tok-9' })
    await cliente.GET('/api/auth/eu')
    expect(autorizacao).toBe('Bearer tok-9')
  })
})
