// @vitest-environment node
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

/** As 34 operações de `30-fatia-3-ponto.yaml`, menos `obterBancoDeHorasDeFuncionario` (sem tela por decisão da ficha 3). */
const OPERACOES_DA_FATIA_3 = [
  'obterPontoHoje',
  'registrarMarcacaoDePonto',
  'obterMeuEspelhoDePonto',
  'listarMeusPedidosDeAjuste',
  'criarPedidoDeAjuste',
  'obterMeuPedidoDeAjuste',
  'obterMeuBancoDeHoras',
  'listarMeusAtestados',
  'enviarAtestado',
  'obterPontoDaEquipe',
  'obterEspelhoDePontoDaPessoaDaEquipe',
  'baixarEspelhoDePontoPdfDaEquipe',
  'listarPedidosDeAjusteDaEquipe',
  'obterPedidoDeAjusteDaEquipe',
  'aprovarPedidoDeAjusteComoGestor',
  'recusarPedidoDeAjusteComoGestor',
  'listarAtestadosDaEquipe',
  'baixarAtestadoDaEquipe',
  'obterEspelhoDePontoDeFuncionario',
  'baixarEspelhoDePontoPdf',
  'corrigirMarcacoesDoDia',
  'listarPedidosDeAjusteParaRh',
  'obterPedidoDeAjusteParaRh',
  'aprovarPedidoDeAjusteComoRh',
  'recusarPedidoDeAjusteComoRh',
  'listarFeriados',
  'criarFeriado',
  'editarFeriado',
  'exportarPontoParaFolha',
  'listarAtestados',
  'baixarAtestado',
  'validarAtestado',
  'recusarAtestado',
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

describe('cobertura da fatia 3', () => {
  it('a lista tem 33 operacoes e deixa de fora so a que nao tem tela', () => {
    expect(OPERACOES_DA_FATIA_3).toHaveLength(33)
    expect(OPERACOES_DA_FATIA_3).not.toContain('obterBancoDeHorasDeFuncionario' as never)
  })

  it('cada uma das 33 operacoes com tela aparece num teste de contrato (capturar ou esperarRequisicaoConforme)', () => {
    const cobertas = operacoesComTesteDeContrato()
    expect(OPERACOES_DA_FATIA_3.filter((op) => !cobertas.has(op))).toEqual([])
  })
})
