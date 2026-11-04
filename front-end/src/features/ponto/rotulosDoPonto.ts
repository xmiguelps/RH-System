import type { Esquema } from '../../shared/api/tipos'
import { formatarDiaMesDaData, formatarDiaMesHora, formatarHora } from '../../shared/formato/datas'
import { formatarMinutosComoHoras } from '../../shared/formato/numeros'
import type { TomDoSelo } from '../../shared/ui/Selo'

type Periodo = Esquema<'PontoPeriodo'>
type Pedido = Esquema<'PontoPedidoAjuste'>
type Atestado = Esquema<'Atestado'>
export type Rotulado = { texto: string; tom: TomDoSelo }

const DIAS: Record<Esquema<'PontoDiaDaSemana'>, string> = {
  SEGUNDA: 'segunda',
  TERCA: 'terça',
  QUARTA: 'quarta',
  QUINTA: 'quinta',
  SEXTA: 'sexta',
  SABADO: 'sábado',
  DOMINGO: 'domingo',
}

export function rotuloDoDiaDaSemana(dia: Esquema<'PontoDiaDaSemana'>, maiuscula = false): string {
  const texto = DIAS[dia]
  return maiuscula ? texto.charAt(0).toUpperCase() + texto.slice(1) : texto
}

/**
 * A etiqueta da situação de um dia (`telas\fatia-3.md`, "Etiqueta da situação do dia"). Na tela 12 o
 * dia de hoje é "Em andamento"; nas outras, "Hoje".
 */
export function etiquetaDoDia(
  dia: Pick<Esquema<'PontoEspelhoDia'>, 'situacao' | 'atrasoMinutos' | 'nomeFeriado'>,
  onde: 'pessoa' | 'rh' = 'pessoa',
): Rotulado {
  switch (dia.situacao) {
    case 'NORMAL':
      return { texto: 'Normal', tom: 'sucesso' }
    case 'ATRASO':
      return {
        texto: dia.atrasoMinutos === null ? 'Atraso' : `Atraso de ${dia.atrasoMinutos} min`,
        tom: 'alerta',
      }
    case 'AJUSTE_EM_ANALISE':
      return { texto: 'Ajuste em análise', tom: 'alerta' }
    case 'EM_ANDAMENTO':
      return { texto: onde === 'rh' ? 'Em andamento' : 'Hoje', tom: 'info' }
    case 'FOLGA':
      return { texto: 'Folga', tom: 'neutro' }
    case 'SEM_REGISTRO':
      return { texto: 'Sem registro', tom: 'alerta' }
    case 'INCOMPLETO':
      return { texto: 'Incompleto', tom: 'alerta' }
    case 'FERIADO':
      return { texto: dia.nomeFeriado ?? 'Feriado', tom: 'neutro' }
    case 'FERIAS':
      return { texto: 'Férias', tom: 'info' }
    case 'FALTA_COMPENSADA':
      return { texto: 'Falta compensada', tom: 'neutro' }
  }
}

/** O chip de um período: "07:56 - 12:01", "07:58 - em aberto" (hoje), "13:02 - sem saída" (passado). */
export function textoDoPeriodo(periodo: Periodo, ehHoje: boolean): Rotulado {
  const entrada = periodo.entrada ? formatarHora(periodo.entrada.instanteUtc) : null
  const saida = periodo.saida ? formatarHora(periodo.saida.instanteUtc) : null
  if (entrada && saida) return { texto: `${entrada} - ${saida}`, tom: 'contorno' }
  if (entrada) {
    return ehHoje
      ? { texto: `${entrada} - em aberto`, tom: 'neutro' }
      : { texto: `${entrada} - sem saída`, tom: 'alerta' }
  }
  return { texto: `sem entrada - ${saida ?? '—'}`, tom: 'alerta' }
}

export function rotuloDaSituacaoHoje(
  situacao: Esquema<'SituacaoHoje'>,
  periodoAberto: boolean,
): Rotulado {
  switch (situacao) {
    case 'TRABALHANDO':
      return periodoAberto
        ? { texto: 'Trabalhando', tom: 'sucesso' }
        : { texto: 'Saiu', tom: 'neutro' }
    case 'FERIAS':
      return { texto: 'Férias', tom: 'info' }
    case 'SEM_REGISTRO':
      return { texto: 'Sem registro', tom: 'alerta' }
    case 'ATESTADO':
      return { texto: 'Atestado', tom: 'info' }
    case 'SEM_EXPEDIENTE':
      return { texto: 'Sem expediente', tom: 'neutro' }
    case 'AINDA_NAO_COMECOU':
      return { texto: 'Ainda não começou', tom: 'neutro' }
  }
}

/** A etiqueta do pedido nas listas do RH e do gestor (`telas\fatia-3.md`, "Etiqueta do pedido"). */
export function etiquetaDoPedido(pedido: Pedido, paraOGestor = false): Rotulado {
  if (paraOGestor && pedido.podeDecidir) return { texto: 'Aguardando você', tom: 'alerta' }
  if (pedido.etapa === 'GESTOR') return { texto: 'Com o gestor', tom: 'alerta' }
  if (pedido.etapa === 'RH') {
    if (pedido.decisaoGestor?.decisao === 'APROVADA') {
      return pedido.decisaoGestor.automatica
        ? { texto: 'Aprovado automaticamente', tom: 'sucesso' }
        : { texto: 'Aprovado pelo gestor', tom: 'sucesso' }
    }
    return { texto: 'Com o RH', tom: 'info' }
  }
  return pedido.status === 'APROVADA'
    ? { texto: 'Aprovado', tom: 'sucesso' }
    : { texto: 'Recusado', tom: 'erro' }
}

