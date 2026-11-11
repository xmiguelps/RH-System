export const FUSO_DO_PORTAL = 'America/Sao_Paulo'

const VAZIO = '—'

type Partes = { ano: string; mes: string; dia: string; hora: string; minuto: string }

const formatoDePartes = new Intl.DateTimeFormat('pt-BR', {
  timeZone: FUSO_DO_PORTAL,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
})

function partes(instante: Date): Partes {
  const valores: Record<string, string> = {}
  for (const parte of formatoDePartes.formatToParts(instante)) valores[parte.type] = parte.value
  return {
    ano: valores.year ?? '',
    mes: valores.month ?? '',
    dia: valores.day ?? '',
    hora: valores.hour ?? '',
    minuto: valores.minute ?? '',
  }
}

function lerInstante(instanteUtc: string | null | undefined): Date | null {
  if (!instanteUtc) return null
  const data = new Date(instanteUtc)
  return Number.isNaN(data.getTime()) ? null : data
}

/** Data de calendário ('2026-10-07'): não passa por Date, então nunca muda de dia. */
export function formatarData(data: string | null | undefined): string {
  if (!data) return VAZIO
  const achado = /^(\d{4})-(\d{2})-(\d{2})$/.exec(data)
  if (!achado) return data
  return `${achado[3]}/${achado[2]}/${achado[1]}`
}

const MESES = [
  'janeiro',
  'fevereiro',
  'março',
  'abril',
  'maio',
  'junho',
  'julho',
  'agosto',
  'setembro',
  'outubro',
  'novembro',
  'dezembro',
]

/** Data de calendário por extenso, sem ano e sem passar por Date: '1990-06-18' → '18 de junho'. */
export function formatarDiaEMes(data: string | null | undefined): string {
  if (!data) return VAZIO
  const achado = /^(\d{4})-(\d{2})-(\d{2})$/.exec(data)
  const mes = achado ? MESES[Number(achado[2]) - 1] : undefined
  if (!achado || !mes) return data
  return `${Number(achado[3])} de ${mes}`
}

export function formatarDataHora(instanteUtc: string | null | undefined): string {
  const instante = lerInstante(instanteUtc)
  if (!instante) return VAZIO
  const p = partes(instante)
  return `${p.dia}/${p.mes}/${p.ano} ${p.hora}:${p.minuto}`
}

/** O dia (em São Paulo) de um instante UTC: '2026-10-08T02:30:00Z' → '07/10/2026'. */
export function formatarDataDoInstante(instanteUtc: string | null | undefined): string {
  const instante = lerInstante(instanteUtc)
  if (!instante) return VAZIO
  const p = partes(instante)
  return `${p.dia}/${p.mes}/${p.ano}`
}

const formatoComSegundos = new Intl.DateTimeFormat('pt-BR', {
  timeZone: FUSO_DO_PORTAL,
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hourCycle: 'h23',
})

/** '2026-10-07T12:38:12Z' → '07/10/2026 09:38:12' (São Paulo). */
export function formatarDataHoraComSegundos(instanteUtc: string | null | undefined): string {
  const instante = lerInstante(instanteUtc)
  if (!instante) return VAZIO
  return `${formatarDataDoInstante(instanteUtc)} ${formatoComSegundos.format(instante)}`
}

/** Soma dias a uma data de calendário, sem o fuso da máquina: '2026-10-07' + -29 → '2026-09-08'. */
export function somarDias(data: string, dias: number): string {
  const [ano, mes, dia] = data.split('-').map(Number)
  const resultado = new Date(Date.UTC(ano ?? 0, (mes ?? 1) - 1, (dia ?? 1) + dias))
  return resultado.toISOString().slice(0, 10)
}

export function formatarHora(instanteUtc: string | null | undefined): string {
  const instante = lerInstante(instanteUtc)
  if (!instante) return VAZIO
  const p = partes(instante)
  return `${p.hora}:${p.minuto}`
}

export function formatarDiaMes(instanteUtc: string | null | undefined): string {
  const instante = lerInstante(instanteUtc)
  if (!instante) return VAZIO
  const p = partes(instante)
  return `${p.dia}/${p.mes}`
}

export function hojeEmSaoPaulo(agora: Date = new Date()): string {
  const p = partes(agora)
  return `${p.ano}-${p.mes}-${p.dia}`
}

