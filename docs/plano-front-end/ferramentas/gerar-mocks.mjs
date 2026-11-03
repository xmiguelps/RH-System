#!/usr/bin/env node
// gerar-mocks.mjs
//
// PARA QUE SERVE
//   Lê o contrato OpenAPI (contrato/openapi.yaml) e escreve dois arquivos TypeScript:
//   1. handlers.ts: um handler do MSW por operação. Cada handler devolve o `example` da primeira
//      resposta 2xx da operação. São os "dados simulados" do front: o desenvolvimento (npm run dev) e
//      os testes (npm test) usam estes handlers.
//   2. exemplos.ts (na mesma pasta): TODOS os exemplos de cada resposta de cada operação, erros
//      incluídos (o 409 `cpfInativo`, os `examples` nomeados...). Quem lê é o ajudante de teste
//      `exemploDoContrato` (src/shared/teste), para o teste carregar o exemplo pelo nome em vez de
//      copiar o texto do contrato.
//   Nada aqui é escrito à mão por rota: tudo vem do contrato. Se o contrato muda, rode de novo.
//
// COMO RODA
//   Da raiz do repositório, no PowerShell:   npm --prefix front-end run contrato:mocks
//   (o package.json chama: node scripts/gerar-mocks.mjs ../contrato/openapi.yaml src/shared/mocks/gerado/handlers.ts)
//   `npm test`, `npm run dev` e `npm run build` já rodam isto antes de começar (pretest, predev, prebuild).
//   Saída: um resumo em JSON no console. Código de saída 1 se alguma operação 2xx com corpo não tiver
//   exemplo (defeito do contrato: vira dúvida para o dono, não se contorna). Com --detalhes o resumo traz
//   também os pares de rotas que casam com a mesma URL.
//
// ONDE COPIAR
//   Para front-end\scripts\gerar-mocks.mjs (dependência: o pacote `yaml`, já no package.json do plano).
//   Os arquivos gerados (src/shared/mocks/gerado/handlers.ts e exemplos.ts) NÃO vão para o git: estão
//   no .gitignore.
//
// O QUE O handlers.ts EXPORTA
//   versaoDoContrato  texto  info.version do contrato usado (é o que o PROGRESSO.md registra por tela)
//   exemplos          mapa   operationId -> { metodo, caminho, status, contentType, corpo }
//   handlers          lista  handlers do MSW, na ordem certa (rota literal antes de rota com {parametro})
//
// O QUE O exemplos.ts EXPORTA
//   exemplosPorOperacao  mapa  operationId -> status -> nome -> corpo do exemplo.
//     O status é o texto do código ("200", "409"); respostas sem corpo (204) não entram. `example`
//     (um só) tem o nome "padrao"; `examples` nomeados mantêm o nome do contrato. A ordem é a do contrato.
//
// LIMITES CONHECIDOS (medidos em 2026-10-30 contra o contrato de 224 operações)
//   - O handler casa só por método e caminho; não olha parâmetros de consulta nem o corpo enviado.
//   - O handler sempre devolve o mesmo exemplo (o primeiro 2xx). Para outro cenário (erro, lista vazia,
//     um exemplo nomeado), o teste usa servidor.use(...) com exemploDoContrato(operationId, status, nome).
//   - O MSW usa o primeiro handler que casa; por isso a ordenação. Pares ambíguos (nenhuma das duas rotas
//     é mais específica) saem como aviso no resumo.
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { parse } from 'yaml'

const argv = process.argv.slice(2)
const detalhes = argv.includes('--detalhes')
const [entrada = '../contrato/openapi.yaml', saida = 'src/shared/mocks/gerado/handlers.ts'] =
  argv.filter((a) => !a.startsWith('--'))
const doc = parse(readFileSync(entrada, 'utf8'))
const METODOS = ['get', 'post', 'put', 'patch', 'delete']

// Segue `$ref` locais (#/components/...).
function resolver(o) {
  for (let i = 0; o && typeof o === 'object' && o.$ref; i++) {
    if (i > 20) throw new Error('$ref circular: ' + o.$ref)
    o = o.$ref
      .slice(2)
      .split('/')
      .map((s) => s.replace(/~1/g, '/').replace(/~0/g, '~'))
      .reduce((n, k) => n?.[k], doc)
  }
  return o
}

