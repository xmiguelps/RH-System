import type { Esquema } from '../../shared/api/tipos'
import { formatarData } from '../../shared/formato/datas'
import type { TomDoSelo } from '../../shared/ui/Selo'

type Tipo = Esquema<'AdvertenciaTipo'>
type Situacao = Esquema<'AdvertenciaSituacao'>
type AplicadaPor = Esquema<'AdvertenciaAplicadaPor'>

export const ROTULO_DO_TIPO: Record<Tipo, string> = { VERBAL: 'Verbal', ESCRITA: 'Escrita' }

export const OPCOES_DE_TIPO: readonly { valor: Tipo; rotulo: string }[] = [
  { valor: 'VERBAL', rotulo: 'Verbal' },
  { valor: 'ESCRITA', rotulo: 'Escrita' },
]

export const SITUACAO: Record<Situacao, { texto: string; tom: TomDoSelo }> = {
  PENDENTE: { texto: 'Aguardando ciência', tom: 'alerta' },
  APLICADA: { texto: 'Ciente', tom: 'sucesso' },
  CONTESTADA: { texto: 'Contestada', tom: 'erro' },
  CANCELADA: { texto: 'Cancelada', tom: 'neutro' },
}

export const SITUACOES = ['PENDENTE', 'APLICADA', 'CONTESTADA', 'CANCELADA'] as const
export const TIPOS = ['VERBAL', 'ESCRITA'] as const

/** "Carlos Ribeiro" (gestor) ou "RH" (o nome vem null); no cartão da pessoa, "..., seu gestor". */
export function textoDeAplicadaPor(aplicadaPor: AplicadaPor, daPropriaPessoa = false): string {
  if (aplicadaPor.tipo === 'RH') return 'RH'
  const nome = aplicadaPor.nome ?? 'Gestor'
  return daPropriaPessoa ? `${nome}, seu gestor` : nome
}

/** "Advertência escrita de 05/10/2026". */
export function tituloDaAdvertencia(a: { tipo: Tipo; data: string }): string {
  return `Advertência ${ROTULO_DO_TIPO[a.tipo].toLowerCase()} de ${formatarData(a.data)}`
}

export const LIMITE_DO_MOTIVO = 255
export const LIMITE_DA_DESCRICAO = 5000
export const LIMITE_DA_RESPOSTA = 2000
