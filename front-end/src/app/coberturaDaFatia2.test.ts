// @vitest-environment node
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

/** As 3 operações de `25-fatia-2-importacao.yaml` (todas têm tela). */
const OPERACOES_DA_FATIA_2 = [
  'baixarModeloDeImportacao',
  'validarPlanilhaDeImportacao',
  'confirmarImportacao',
] as const

const RAIZ = resolve(__dirname, '..')

function arquivosDeTeste(pasta: string): string[] {
  const achados: string[] = []
  for (const nome of readdirSync(pasta)) {
    const caminho = join(pasta, nome)
    if (statSync(caminho).isDirectory()) achados.push(...arquivosDeTeste(caminho))
    else if (/\.test\.tsx?$/.test(nome) && !nome.startsWith('coberturaDaFatia'))
      achados.push(caminho)
  }
  return achados
}

function operacoesComTesteDeContrato(): ReadonlySet<string> {
  const achadas = new Set<string>()
  const padrao = /(?:capturar|esperarRequisicaoConforme)\(\s*'(\w+)'/g
  for (const pasta of ['features', 'app']) {
    for (const arquivo of arquivosDeTeste(join(RAIZ, pasta))) {
      for (const m of readFileSync(arquivo, 'utf8').matchAll(padrao)) achadas.add(m[1]!)
    }
  }
  return achadas
}

describe('cobertura da fatia 2', () => {
  it('cada uma das 3 operacoes aparece num teste de contrato (capturar ou esperarRequisicaoConforme)', () => {
    const cobertas = operacoesComTesteDeContrato()
    expect(OPERACOES_DA_FATIA_2.filter((op) => !cobertas.has(op))).toEqual([])
  })
})