/** O chip do pedido na tela da própria pessoa. */
export function etiquetaDoMeuPedido(status: Esquema<'PontoPedidoStatus'>): Rotulado {
  if (status === 'PENDENTE') return { texto: 'Em análise', tom: 'alerta' }
  if (status === 'APROVADA') return { texto: 'Aprovado', tom: 'sucesso' }
  return { texto: 'Recusado', tom: 'erro' }
}

/** A resposta que a pessoa lê sob a justificativa (nunca o nome de alguém do RH antes da decisão). */
export function respostaDoPedido(pedido: Pedido): string {
  if (pedido.etapa === 'GESTOR') {
    return pedido.gestorResponsavel
      ? `Aguardando a análise de ${pedido.gestorResponsavel.nome}`
      : 'Aguardando a análise do gestor'
  }
  if (pedido.etapa === 'RH') return 'Aguardando a análise do RH'
  const decisao =
    pedido.decisaoRh ??
    (pedido.decisaoGestor?.decisao === 'REJEITADA' ? pedido.decisaoGestor : null)
  if (!decisao) return pedido.status === 'APROVADA' ? 'Aprovado.' : 'Recusado.'
  const quem = decisao.decididaPor?.nome ?? (decisao.automatica ? 'Sistema' : 'RH')
  if (decisao.observacao) return `${quem}: ${decisao.observacao}`
  return `${quem} ${decisao.decisao === 'APROVADA' ? 'aprovou' : 'recusou'}.`
}

/** A frase de status do detalhe do pedido na tela 13. */
export function fraseDoStatusDoPedido(pedido: Pedido): string {
  if (pedido.etapa === 'GESTOR') {
    return `${pedido.gestorResponsavel?.nome ?? 'O gestor'} ainda não analisou.`
  }
  if (pedido.etapa === 'RH') {
    const g = pedido.decisaoGestor
    if (!g) return 'Este pedido não passa pelo gestor.'
    if (g.automatica) return 'O gestor está ausente: etapa aprovada automaticamente.'
    return `${g.decididaPor?.nome ?? 'O gestor'} aprovou em ${formatarDiaMesHora(g.decididaEmUtc)}.`
  }
  const d = pedido.decisaoRh ?? pedido.decisaoGestor
  const quem = d?.decididaPor?.nome
  const resultado = pedido.status === 'APROVADA' ? 'Aprovado' : 'Recusado'
  return d && quem
    ? `${resultado} por ${quem} em ${formatarDiaMesHora(d.decididaEmUtc)}.`
    : `${resultado}.`
}

export function periodoDoAtestado(atestado: Atestado): string {
  const inicio = formatarDiaMesDaData(atestado.dataInicio)
  if (atestado.horaInicio && atestado.horaFim) {
    return `${inicio}, das ${atestado.horaInicio} às ${atestado.horaFim}`
  }
  if (atestado.dataFim === atestado.dataInicio) return inicio
  return `${inicio} a ${formatarDiaMesDaData(atestado.dataFim)}`
}

export function situacaoDoAtestado(
  situacao: Esquema<'AtestadoSituacao'>,
  paraORh = false,
): Rotulado {
  if (situacao === 'ENVIADO') {
    return { texto: paraORh ? 'Aguardando validação' : 'Aguardando o RH', tom: 'alerta' }
  }
  if (situacao === 'VALIDADO') return { texto: 'Validado', tom: 'sucesso' }
  return { texto: 'Recusado', tom: 'erro' }
}

export function tomDoSaldo(minutos: number | null): 'neutro' | 'alerta' | 'sucesso' {
  if (minutos === null || minutos === 0) return 'neutro'
  return minutos < 0 ? 'alerta' : 'sucesso'
}

export function saldoComSinal(minutos: number | null): string {
  return minutos === null ? '—' : formatarMinutosComoHoras(minutos, { sinal: true })
}

export function horasOuTraco(minutos: number | null): string {
  return minutos === null ? '—' : formatarMinutosComoHoras(minutos)
}

export function rotuloDoTipoDeLancamento(tipo: Esquema<'BancoHorasTipoLancamento'>): string {
  return tipo === 'CREDITO' ? 'Crédito' : 'Débito'
}

/** Crédito soma (+), débito tira (U+2212): a quantidade vem sempre positiva. */
export function sinalDoLancamento(
  l: Pick<Esquema<'BancoHorasLancamento'>, 'tipo' | 'quantidadeMinutos'>,
): string {
  const valor = formatarMinutosComoHoras(l.quantidadeMinutos)
  return l.tipo === 'CREDITO' ? `+${valor}` : `−${valor}`
}

/** A fila do RH (tela 13 e o número do botão da 12): o mais antigo primeiro. */
export const FILA_DO_RH = {
  status: 'PENDENTE',
  ordenarPor: 'CRIADO_EM',
  direcao: 'ASC',
} as const
