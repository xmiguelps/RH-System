import { formatarDiaEMes } from '../../shared/formato/datas'
import { formatarMinutosComoHoras } from '../../shared/formato/numeros'
import { plural } from '../../shared/formato/texto'

export type ItemDeIndicador = { rotulo: string; valor: string; tom: 'neutro' | 'alerta' | 'erro' }

const VAZIO = '—'

export function itemDeBancoDeHoras(minutos: number | null): ItemDeIndicador {
  if (minutos === null) return { rotulo: 'Banco de horas', valor: VAZIO, tom: 'neutro' }
  return {
    rotulo: 'Banco de horas',
    valor: formatarMinutosComoHoras(minutos, { sinal: true }),
    tom: minutos < 0 ? 'alerta' : 'neutro',
  }
}

export function itemDeFerias(dias: number | null): ItemDeIndicador {
  return {
    rotulo: 'Férias disponíveis',
    valor: dias === null ? VAZIO : `${dias} ${plural(dias, 'dia', 'dias')}`,
    tom: 'neutro',
  }
}

export function itemDeAdvertencias(quantidade: number | null): ItemDeIndicador {
  return {
    rotulo: 'Advertências',
    valor: quantidade === null ? VAZIO : String(quantidade),
    tom: quantidade !== null && quantidade > 0 ? 'erro' : 'neutro',
  }
}

export function itemDeProximoPagamento(data: string | null): ItemDeIndicador {
  return { rotulo: 'Próximo pagamento', valor: formatarDiaEMes(data), tom: 'neutro' }
}

export function itemDeTreinamentosPendentes(quantidade: number | null): ItemDeIndicador {
  return {
    rotulo: 'Treinamentos pendentes',
    valor: quantidade === null ? VAZIO : String(quantidade),
    tom: quantidade !== null && quantidade > 0 ? 'alerta' : 'neutro',
  }
}
