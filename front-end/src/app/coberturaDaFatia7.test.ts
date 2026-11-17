// @vitest-environment node
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

/**
 * As 41 operações de `70-fatia-7-gestao.yaml` (17 de férias, 11 de benefícios, 5 de pagamento e 8 de
 * painéis), menos uma e mais duas (ficha 7, seção 6):
 * - `listarFeriasDaEquipe`: sem uso em tela (a lista "Esperando sua aprovação" do Painel da equipe já
 *   vem dentro de `obterPainelDaEquipe`, os mesmos itens);
 * - `listarEquipeDoGestor` (fatia 1): a faixa "Hoje" do Painel da equipe (tela 51);
 * - `listarAniversariantes` (fatia 4): o cartão "Aniversariantes" das telas 32 e 46.
 */
const OPERACOES_DA_FATIA_7 = [
  // 7a, férias
  'obterMinhasFerias',
  'pedirFerias',
  'conferirPedidoDeFerias',
  'aceitarSugestaoDeDataDasFerias',
  'recusarSugestaoDeDataDasFerias',
  'obterCalendarioDeFeriasDaEquipe',
  'aprovarFeriasComoGestor',
  'recusarFeriasComoGestor',
  'sugerirOutraDataDasFerias',
  'obterFeriasDaPessoaDaEquipe',
  'listarFeriasParaORh',
  'confirmarFeriasComoRh',
  'recusarFeriasComoRh',
  'obterFeriasDaFichaDoFuncionario',
  'registrarFeriasPeloRh',
  'conferirRegistroDeFerias',
  // 7b, benefícios
  'obterMeusBeneficios',
  'pedirAdesaoABeneficio',
  'pedirInclusaoDeDependente',
  'pedirAtualizacaoDeTrajeto',
  'listarPedidosDeBeneficio',
  'aprovarAdesaoABeneficio',
  'recusarAdesaoABeneficio',
  'aprovarDependenteDeBeneficio',
  'recusarDependenteDeBeneficio',
  'aprovarTrajetoDeBeneficio',
  'recusarTrajetoDeBeneficio',
  // 7c, pagamento
  'obterProximoPagamento',
  'listarExcecoesDePagamento',
  'obterCalendarioDePagamento',
  'definirExcecaoDePagamento',
  'removerExcecaoDePagamento',
  // 7d, painéis
  'obterPainelDoFuncionario',
  'obterContadoresDoMenuDoFuncionario',
  'obterContadoresDoMenuDoGestor',
  'obterContadoresDoMenuDoRh',
  'obterPainelDaEquipe',
  'obterPainelDoRh',
  'listarPendenciasDoRh',
  'listarPessoasSemRegistroHoje',
  // de outras fatias, cobertas aqui
  'listarEquipeDoGestor',
  'listarAniversariantes',
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

describe('cobertura da fatia 7', () => {
  it('a lista tem 42 operacoes: as 41 da fatia, sem listarFeriasDaEquipe, mais as duas de outras fatias', () => {
    expect(OPERACOES_DA_FATIA_7).toHaveLength(42)
    expect(new Set(OPERACOES_DA_FATIA_7).size).toBe(42)
    expect(OPERACOES_DA_FATIA_7).not.toContain('listarFeriasDaEquipe' as never)
  })

  it('cada uma das 42 operacoes aparece num teste de contrato (capturar ou esperarRequisicaoConforme)', () => {
    const cobertas = operacoesComTesteDeContrato()
    expect(OPERACOES_DA_FATIA_7.filter((op) => !cobertas.has(op))).toEqual([])
  })
})
