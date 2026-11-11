import createClient, { type Client } from 'openapi-fetch'
import { ErroDeApi } from './erroDeApi'
import type { paths } from './gerado/schema'
import { ehSessao, ehUsuarioDoContrato, type Sessao, type Usuario } from './tipos'

export type ClienteHttp = Client<paths>

type OpcoesDoClienteHttp = {
  urlBase: string
  obterToken?: () => string | null
  fetchImpl?: typeof fetch
  aoExpirarAcesso?: () => Promise<string | null>
}

export function criarClienteHttp(opcoes: OpcoesDoClienteHttp): ClienteHttp {
  // Por função: o MSW troca o fetch global ao ligar, e um fetch capturado antes escaparia dele.
  const buscar = (req: Request) => (opcoes.fetchImpl ?? globalThis.fetch)(req)
  const cliente = createClient<paths>({ baseUrl: opcoes.urlBase, fetch: buscar })
  // Cópia de cada requisição com token, para repetir (corpo incluído) depois de renovar o acesso.
  const copias = new Map<string, Request>()
  cliente.use({
    onRequest({ request, id }) {
      const token = opcoes.obterToken?.()
      if (token) request.headers.set('Authorization', `Bearer ${token}`)
      if (opcoes.aoExpirarAcesso && request.headers.has('Authorization')) {
        copias.set(id, request.clone())
      }
      return request
    },
    async onResponse({ response, id }) {
      const copia = copias.get(id)
      copias.delete(id)
      if (response.status === 401 && copia && opcoes.aoExpirarAcesso) {
        const novoToken = await opcoes.aoExpirarAcesso()
        if (novoToken) {
          // Uma repetição só, pelo fetch direto (fora dos middlewares): nunca vira laço.
          copia.headers.set('Authorization', `Bearer ${novoToken}`)
          const repeticao = await buscar(copia)
          if (!repeticao.ok) throw await ErroDeApi.deResposta(repeticao)
          return repeticao
        }
      }
      if (!response.ok) throw await ErroDeApi.deResposta(response)
      return response
    },
    onError({ error }) {
      return error instanceof ErroDeApi ? error : ErroDeApi.semConexao(error)
    },
  })
  return cliente
}

export type ClienteApi = {
  entrar(email: string, senha: string): Promise<Sessao>
  renovar(refreshToken: string): Promise<Sessao>
  sair(refreshToken: string): Promise<void>
  eu(tokenDeAcesso: string): Promise<Usuario>
}

function exigirSessao(dado: unknown): Sessao {
  if (!ehSessao(dado)) throw new ErroDeApi(500, 'Resposta inesperada da API.')
  return dado
}

export function criarClienteApi(urlBase: string, fetchImpl?: typeof fetch): ClienteApi {
  const cliente = criarClienteHttp({ urlBase, fetchImpl })
  return {
    async entrar(email, senha) {
      const { data } = await cliente.POST('/api/auth/entrar', { body: { email, senha } })
      return exigirSessao(data)
    },
    async renovar(refreshToken) {
      const { data } = await cliente.POST('/api/auth/renovar', { body: { refreshToken } })
      return exigirSessao(data)
    },
    async sair(refreshToken) {
      await cliente.POST('/api/auth/sair', { body: { refreshToken } })
    },
    async eu(tokenDeAcesso) {
      const { data } = await cliente.GET('/api/auth/eu', {
        headers: { Authorization: `Bearer ${tokenDeAcesso}` },
      })
      if (!ehUsuarioDoContrato(data)) throw new ErroDeApi(500, 'Resposta inesperada da API.')
      return data
    },
  }
}
