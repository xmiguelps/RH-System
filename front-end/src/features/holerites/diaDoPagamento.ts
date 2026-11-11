import { $api } from '../../shared/api/api'
import { hojeEmSaoPaulo } from '../../shared/formato/datas'

/** O dia do pagamento que o sistema vai usar (obterProximoPagamento, fatia 7); null sem configuração. */
export function useDiaDoPagamento(): { data: string | null; carregando: boolean } {
  const consulta = $api.useQuery('get', '/api/eu/pagamento/proximo')
  return { data: consulta.data?.pagamento?.data ?? null, carregando: consulta.isLoading }
}

export function anosDoHolerite(): { valor: string; rotulo: string }[] {
  const atual = Number(hojeEmSaoPaulo().slice(0, 4))
  return [atual + 1, atual, atual - 1, atual - 2].map((a) => ({
    valor: String(a),
    rotulo: String(a),
  }))
}
