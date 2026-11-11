// @vitest-environment node
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

/**
 * As 35 operações de `40-fatia-4-comunicacao-e-documentos.yaml`, menos `listarAniversariantes`:
 * a tela dela é o cartão "Aniversariantes" da fatia 7d, e a ficha 7 a conta no teste de cobertura
 * dela (ficha 4, seção 6). Nenhuma operação com tela fica sem teste.
 */
const OPERACOES_DA_FATIA_4 = [
  'listarAvisos',
  'criarAviso',
  'obterResumoDosAvisos',
  'obterAviso',
  'editarAviso',
  'excluirAviso',
  'publicarAviso',
  'encerrarAviso',
  'listarLeiturasDoAviso',
  'lembrarQuemNaoViuAviso',
  'listarMeusAvisos',
  'obterMeuAviso',
  'marcarAvisoComoLido',
  'listarHoleritesDoFuncionario',
  'enviarHolerite',
  'enviarHoleritesEmLote',
  'substituirHolerite',
  'obterLinkDeDownloadDoHolerite',
  'listarMeusHolerites',
  'obterLinkDeDownloadDoMeuHolerite',
  'listarAdvertencias',
  'obterResumoDasAdvertencias',
  'obterAdvertencia',
  'responderContestacaoDeAdvertencia',
  'listarAdvertenciasDoFuncionario',
  'registrarAdvertencia',
  'listarAdvertenciasDaEquipe',
  'obterResumoDasAdvertenciasDaEquipe',
  'obterAdvertenciaDaEquipe',
  'listarAdvertenciasDoFuncionarioDaEquipe',
  'registrarAdvertenciaDaEquipe',
  'listarMinhasAdvertencias',
  'confirmarCienciaDeAdvertencia',
  'contestarAdvertencia',
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

describe('cobertura da fatia 4', () => {
  it('a lista tem 34 operacoes e deixa de fora so listarAniversariantes (tela da 7d)', () => {
    expect(OPERACOES_DA_FATIA_4).toHaveLength(34)
    expect(OPERACOES_DA_FATIA_4).not.toContain('listarAniversariantes' as never)
  })

  it('cada uma das 34 operacoes aparece num teste de contrato (capturar ou esperarRequisicaoConforme)', () => {
    const cobertas = operacoesComTesteDeContrato()
    expect(OPERACOES_DA_FATIA_4.filter((op) => !cobertas.has(op))).toEqual([])
  })

  it('obterProximoPagamento (fatia 7) tem o teste de contrato das telas 36 e 49 (ficha 4, item 4)', () => {
    expect(operacoesComTesteDeContrato().has('obterProximoPagamento')).toBe(true)
  })
})