// `example` no objeto de mídia; senão o primeiro de `examples`; senão o `example`/`examples` do schema.
function exemploDe(media) {
  if (media.example !== undefined) return { ok: true, valor: media.example }
  const ex = media.examples && Object.values(media.examples)[0]
  if (ex !== undefined && resolver(ex)?.value !== undefined)
    return { ok: true, valor: resolver(ex).value }
  const s = resolver(media.schema)
  if (s?.example !== undefined) return { ok: true, valor: s.example }
  if (Array.isArray(s?.examples) && s.examples.length) return { ok: true, valor: s.examples[0] }
  return { ok: false }
}

// Todos os exemplos de uma mídia: `example` único = "padrao"; `examples` nomeados = o nome do contrato;
// sem nenhum dos dois, o `example`/`examples[0]` do schema (também "padrao").
function exemplosNomeados(media) {
  const achados = {}
  if (media.example !== undefined) achados.padrao = media.example
  for (const [nome, ex] of Object.entries(media.examples ?? {})) {
    const valor = resolver(ex)?.value
    if (valor !== undefined) achados[nome] = valor
  }
  if (Object.keys(achados).length === 0) {
    const s = resolver(media.schema)
    if (s?.example !== undefined) achados.padrao = s.example
    else if (Array.isArray(s?.examples) && s.examples.length) achados.padrao = s.examples[0]
  }
  return achados
}

const entradas = []
const semExemplo = []
const exemplosPorOperacao = {}
let respostasComExemplo = 0
let totalDeExemplos = 0
for (const [caminho, item] of Object.entries(doc.paths)) {
  for (const metodo of METODOS) {
    const op = item[metodo]
    if (!op) continue
    const porStatus = {}
    for (const [codigo, resposta] of Object.entries(op.responses ?? {})) {
      const conteudo = resolver(resposta)?.content ?? {}
      const tipos = Object.keys(conteudo)
      if (!tipos.length) continue
      const achados = exemplosNomeados(conteudo[tipos.find((t) => /json/.test(t)) ?? tipos[0]])
      const quantos = Object.keys(achados).length
      if (!quantos) continue
      porStatus[codigo] = achados
      respostasComExemplo++
      totalDeExemplos += quantos
    }
    exemplosPorOperacao[op.operationId] = porStatus
    const status = Object.keys(op.responses)
      .filter((s) => /^2\d\d$/.test(s))
      .sort()[0]
    if (!status) {
      semExemplo.push(`${op.operationId}: sem resposta 2xx`)
      continue
    }
    const resp = resolver(op.responses[status])
    const tipos = Object.keys(resp.content ?? {})
    let contentType = null
    let corpo
    if (tipos.length) {
      contentType = tipos.find((t) => /json/.test(t)) ?? tipos[0]
      const e = exemploDe(resp.content[contentType])
      if (!e.ok) {
        semExemplo.push(`${op.operationId}: ${status} ${contentType} sem example`)
        continue
      }
      corpo = e.valor
    }
    // 2xx sem corpo declarado (204, 202): resposta vazia.
    entradas.push({
      operationId: op.operationId,
      metodo,
      caminho,
      status: Number(status),
      contentType,
      corpo,
    })
  }
}

// Ordem: segmento literal antes de {parametro}, da esquerda para a direita.
const segs = (c) => c.split('/').filter(Boolean)
const ehParam = (s) => /^\{.*\}$/.test(s)
const chave = (c) =>
  segs(c)
    .map((s) => (ehParam(s) ? '1' : '0') + s)
    .join('/')
const ordenadas = [...entradas].sort((a, b) => (chave(a.caminho) < chave(b.caminho) ? -1 : 1))

