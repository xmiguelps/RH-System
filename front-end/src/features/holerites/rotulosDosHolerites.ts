import type { Esquema } from '../../shared/api/tipos'
import { formatarMesAno, nomeDoMes } from '../../shared/formato/datas'

type Tipo = Esquema<'HoleriteTipo'>

export const ROTULO_DO_TIPO: Record<Tipo, string> = {
  MENSAL: 'Mensal',
  FERIAS: 'Férias',
  DECIMO_TERCEIRO: '13º salário',
}

export const OPCOES_DE_TIPO: readonly { valor: Tipo; rotulo: string }[] = [
  { valor: 'MENSAL', rotulo: 'Mensal' },
  { valor: 'DECIMO_TERCEIRO', rotulo: '13º salário' },
  { valor: 'FERIAS', rotulo: 'Férias' },
]

export const TIPOS_DO_HOLERITE = ['MENSAL', 'FERIAS', 'DECIMO_TERCEIRO'] as const

export const PDF = ['application/pdf'] as const
export const TAMANHO_MAXIMO_MB = 5

/** "Setembro de 2026", "Férias de janeiro de 2026" ou "13º salário de 2026". */
export function referenciaDoHolerite(h: { tipo: Tipo; mes: number; ano: number }): string {
  if (h.tipo === 'FERIAS') return `Férias de ${nomeDoMes(h.mes)} de ${h.ano}`
  if (h.tipo === 'DECIMO_TERCEIRO') return `13º salário de ${h.ano}`
  return formatarMesAno(h.ano, h.mes)
}

export const OPCOES_DE_MES = Array.from({ length: 12 }, (_, i) => {
  const nome = nomeDoMes(i + 1)
  return { valor: String(i + 1), rotulo: `${nome.charAt(0).toUpperCase()}${nome.slice(1)}` }
})

/** Quando publicar: agora, no dia do pagamento (o sistema calcula) ou numa data. */
export type QuandoPublicar = 'AGORA' | 'DIA_DO_PAGAMENTO' | 'DATA'

export function camposDeQuandoPublicar(
  quando: QuandoPublicar,
  data: string,
): { publicarEm?: string; noDiaDoPagamento?: boolean } {
  if (quando === 'DIA_DO_PAGAMENTO') return { noDiaDoPagamento: true }
  if (quando === 'DATA') return { publicarEm: data }
  return {}
}
