import type { Esquema } from '../../shared/api/tipos'
import { dataDoInstante, formatarData, formatarDiaMesDaData } from '../../shared/formato/datas'
import { plural } from '../../shared/formato/texto'
import type { TomDoSelo } from '../../shared/ui/Selo'

type Situacao = Esquema<'TreinamentoSituacao'>
type ModuloTipo = Esquema<'TreinamentoModuloTipo'>
type Participante = Esquema<'TreinamentoParticipanteSituacao'>

export const SITUACAO_DO_TREINAMENTO: Record<Situacao, { texto: string; tom: TomDoSelo }> = {
  RASCUNHO: { texto: 'Rascunho', tom: 'neutro' },
  AGENDADO: { texto: 'Agendado', tom: 'info' },
  EM_ANDAMENTO: { texto: 'Em andamento', tom: 'sucesso' },
  ENCERRADO: { texto: 'Encerrado', tom: 'contorno' },
}

export const SITUACAO_DA_PESSOA: Record<Participante, { texto: string; tom: TomDoSelo }> = {
  INSCRITO: { texto: 'Não iniciado', tom: 'neutro' },
  EM_ANDAMENTO: { texto: 'Em andamento', tom: 'alerta' },
  CONCLUIDO: { texto: 'Concluído', tom: 'sucesso' },
}

export const ROTULO_DO_MODULO: Record<ModuloTipo, string> = {
  VIDEO: 'Vídeo',
  TEXTO: 'Texto',
  LINK: 'Link',
  PDF: 'Arquivo PDF',
}

export const TIPOS_DE_MODULO = ['VIDEO', 'PDF', 'TEXTO', 'LINK'] as const

export const TAMANHO_DO_PDF_MB = 10

/** "Obrigatório", "Opcional" ou "Onboarding". */
export function seloDoTreinamento(t: { obrigatorio: boolean; onboarding: boolean }): string {
  if (t.onboarding) return 'Onboarding'
  return t.obrigatorio ? 'Obrigatório' : 'Opcional'
}

/** "1h40", "45 min"; sem duração, null. */
export function formatarDuracao(minutos: number | null | undefined): string | null {
  if (!minutos) return null
  const horas = Math.floor(minutos / 60)
  const resto = minutos % 60
  if (horas === 0) return `${resto} min`
  return resto ? `${horas}h${String(resto).padStart(2, '0')}` : `${horas}h`
}

/** A segunda linha do módulo: "Vídeo, 15 minutos" (e o nome do arquivo no PDF). */
export function linhaDoModulo(m: {
  tipo: ModuloTipo
  duracaoMinutos: number | null
  nomeArquivo?: string | null
}): string {
  const partes = [ROTULO_DO_MODULO[m.tipo]]
  if (m.duracaoMinutos)
    partes.push(`${m.duracaoMinutos} ${plural(m.duracaoMinutos, 'minuto', 'minutos')}`)
  if (m.nomeArquivo) partes.push(m.nomeArquivo)
  return partes.join(', ')
}

/** "Começa em" na lista (o encerrado mostra "Encerrado em dd/mm"); sem fim (T1). */
export function textoDoInicio(t: {
  situacao: Situacao
  comecaEm: string | null
  encerradoEmUtc: string | null
}): string {
  if (t.situacao === 'ENCERRADO' && t.encerradoEmUtc)
    return `Encerrado em ${formatarDiaMesDaData(dataDoInstante(t.encerradoEmUtc))}`
  return t.comecaEm ? formatarData(t.comecaEm) : '—'
}

/** O dia (dd/mm/aaaa) de um instante, ou "—". */
export function diaDoInstante(instante: string | null): string {
  return instante ? formatarData(dataDoInstante(instante)) : '—'
}
