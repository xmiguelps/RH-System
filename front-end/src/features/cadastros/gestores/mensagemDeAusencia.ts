import type { Esquema } from '../../../shared/api/tipos'
import { plural } from '../../../shared/formato/texto'

export function mensagemDeAusencia(resposta: Esquema<'GestorAusenciaResposta'>): string {
  const { ajustesDePonto, ferias } = resposta.pedidosAprovadosAgora
  if (ajustesDePonto === 0 && ferias === 0) return 'Marcação salva.'
  const pontos = `${ajustesDePonto} ${plural(ajustesDePonto, 'pedido de ajuste de ponto', 'pedidos de ajuste de ponto')}`
  const deFerias = `${ferias} ${plural(ferias, 'pedido de férias', 'pedidos de férias')}`
  const verbo = ajustesDePonto + ferias === 1 ? 'passou' : 'passaram'
  return `${pontos} e ${deFerias} ${verbo} para a etapa do RH.`
}
