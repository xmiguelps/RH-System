import type { Esquema } from '../../shared/api/tipos'
import { formatarData, formatarDiaMesDaData, formatarDiaMes } from '../../shared/formato/datas'
import { plural } from '../../shared/formato/texto'
import type { TomDoSelo } from '../../shared/ui/Selo'

type Situacao = Esquema<'FeriasSituacao'>
type Pedido = Esquema<'FeriasPedido'>

export const SITUACAO_DAS_FERIAS: Record<Situacao, { texto: string; tom: TomDoSelo }> = {
  COM_O_GESTOR: { texto: 'Aguardando o gestor', tom: 'alerta' },
  SUGESTAO_DE_DATA: { texto: 'Outra data sugerida', tom: 'info' },
  COM_O_RH: { texto: 'Aguardando o RH', tom: 'alerta' },
  APROVADA: { texto: 'Aprovada', tom: 'sucesso' },
  EM_GOZO: { texto: 'Em gozo', tom: 'sucesso' },
  CONCLUIDA: { texto: 'Concluída', tom: 'neutro' },
  REJEITADA: { texto: 'Recusada', tom: 'erro' },
  CANCELADA: { texto: 'Cancelada', tom: 'neutro' },
}

export const OPCOES_DE_SITUACAO: readonly { valor: Situacao; rotulo: string }[] = (
  Object.keys(SITUACAO_DAS_FERIAS) as Situacao[]
).map((valor) => ({ valor, rotulo: SITUACAO_DAS_FERIAS[valor].texto }))

/** "15 dias" (singular só no 1). */
export function textoDeDias(n: number): string {
  return `${n} ${plural(n, 'dia', 'dias')}`
}

/** "05/01/2026 a 16/01/2026" (date, sem fuso). */
export function periodoCompleto(inicio: string, fim: string): string {
  return `${formatarData(inicio)} a ${formatarData(fim)}`
}

/** "10/11 a 24/11". */
export function periodoCurto(inicio: string, fim: string): string {
  return `${formatarDiaMesDaData(inicio)} a ${formatarDiaMesDaData(fim)}`
}

/** As frases das etapas (notas da fatia 7, "Regras comuns"); o motivo da recusa sempre aparece. */
export function frasesDaDecisao(p: Pedido): string[] {
  const frases: string[] = []
  const g = p.decisaoGestor
  if (p.situacao === 'SUGESTAO_DE_DATA' && p.sugestao)
    frases.push(
      `${p.sugestao.sugeridaPor.nome} sugeriu outras datas (${periodoCurto(p.sugestao.dataInicio, p.sugestao.dataFim)}) e espera a resposta de ${p.funcionario.nome}`,
    )
  if (g) {
    if (g.decisao === 'APROVADA')
      frases.push(
        g.automatica
          ? 'Aprovado automaticamente (gestor ausente)'
          : `Aprovado pelo gestor ${g.decididaPor?.nome ?? ''} em ${formatarDiaMes(g.decididaEmUtc)}`.replace('  ', ' '),
      )
    else
      frases.push(
        `Recusado por ${g.decididaPor?.nome ?? 'o gestor'} em ${formatarDiaMes(g.decididaEmUtc)}${g.observacao ? `: ${g.observacao}` : ''}`,
      )
  } else if (!p.gestorResponsavel && (p.etapa === 'RH' || p.decisaoRh))
    frases.push('Este pedido não passa pelo gestor')
  const r = p.decisaoRh
  if (r) {
    if (r.decisao === 'APROVADA')
      frases.push(`Confirmado pelo RH (${r.decididaPor?.nome ?? 'RH'}) em ${formatarDiaMes(r.decididaEmUtc)}`)
    else
      frases.push(
        `Recusado por ${r.decididaPor?.nome ?? 'o RH'} em ${formatarDiaMes(r.decididaEmUtc)}${r.observacao ? `: ${r.observacao}` : ''}`,
      )
  }
  if (p.situacao === 'CANCELADA' && p.sugestao?.respostaDaPessoa === 'RECUSOU')
    frases.push('Cancelado: a pessoa recusou as datas sugeridas')
  if (p.situacao === 'CANCELADA' && p.sugestao?.respostaDaPessoa === 'EXPIROU')
    frases.push('Cancelado: a sugestão de datas expirou')
  return frases
}

/** As frases extras do cartão: abono, 13º e a mensagem da pessoa. */
export function extrasDoPedido(p: Pedido): string[] {
  const extras: string[] = []
  if (p.abonoPecuniario) extras.push(`Quer vender ${textoDeDias(p.diasAbono)}.`)
  if (p.adiantar13Salario) extras.push('Quer adiantar a 1ª parcela do 13º.')
  return extras
}

/**
 * "Como funciona" (telas 34 e 48). As regras de férias são provisórias no contrato: o texto fica
 * só aqui, num lugar, para trocar rápido.
 */
export function comoFunciona(gestor: { nome: string } | null): string[] {
  return [
    'Você pode dividir as férias em até 3 partes: uma de pelo menos 14 dias e as outras de pelo menos 5 dias.',
    'Dá para vender até 10 dias (abono) e pedir o adiantamento da 1ª parcela do 13º.',
    'As férias não podem começar nos 2 dias antes de um feriado ou do fim de semana.',
    gestor
      ? `Peça com pelo menos 30 dias de antecedência. ${gestor.nome} analisa e depois o RH confirma.`
      : 'Peça com pelo menos 30 dias de antecedência. O RH analisa o pedido.',
  ]
}

export const TEXTO_DA_RECUSA_DA_SUGESTAO =
  'Seu pedido foi encerrado. Converse com o seu gestor antes de abrir outro pedido de férias.'
