import { http, HttpResponse } from 'msw'
import { exemplos } from '../shared/mocks/gerado/handlers'
import { servidor } from '../shared/mocks/servidor'
import { esperarRequisicaoConforme } from './contrato'

export type Capturada = {
  consulta: URLSearchParams
  corpo?: Record<string, unknown>
  parametrosDoCaminho: Record<string, string>
  cabecalhos: Headers
}

export type RespostaDeTeste = {
  status?: number
  corpo?: unknown
  cabecalhos?: Record<string, string>
  espera?: Promise<unknown>
}

export type Capturador = {
  todas: Capturada[]
  ultima(): Capturada
  quantas(): number
  conforme(): void
}

type Metodo = 'get' | 'post' | 'put' | 'patch' | 'delete'

async function lerCorpo(request: Request): Promise<Record<string, unknown> | undefined> {
  const tipo = request.headers.get('content-type') ?? ''
  if (tipo.includes('multipart/form-data')) {
    const formulario = await request.formData()
    const saida: Record<string, unknown> = {}
    for (const [nome, valor] of formulario.entries()) {
      saida[nome] = valor instanceof File ? valor.name : valor
    }
    return saida
  }
  const texto = await request.text()
  if (!texto) return undefined
  try {
    const lido: unknown = JSON.parse(texto)
    return typeof lido === 'object' && lido !== null ? (lido as Record<string, unknown>) : undefined
  } catch {
    return undefined
  }
}

/** Registra o handler da operação, guarda cada requisição e devolve `resposta` ou o exemplo do contrato. */
export function capturar(operationId: string, resposta?: RespostaDeTeste): Capturador {
  const exemplo = exemplos[operationId]
  if (!exemplo) throw new Error(`capturar: a operação "${operationId}" não existe no contrato`)
  const todas: Capturada[] = []
  const caminho = '*' + exemplo.caminho.replace(/\{([^}]+)\}/g, ':$1')
  const metodo = exemplo.metodo as Metodo
  // '/api/rh/avisos/{avisoId}' também casaria com '/api/rh/avisos/resumo': um caminho fixo do
  // contrato com o mesmo método nunca é desta operação (segue para o handler dele).
  const fixosDoMesmoMetodo = exemplo.caminho.includes('{')
    ? Object.values(exemplos)
        .filter((e) => e.metodo === exemplo.metodo && !e.caminho.includes('{'))
        .map((e) => e.caminho)
    : []
  servidor.use(
    http[metodo](caminho, async ({ request, params }) => {
      const { pathname } = new URL(request.url)
      if (fixosDoMesmoMetodo.some((fixo) => pathname.endsWith(fixo))) return undefined
      todas.push({
        consulta: new URL(request.url).searchParams,
        corpo: await lerCorpo(request.clone()),
        parametrosDoCaminho: Object.fromEntries(
          // o "*" do começo do padrão entra como "0": só os nomes do contrato ficam
          Object.entries(params)
            .filter(([chave]) => !/^\d+$/.test(chave))
            .map(([chave, valor]) => [chave, String(valor)]),
        ),
        cabecalhos: request.headers,
      })
      if (resposta?.espera) await resposta.espera
      const status = resposta?.status ?? exemplo.status
      const corpo = resposta && 'corpo' in resposta ? resposta.corpo : exemplo.corpo
      const cabecalhos: Record<string, string> = {
        ...(exemplo.contentType && !resposta?.status
          ? { 'content-type': exemplo.contentType }
          : {}),
        ...resposta?.cabecalhos,
      }
      if (corpo === undefined || corpo === null || status === 204) {
        return new HttpResponse(null, { status, headers: cabecalhos })
      }
      if (typeof corpo === 'string') return new HttpResponse(corpo, { status, headers: cabecalhos })
      return HttpResponse.json(corpo as Record<string, unknown>, { status, headers: cabecalhos })
    }),
  )
  return {
    todas,
    ultima() {
      const ultima = todas[todas.length - 1]
      if (!ultima) throw new Error(`capturar: nenhuma requisição de "${operationId}" chegou`)
      return ultima
    },
    quantas: () => todas.length,
    conforme() {
      const { consulta, corpo } = this.ultima()
      esperarRequisicaoConforme(operationId, { consulta, corpo })
    },
  }
}

/** Corpo problem+json (aceita o exemplo inteiro de exemploDoContrato como `campos`). */
export function problema(
  status: number,
  campos: {
    title?: string
    detail?: string
    codigo?: string
    errors?: Record<string, string[]>
  } & Record<string, unknown> = {},
): RespostaDeTeste {
  return {
    status,
    corpo: { title: 'Erro.', traceId: '00-teste', ...campos, status },
    cabecalhos: { 'content-type': 'application/problem+json' },
  }
}
