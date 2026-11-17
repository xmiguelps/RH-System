import type { Esquema } from '../../shared/api/tipos'
import type { TomDoSelo } from '../../shared/ui/Selo'

type Situacao = Esquema<'BeneficioAdesaoSituacao'>

export const SITUACAO_DO_PEDIDO_DE_BENEFICIO: Record<Situacao, { texto: string; tom: TomDoSelo }> =
  {
    PENDENTE: { texto: 'Pendente', tom: 'alerta' },
    APROVADO: { texto: 'Aprovado', tom: 'sucesso' },
    RECUSADO: { texto: 'Recusado', tom: 'erro' },
    CANCELADO: { texto: 'Cancelado', tom: 'neutro' },
  }

/** O primeiro dia do mês seguinte ao de `hoje` (aaaa-mm-dd), a sugestão de "Começar em". */
export function primeiroDiaDoProximoMes(hoje: string): string {
  const ano = Number(hoje.slice(0, 4))
  const mes = Number(hoje.slice(5, 7))
  const proximoAno = mes === 12 ? ano + 1 : ano
  const proximoMes = mes === 12 ? 1 : mes + 1
  return `${proximoAno}-${String(proximoMes).padStart(2, '0')}-01`
}

export const TEXTO_DO_PRAZO_DE_RESPOSTA =
  'O RH confere o pedido e responde em até 5 dias úteis. Você vê a resposta em Seus pedidos.'
