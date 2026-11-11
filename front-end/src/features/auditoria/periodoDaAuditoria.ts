import { formatarData, somarDias } from '../../shared/formato/datas'

export type PeriodoDaAuditoria = 'hoje' | '7' | '30' | 'personalizado'

export const PERIODOS: readonly PeriodoDaAuditoria[] = ['hoje', '7', '30', 'personalizado']

/** O que vai à API. `'7'` não manda nada (a API já assume 7 dias); `null`: período incompleto. */
export function intervaloDoPeriodo(
  periodo: PeriodoDaAuditoria,
  de: string | undefined,
  ate: string | undefined,
  hoje: string,
): { de?: string; ate?: string } | null {
  if (periodo === '7') return {}
  if (periodo === 'hoje') return { de: hoje, ate: hoje }
  if (periodo === '30') return { de: somarDias(hoje, -29), ate: hoje }
  if (!de || !ate || ate < de) return null
  return { de, ate }
}

export function textoDoPeriodo(
  periodo: PeriodoDaAuditoria,
  de: string | undefined,
  ate: string | undefined,
): string {
  if (periodo === 'hoje') return 'de hoje'
  if (periodo === '30') return 'dos últimos 30 dias'
  if (periodo === 'personalizado' && de && ate)
    return `de ${formatarData(de)} a ${formatarData(ate)}`
  return 'dos últimos 7 dias'
}
