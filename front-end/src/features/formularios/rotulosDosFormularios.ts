import type { Esquema } from '../../shared/api/tipos'
import { dataDoInstante, formatarDiaMesDaData } from '../../shared/formato/datas'
import { plural } from '../../shared/formato/texto'
import type { TomDoSelo } from '../../shared/ui/Selo'

type Tipo = Esquema<'FormularioTipo'>
type Sentido = Esquema<'FormularioSentidoAvaliacao'>
type Situacao = Esquema<'FormularioSituacao'>
type PerguntaTipo = Esquema<'PerguntaTipo'>

/** Os seis cartões do tipo (tela 17): as duas avaliações são o mesmo `tipo` com sentidos diferentes. */
export type TipoDoCartao =
  'ENTREVISTA' | 'ADMISSAO' | 'AVALIACAO_EQUIPE' | 'AVALIACAO_GESTOR' | 'PESQUISA' | 'OUTRO'

export const CARTOES_DE_TIPO: readonly {
  valor: TipoDoCartao
  titulo: string
  descricao: string
}[] = [
  { valor: 'ENTREVISTA', titulo: 'Entrevista automática', descricao: 'Candidatos de uma vaga' },
  {
    valor: 'ADMISSAO',
    titulo: 'Formulário de admissão',
    descricao: 'Quem acabou de ser contratado',
  },
  {
    valor: 'AVALIACAO_EQUIPE',
    titulo: 'Avaliação da equipe',
    descricao: 'O gestor avalia cada pessoa',
  },
  {
    valor: 'AVALIACAO_GESTOR',
    titulo: 'Avaliação do gestor',
    descricao: 'A equipe avalia o gestor',
  },
  { valor: 'PESQUISA', titulo: 'Pesquisa', descricao: 'Clima, satisfação ou outro tema' },
  { valor: 'OUTRO', titulo: 'Sugestão', descricao: 'Caixa sempre aberta para ideias' },
]

export function cartaoDoTipo(tipo: Tipo, sentido: Sentido | null): TipoDoCartao {
  if (tipo === 'AVALIACAO')
    return sentido === 'EQUIPE_AVALIA_GESTOR' ? 'AVALIACAO_GESTOR' : 'AVALIACAO_EQUIPE'
  return tipo
}

export function tipoDoCartao(cartao: TipoDoCartao): {
  tipo: Tipo
  sentidoAvaliacao: Sentido | null
} {
  if (cartao === 'AVALIACAO_EQUIPE')
    return { tipo: 'AVALIACAO', sentidoAvaliacao: 'GESTOR_AVALIA_EQUIPE' }
  if (cartao === 'AVALIACAO_GESTOR')
    return { tipo: 'AVALIACAO', sentidoAvaliacao: 'EQUIPE_AVALIA_GESTOR' }
  return { tipo: cartao, sentidoAvaliacao: null }
}

/** "Entrevista automática", "Avaliação do gestor", "Sugestão"... */
export function rotuloDoTipo(tipo: Tipo, sentido: Sentido | null): string {
  return CARTOES_DE_TIPO.find((c) => c.valor === cartaoDoTipo(tipo, sentido))!.titulo
}

export const SITUACAO_DO_FORMULARIO: Record<Situacao, { texto: string; tom: TomDoSelo }> = {
  RASCUNHO: { texto: 'Rascunho', tom: 'neutro' },
  AGENDADO: { texto: 'Agendado', tom: 'info' },
  PUBLICADO: { texto: 'Publicado', tom: 'sucesso' },
  ENCERRADO: { texto: 'Encerrado', tom: 'contorno' },
}

export const ROTULO_DA_PERGUNTA: Record<PerguntaTipo, string> = {
  RESPOSTA_CURTA: 'Resposta curta',
  RESPOSTA_LONGA: 'Resposta longa',
  MULTIPLA_ESCOLHA: 'Múltipla escolha',
  ESCALA: 'Escala de 1 a 5',
  ARQUIVO: 'Envio de arquivo',
}

export const TIPOS_DE_PERGUNTA = [
  'RESPOSTA_CURTA',
  'RESPOSTA_LONGA',
  'MULTIPLA_ESCOLHA',
  'ESCALA',
  'ARQUIVO',
] as const

export const LIMITE_DA_RESPOSTA: Partial<Record<PerguntaTipo, number>> = {
  RESPOSTA_CURTA: 200,
  RESPOSTA_LONGA: 4000,
}

export const ARQUIVOS_ACEITOS = [
  'application/pdf',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
] as const

/** O texto da coluna "Respostas" da tela 16 (R5: anônimo aberto não tem número nenhum). */
export function textoDasRespostas(item: Esquema<'FormularioRhItem'>): {
  texto: string
  barra?: { valor: number; maximo: number }
} {
  if (item.anonimo && item.situacao !== 'ENCERRADO')
    return {
      texto: `${item.esperados ?? 0} ${plural(item.esperados ?? 0, 'esperado', 'esperados')}, resultado ao encerrar`,
    }
  if (item.alvo === 'LINK') {
    const n = item.respondidos ?? 0
    return { texto: n === 0 ? 'Nenhuma ainda' : `${n} ${plural(n, 'resposta', 'respostas')}` }
  }
  if (item.tipo === 'OUTRO') {
    const n = item.respondidos ?? 0
    return { texto: `${n} ${plural(n, 'sugestão', 'sugestões')}` }
  }
  if (item.situacao === 'RASCUNHO' && !item.respondidos) return { texto: 'Nenhuma ainda' }
  if (item.esperados === null || item.respondidos === null) return { texto: '—' }
  return {
    texto: `${item.respondidos} de ${item.esperados}`,
    barra: { valor: item.respondidos, maximo: Math.max(item.esperados, 1) },
  }
}

/** "Sem prazo", "Até 31/10" ou "Encerrou em dd/mm" (dia de `encerradoEmUtc` em Brasília; D5-2). */
export function textoDoPrazo(item: {
  prazo: string | null
  situacao: Situacao
  encerradoEmUtc: string | null
}): string {
  if (item.situacao === 'ENCERRADO' && item.encerradoEmUtc)
    return `Encerrou em ${formatarDiaMesDaData(dataDoInstante(item.encerradoEmUtc))}`
  return item.prazo ? `Até ${formatarDiaMesDaData(item.prazo)}` : 'Sem prazo'
}

export const AVISO_DO_ANONIMO =
  'Suas respostas são anônimas: o RH e o gestor não veem quem respondeu o quê.'
