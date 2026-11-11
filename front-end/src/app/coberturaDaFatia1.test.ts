// @vitest-environment node
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { exemplos } from '../shared/mocks/gerado/handlers'

/** As 43 operações de `20-fatia-1-colaboradores.yaml`, menos `listarEquipeDoGestor` (tela 51, fatia 7). */
const OPERACOES_DA_FATIA_1 = [
  'listarFuncionarios',
  'criarFuncionario',
  'obterFichaDoFuncionario',
  'editarFuncionario',
  'obterIndicadoresDoFuncionario',
  'inativarFuncionario',
  'cancelarDesligamentoAgendado',
  'readmitirFuncionario',
  'obterFichaDaEquipe',
  'obterIndicadoresDaEquipe',
  'listarDocumentosDoFuncionario',
  'adicionarDocumentoDoFuncionario',
  'baixarDocumentoDoFuncionario',
  'listarMeusDocumentos',
  'enviarMeuDocumento',
  'baixarMeuDocumento',
  'listarCargos',
  'criarCargo',
  'editarCargo',
  'listarSetores',
  'criarSetor',
  'editarSetor',
  'listarGestores',
  'cadastrarGestor',
  'configurarAusenciaDoGestor',
  'listarBeneficiosDoCatalogo',
  'criarBeneficioDoCatalogo',
  'editarBeneficioDoCatalogo',
  'excluirBeneficioDoCatalogo',
  'obterEmpresa',
  'salvarEmpresa',
  'listarUsuariosDoSistema',
  'enviarConviteDeAcesso',
  'ativarUsuarioDoSistema',
  'desativarUsuarioDoSistema',
  'listarEventosDeAuditoria',
  'exportarAuditoria',
  'obterEventoDeAuditoria',
  'solicitarRedefinicaoDeSenha',
  'validarLinkDeSenha',
  'definirSenhaPeloLink',
  'trocarMinhaSenha',
] as const

const RAIZ = resolve(__dirname, '..')
const ESTE_ARQUIVO = 'coberturaDaFatia1.test.ts'

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

/** Só os testes de tela (src\features e src\app): os de src\test e src\shared testam os ajudantes. */
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

describe('cobertura da fatia 1', () => {
  it('cada uma das 42 operacoes com tela aparece num teste de contrato (capturar ou esperarRequisicaoConforme)', () => {
    expect(OPERACOES_DA_FATIA_1).toHaveLength(42)
    expect(ESTE_ARQUIVO).toMatch(/^coberturaDaFatia/)
    const cobertas = operacoesComTesteDeContrato()
    const faltando = OPERACOES_DA_FATIA_1.filter((op) => !cobertas.has(op))
    expect(faltando).toEqual([])
  })

  it('listarEquipeDoGestor tem exemplo no contrato mas nao tem tela aqui', () => {
    expect(exemplos).toHaveProperty('listarEquipeDoGestor')
    expect(OPERACOES_DA_FATIA_1).not.toContain('listarEquipeDoGestor')
    // A tela 51 é da fatia 7; desde a fatia 3 a tela 54 lê esta rota só para o subtítulo (ficha 3).
  })
})