function diaAnterior(dataDeCalendario: string): string {
  const [ano, mes, dia] = dataDeCalendario.split('-').map(Number)
  const data = new Date(Date.UTC(ano ?? 0, (mes ?? 1) - 1, dia ?? 1))
  data.setUTCDate(data.getUTCDate() - 1)
  return data.toISOString().slice(0, 10)
}

export function formatarInstanteRelativo(instanteUtc: string, agora: Date = new Date()): string {
  const instante = lerInstante(instanteUtc)
  if (!instante) return VAZIO
  const p = partes(instante)
  const dia = `${p.ano}-${p.mes}-${p.dia}`
  const hoje = hojeEmSaoPaulo(agora)
  const hora = `${p.hora}:${p.minuto}`
  if (dia === hoje) return `Hoje, ${hora}`
  if (dia === diaAnterior(hoje)) return `Ontem, ${hora}`
  if (p.ano === hoje.slice(0, 4)) return `${p.dia}/${p.mes}, ${hora}`
  return `${p.dia}/${p.mes}/${p.ano}, ${hora}`
}

const formatoPorExtenso = new Intl.DateTimeFormat('pt-BR', {
  timeZone: FUSO_DO_PORTAL,
  weekday: 'long',
  day: 'numeric',
  month: 'long',
})

export function dataPorExtenso(agora: Date = new Date()): string {
  const valores: Record<string, string> = {}
  for (const parte of formatoPorExtenso.formatToParts(agora)) valores[parte.type] = parte.value
  const semana = valores.weekday ?? ''
  const semanaComMaiuscula = semana.charAt(0).toUpperCase() + semana.slice(1)
  return `${semanaComMaiuscula}, ${valores.day} de ${valores.month}`
}

export function saudacao(agora: Date = new Date()): 'Bom dia' | 'Boa tarde' | 'Boa noite' {
  const hora = Number(partes(agora).hora)
  if (hora >= 5 && hora < 12) return 'Bom dia'
  if (hora >= 12 && hora < 18) return 'Boa tarde'
  return 'Boa noite'
}

/** Dia de calendário curto, sem passar por Date: '2026-10-05' → '05/10'. */
export function formatarDiaMesDaData(data: string | null | undefined): string {
  if (!data) return VAZIO
  const achado = /^(\d{4})-(\d{2})-(\d{2})$/.exec(data)
  return achado ? `${achado[3]}/${achado[2]}` : data
}

/** Instante em Brasília no formato das listas: '2026-10-02T21:05:00Z' → '02/10, 18:05'. */
export function formatarDiaMesHora(instanteUtc: string | null | undefined): string {
  const instante = lerInstante(instanteUtc)
  if (!instante) return VAZIO
  const p = partes(instante)
  return `${p.dia}/${p.mes}, ${p.hora}:${p.minuto}`
}

/** O dia de calendário em Brasília de um instante UTC ('...T02:59:59Z' cai no dia anterior). */
export function dataDoInstante(instanteUtc: string): string {
  const instante = lerInstante(instanteUtc)
  if (!instante) return ''
  const p = partes(instante)
  return `${p.ano}-${p.mes}-${p.dia}`
}

/**
 * A hora que a pessoa digita ('13:05' do dia '2026-10-05') vira instante UTC pelo fuso fixo -03:00,
 * como o contrato pede: '2026-10-05T16:05:00Z'. `diaSeguinte` soma um dia (jornada que vira a noite).
 */
export function instanteUtcDaHoraLocal(data: string, hora: string, diaSeguinte = false): string {
  const dia = diaSeguinte ? somarDias(data, 1) : data
  const instante = new Date(`${dia}T${hora}:00-03:00`)
  return instante.toISOString().replace('.000Z', 'Z')
}

/** 'Outubro de 2026' (mês de 1 a 12). */
export function formatarMesAno(ano: number, mes: number): string {
  const nome = MESES[mes - 1] ?? ''
  return `${nome.charAt(0).toUpperCase()}${nome.slice(1)} de ${ano}`
}

/** Nome do mês em minúsculas (1 a 12): 'outubro'. */
export function nomeDoMes(mes: number): string {
  return MESES[mes - 1] ?? ''
}
