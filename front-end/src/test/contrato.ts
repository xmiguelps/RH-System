/* eslint-disable @typescript-eslint/no-explicit-any -- o contrato lido do YAML não tem tipo; este arquivo só confere chaves */
import { readFileSync } from 'node:fs'
import { expect } from 'vitest'
import { parse } from 'yaml'

export type Enviado = { consulta?: URLSearchParams; corpo?: Record<string, unknown> }

const METODOS = ['get', 'post', 'put', 'patch', 'delete']

let contratoEmCache: any = null

function contrato(): any {
  if (!contratoEmCache) contratoEmCache = parse(readFileSync('../contrato/openapi.yaml', 'utf8'))
  return contratoEmCache
}

function resolver(no: any): any {
  if (no && typeof no === 'object' && typeof no.$ref === 'string') {
    const partes = no.$ref.replace(/^#\//, '').split('/')
    let atual = contrato()
    for (const parte of partes) atual = atual?.[parte.replace(/~1/g, '/').replace(/~0/g, '~')]
    return resolver(atual)
  }
  return no
}

type Operacao = { caminho: string; metodo: string; operacao: any; parametrosDoCaminho: any[] }

function acharOperacao(operationId: string): Operacao | null {
  for (const [caminho, item] of Object.entries<any>(contrato().paths)) {
    for (const metodo of METODOS) {
      const operacao = item[metodo]
      if (operacao?.operationId === operationId) {
        return { caminho, metodo, operacao, parametrosDoCaminho: item.parameters ?? [] }
      }
    }
  }
  return null
}

// Junta propriedades e obrigatórios de um esquema de objeto (com allOf, oneOf e anyOf).
function propriedadesDoEsquema(esquema: any): {
  propriedades: Set<string>
  obrigatorias: Set<string>
  aberto: boolean
} {
  const propriedades = new Set<string>()
  const obrigatorias = new Set<string>()
  let aberto = false
  const visitar = (no: any, contarObrigatorias: boolean) => {
    const e = resolver(no)
    if (!e || typeof e !== 'object') return
    for (const nome of Object.keys(e.properties ?? {})) propriedades.add(nome)
    if (contarObrigatorias) for (const nome of e.required ?? []) obrigatorias.add(nome)
    if (e.additionalProperties && e.additionalProperties !== false) aberto = true
    for (const parte of e.allOf ?? []) visitar(parte, contarObrigatorias)
    for (const parte of [...(e.oneOf ?? []), ...(e.anyOf ?? [])]) visitar(parte, false)
  }
  visitar(esquema, true)
  return { propriedades, obrigatorias, aberto }
}

export function esperarRequisicaoConforme(operationId: string, enviado: Enviado): void {
  const achada = acharOperacao(operationId)
  expect(achada, `a operação "${operationId}" não existe no contrato`).not.toBeNull()
  if (!achada) return
  const { operacao, parametrosDoCaminho } = achada

  if (enviado.consulta) {
    const parametros = [...parametrosDoCaminho, ...(operacao.parameters ?? [])]
      .map(resolver)
      .filter((p: any) => p?.in === 'query')
    const conhecidos = new Set<string>(parametros.map((p: any) => p.name))
    const enviados = new Set<string>(enviado.consulta.keys())
    const aMais = [...enviados].filter((nome) => !conhecidos.has(nome))
    expect(
      aMais,
      `${operationId}: parâmetros de consulta que o contrato não tem: ${aMais.join(', ')}`,
    ).toEqual([])
    const faltando = parametros
      .filter((p: any) => p.required && !enviados.has(p.name))
      .map((p: any) => p.name)
    expect(
      faltando,
      `${operationId}: parâmetros de consulta obrigatórios que faltam: ${faltando.join(', ')}`,
    ).toEqual([])
  }

  if (enviado.corpo !== undefined) {
    const conteudo = resolver(operacao.requestBody)?.content ?? {}
    const tipo = conteudo['application/json'] ?? Object.values<any>(conteudo)[0]
    expect(tipo, `${operationId}: o contrato não tem corpo para esta operação`).toBeDefined()
    if (!tipo) return
    const { propriedades, obrigatorias, aberto } = propriedadesDoEsquema(tipo.schema)
    const chaves = Object.keys(enviado.corpo)
    const aMais = aberto ? [] : chaves.filter((c) => !propriedades.has(c))
    expect(
      aMais,
      `${operationId}: chaves do corpo que o contrato não tem: ${aMais.join(', ')}`,
    ).toEqual([])
    const faltando = [...obrigatorias].filter((c) => !chaves.includes(c))
    expect(
      faltando,
      `${operationId}: chaves obrigatórias do corpo que faltam: ${faltando.join(', ')}`,
    ).toEqual([])
  }
}
