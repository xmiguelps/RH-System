type Extras = {
  detail?: string | null
  codigo?: string
  traceId?: string
  errors?: Record<string, string[]>
  retryAfterSegundos?: number | null
  corpo?: Record<string, unknown> | null
}

// Os nomes dos campos são os do corpo problem+json do contrato (title, detail, codigo...).
export class ErroDeApi extends Error {
  readonly status: number
  readonly title: string
  readonly detail: string | null
  readonly codigo?: string
  readonly traceId?: string
  readonly errors: Record<string, string[]>
  readonly retryAfterSegundos: number | null
  /** O problem+json inteiro (campos extras como `cadastroExistente`); null se a resposta não tinha corpo. */
  readonly corpo: Record<string, unknown> | null

  constructor(status: number, title: string, extras: Extras = {}) {
    super(title)
    this.name = 'ErroDeApi'
    this.status = status
    this.title = title
    this.detail = extras.detail ?? null
    this.codigo = extras.codigo
    this.traceId = extras.traceId
    this.errors = extras.errors ?? {}
    this.retryAfterSegundos = extras.retryAfterSegundos ?? null
    this.corpo = extras.corpo ?? null
  }

  static async deResposta(resposta: Response): Promise<ErroDeApi> {
    const retryAfter = Number.parseInt(resposta.headers.get('Retry-After') ?? '', 10)
    const retryAfterSegundos = Number.isFinite(retryAfter) ? retryAfter : null
    let corpo: Record<string, unknown> | null = null
    try {
      const texto = await resposta.clone().text()
      const lido: unknown = texto ? JSON.parse(texto) : null
      if (typeof lido === 'object' && lido !== null) corpo = lido as Record<string, unknown>
    } catch {
      corpo = null
    }
    if (!corpo) {
      return new ErroDeApi(resposta.status, `Erro ${resposta.status}`, { retryAfterSegundos })
    }
    const texto = (v: unknown) => (typeof v === 'string' ? v : undefined)
    const errors =
      typeof corpo.errors === 'object' && corpo.errors !== null
        ? (corpo.errors as Record<string, string[]>)
        : undefined
    return new ErroDeApi(resposta.status, texto(corpo.title) ?? `Erro ${resposta.status}`, {
      detail: texto(corpo.detail) ?? null,
      codigo: texto(corpo.codigo),
      traceId: texto(corpo.traceId),
      errors,
      retryAfterSegundos,
      corpo,
    })
  }

  static semConexao(causa: unknown): ErroDeApi {
    const erro = new ErroDeApi(0, 'Sem conexão com a API.')
    erro.cause = causa
    return erro
  }
}