// Pares do mesmo método que casam com a mesma URL (mesmo número de segmentos; em cada posição, iguais
// ou um deles é parâmetro). Informa qual rota é a mais específica; "AMBIGUO" quando nenhuma é.
const sobrepostos = []
for (let i = 0; i < entradas.length; i++)
  for (let j = i + 1; j < entradas.length; j++) {
    const a = entradas[i]
    const b = entradas[j]
    if (a.metodo !== b.metodo || a.caminho === b.caminho) continue
    const sa = segs(a.caminho)
    const sb = segs(b.caminho)
    if (sa.length !== sb.length) continue
    if (!sa.every((s, k) => s === sb[k] || ehParam(s) || ehParam(sb[k]))) continue
    const aDomina = sa.every((s, k) => s === sb[k] || (!ehParam(s) && ehParam(sb[k])))
    const bDomina = sb.every((s, k) => s === sa[k] || (!ehParam(s) && ehParam(sa[k])))
    sobrepostos.push({
      metodo: a.metodo,
      a: a.caminho,
      b: b.caminho,
      especifico: aDomina ? a.caminho : bDomina ? b.caminho : 'AMBIGUO',
    })
  }

// Padrão de caminho do MSW: qualquer origem (*), {id} vira :id.
const rota = (c) => '*' + c.replace(/\{([^}]+)\}/g, ':$1')
const linhas = ordenadas.map(
  (e) =>
    `  http.${e.metodo}(${JSON.stringify(rota(e.caminho))}, () => resposta(${JSON.stringify(e.operationId)})),`,
)
const exemplos = Object.fromEntries(
  ordenadas.map((e) => [
    e.operationId,
    {
      metodo: e.metodo,
      caminho: e.caminho,
      status: e.status,
      contentType: e.contentType,
      corpo: e.corpo,
    },
  ]),
)

const ts = `// GERADO por scripts/gerar-mocks.mjs a partir de ${entrada}. Não edite; não vai para o git.
import { http, HttpResponse, type JsonBodyType } from 'msw'

export interface Exemplo {
  metodo: string
  caminho: string
  status: number
  contentType: string | null
  corpo?: unknown
}

export const versaoDoContrato = ${JSON.stringify(doc.info?.version ?? '')}

export const exemplos: Record<string, Exemplo> = ${JSON.stringify(exemplos)}

function resposta(operationId: string): Response {
  const e = exemplos[operationId]
  if (e.corpo === undefined) return new HttpResponse(null, { status: e.status })
  if (e.contentType?.includes('json'))
    return HttpResponse.json(e.corpo as JsonBodyType, { status: e.status })
  return HttpResponse.text(String(e.corpo), {
    status: e.status,
    headers: { 'Content-Type': e.contentType ?? 'text/plain' },
  })
}

export const handlers = [
${linhas.join('\n')}
]
`
mkdirSync(dirname(saida), { recursive: true })
writeFileSync(saida, ts)

const saidaDosExemplos = join(dirname(saida), 'exemplos.ts')
writeFileSync(
  saidaDosExemplos,
  `// GERADO por scripts/gerar-mocks.mjs a partir de ${entrada}. Não edite; não vai para o git.
// Todos os exemplos de cada resposta do contrato: exemplosPorOperacao[operationId][status][nome].
export const exemplosPorOperacao: Record<string, Record<string, Record<string, unknown>>> = ${JSON.stringify(exemplosPorOperacao)}
`,
)

const ambiguos = sobrepostos.filter((p) => p.especifico === 'AMBIGUO')
console.log(
  JSON.stringify(
    {
      saida,
      saidaDosExemplos,
      versaoDoContrato: doc.info?.version ?? null,
      operacoes: entradas.length + semExemplo.length,
      handlersGerados: entradas.length,
      respostasComExemplo,
      totalDeExemplos,
      semExemploUtilizavel: semExemplo,
      paresQueCasamComAMesmaUrl: sobrepostos.length,
      ambiguos,
      ...(detalhes ? { pares: sobrepostos } : {}),
    },
    null,
    1,
  ),
)
if (ambiguos.length) console.error('AVISO: rotas ambíguas (nenhuma é mais específica):', ambiguos)
if (semExemplo.length) {
  console.error('ERRO: operação sem exemplo utilizável no contrato:', semExemplo)
  process.exitCode = 1
}
