import type { Esquema, Perfil } from '../../shared/api/tipos'
import type { OpcaoDeSelecao } from '../../shared/ui/Selecao'
import type { TomDoSelo } from '../../shared/ui/Selo'

export type TipoDeContrato = Esquema<'FuncionarioTipoContrato'>

const TIPOS: Record<string, string> = { CLT: 'CLT', ESTAGIO: 'Estágio' }
const NIVEIS: Record<Perfil, string> = { Funcionario: 'Colaborador', Gestor: 'Gestor', RH: 'RH' }

/** Valor que o front não conhece (o contrato pode ganhar tipos) aparece como veio. */
export function rotuloDoTipoDeContrato(valor: string | null | undefined): string {
  if (!valor) return '—'
  return TIPOS[valor] ?? valor
}

export function rotuloDoNivelDeAcesso(perfil: Perfil | null | undefined): string {
  if (!perfil) return '—'
  return NIVEIS[perfil] ?? perfil
}

export const OPCOES_DE_TIPO_DE_CONTRATO: readonly OpcaoDeSelecao<TipoDeContrato>[] = [
  { valor: 'CLT', rotulo: 'CLT' },
  { valor: 'ESTAGIO', rotulo: 'Estágio' },
]

export const OPCOES_DE_NIVEL_DE_ACESSO: readonly OpcaoDeSelecao<Perfil>[] = [
  { valor: 'Funcionario', rotulo: 'Colaborador' },
  { valor: 'Gestor', rotulo: 'Gestor' },
  { valor: 'RH', rotulo: 'RH' },
]

/** Etiqueta "Situação hoje" (telas 01, 03 e 52). */
export function etiquetaDaSituacao(
  situacao: Esquema<'FuncionarioSituacao'>,
  situacaoHoje: Esquema<'SituacaoHoje'> | null,
): { texto: string; tom: TomDoSelo } {
  if (situacao === 'INATIVO') return { texto: 'Inativo', tom: 'neutro' }
  if (situacao === 'EM_ADMISSAO') return { texto: 'Em admissão', tom: 'contorno' }
  if (situacaoHoje === 'FERIAS') return { texto: 'Férias', tom: 'info' }
  if (situacaoHoje === 'ATESTADO') return { texto: 'Atestado', tom: 'alerta' }
  return { texto: 'Ativo', tom: 'sucesso' }
}
