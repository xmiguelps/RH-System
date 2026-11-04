// @vitest-environment node
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

/**
 * As 32 operações de `50-fatia-5-formularios.yaml`, menos `adicionarDestinatariosFormulario`:
 * nenhuma tela a chama; ela só existe por dentro de `criarFuncionario`, `confirmarImportacao` e
 * `readmitirFuncionario`, que mandam `formularioAdmissaoId` (ficha 5, seção 3 e seção 7).
 */
const OPERACOES_DA_FATIA_5 = [
  'listarFormulariosRh',
  'criarFormulario',
  'obterFormularioRh',
  'editarFormulario',
  'excluirFormulario',
  'adicionarPerguntaFormulario',
  'editarPerguntaFormulario',
  'excluirPerguntaFormulario',
  'verificarFormulario',
  'salvarPublicacaoFormulario',
  'publicarFormulario',
  'encerrarFormulario',
  'listarRespondentesFormulario',
  'listarCandidatosFormulario',
  'obterRespostasDeFuncionario',
  'obterRespostasDeCandidato',
  'baixarArquivoDeResposta',
  'baixarCurriculoDeCandidato',
  'obterResultadoFormulario',
  'listarAvaliadosFormulario',
  'exportarRespostasFormulario',
  'lembrarQuemFaltaFormulario',
  'listarFormulariosDaEquipe',
  'listarRespondentesDaEquipe',
  'lembrarQuemFaltaNaEquipe',
  'listarMeusFormularios',
  'obterFormularioParaResponder',
  'responderFormulario',
  'obterMinhasRespostas',
  'obterFormularioPublico',
  'responderFormularioPublico',
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

describe('cobertura da fatia 5', () => {
  it('a lista tem 31 operacoes e deixa de fora so adicionarDestinatariosFormulario (sem tela)', () => {
    expect(OPERACOES_DA_FATIA_5).toHaveLength(31)
    expect(OPERACOES_DA_FATIA_5).not.toContain('adicionarDestinatariosFormulario' as never)
  })

  it('cada uma das 31 operacoes aparece num teste de contrato (capturar ou esperarRequisicaoConforme)', () => {
    const cobertas = operacoesComTesteDeContrato()
    expect(OPERACOES_DA_FATIA_5.filter((op) => !cobertas.has(op))).toEqual([])
  })
})
