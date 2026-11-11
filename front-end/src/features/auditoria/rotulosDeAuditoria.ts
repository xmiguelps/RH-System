import type { Esquema } from '../../shared/api/tipos'
import type { OpcaoDeSelecao } from '../../shared/ui/Selecao'
import type { TomDoSelo } from '../../shared/ui/Selo'

type Area = Esquema<'AuditoriaArea'>
type Acao = Esquema<'AuditoriaAcao'>

const AREAS: Record<Area, string> = {
  ACESSO: 'Acesso',
  COLABORADORES: 'Colaboradores',
  DOCUMENTOS: 'Documentos',
  CADASTROS: 'Cadastros',
  IMPORTACAO: 'Importação',
  PONTO: 'Ponto',
  FERIADOS: 'Feriados',
  FERIAS: 'Férias',
  HOLERITES: 'Holerites',
  ADVERTENCIAS: 'Advertências',
  AVISOS: 'Avisos',
  FORMULARIOS: 'Formulários',
  TREINAMENTOS: 'Treinamentos',
  BENEFICIOS: 'Benefícios',
  ATESTADOS: 'Atestados',
}

const ACOES: Record<Acao, string> = {
  ENTROU: 'Entrou',
  ACESSO_NEGADO: 'Acesso negado',
  CRIOU: 'Criou',
  ALTEROU: 'Alterou',
  EXCLUIU: 'Excluiu',
  INATIVOU: 'Inativou',
  REGISTROU: 'Registrou',
  PEDIU_AJUSTE: 'Pediu ajuste',
  APROVOU: 'Aprovou',
  RECUSOU: 'Recusou',
  PUBLICOU: 'Publicou',
  ENVIOU_EMAIL: 'Enviou e-mail',
  IMPORTOU: 'Importou',
  BAIXOU: 'Baixou',
}

export function rotuloDaArea(area: Area): string {
  return AREAS[area] ?? area
}

export function rotuloDaAcao(acao: Acao): string {
  return ACOES[acao] ?? acao
}

export const OPCOES_DE_AREA: readonly OpcaoDeSelecao<Area>[] = (Object.keys(AREAS) as Area[]).map(
  (valor) => ({ valor, rotulo: AREAS[valor] }),
)

export const OPCOES_DE_ACAO: readonly OpcaoDeSelecao<Acao>[] = (Object.keys(ACOES) as Acao[]).map(
  (valor) => ({ valor, rotulo: ACOES[valor] }),
)

export function tomDaAcao(acao: Acao): TomDoSelo {
  return acao === 'EXCLUIU' || acao === 'ACESSO_NEGADO' || acao === 'INATIVOU' ? 'erro' : 'neutro'
}

/** Precedência do contrato: sistema → Automático; desligado → Desligado; senão o perfil da hora. */
export function rotuloDoQuem(ator: Esquema<'AuditoriaAtor'>): string {
  if (ator.tipo === 'SISTEMA') return 'Automático'
  if (ator.desligado) return 'Desligado'
  if (ator.perfil === 'Funcionario') return 'Colaborador'
  return ator.perfil ?? '—'
}
