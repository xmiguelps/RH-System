// @vitest-environment node
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

/**
 * As 31 operações de `60-fatia-6-tad.yaml`, menos duas (ficha 6, seção 7):
 * - `listarTreinamentosDeFuncionario`: sem tela, decisão do dono de 2026-11-02 (a ficha do RH não
 *   tem a aba Treinamentos);
 * - `incluirFuncionarioNoOnboarding`: chamada por dentro de `criarFuncionario`, `confirmarImportacao`
 *   e `readmitirFuncionario`; nenhuma tela a chama, sem botão (dúvida D6-4).
 */
const OPERACOES_DA_FATIA_6 = [
  'obterIndicadoresDeTreinamentos',
  'listarTreinamentos',
  'obterTreinamento',
  'encerrarTreinamento',
  'excluirTreinamento',
  'listarParticipantesDeTreinamento',
  'exportarParticipantesDeTreinamento',
  'baixarCertificadoDeParticipante',
  'criarTreinamento',
  'atualizarTreinamento',
  'adicionarModuloDeTreinamento',
  'atualizarModuloDeTreinamento',
  'reordenarModulosDeTreinamento',
  'excluirModuloDeTreinamento',
  'contarPublicoDeTreinamento',
  'publicarTreinamento',
  'listarMeusTreinamentos',
  'inscreverMeEmTreinamento',
  'iniciarMeuTreinamento',
  'baixarMeuCertificado',
  'obterMeuTreinamento',
  'obterConteudoDeModulo',
  'baixarArquivoDeModulo',
  'concluirModuloDeTreinamento',
  'listarTreinamentosDaEquipe',
  'listarPessoasDaEquipeNoTreinamento',
  'listarTreinamentosDePessoaDaEquipe',
  'validarCertificado',
  'listarTreinamentosDeOnboarding',
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

describe('cobertura da fatia 6', () => {
  it('a lista tem 29 operacoes e deixa de fora so as duas sem tela', () => {
    expect(OPERACOES_DA_FATIA_6).toHaveLength(29)
    for (const fora of ['listarTreinamentosDeFuncionario', 'incluirFuncionarioNoOnboarding'])
      expect(OPERACOES_DA_FATIA_6).not.toContain(fora as never)
  })

  it('cada uma das 29 operacoes aparece num teste de contrato (capturar ou esperarRequisicaoConforme)', () => {
    const cobertas = operacoesComTesteDeContrato()
    expect(OPERACOES_DA_FATIA_6.filter((op) => !cobertas.has(op))).toEqual([])
  })
})
