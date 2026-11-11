import type { Esquema } from '../../shared/api/tipos'
import { dataDoInstante, formatarDiaMes, formatarDiaMesHora } from '../../shared/formato/datas'
import type { TomDoSelo } from '../../shared/ui/Selo'

type Tipo = Esquema<'AvisoTipo'>
type Situacao = Esquema<'AvisoSituacao'>
type AvisoRh = Esquema<'AvisoItemRh'>

export const LIMITE_DO_SIMPLES = 280
export const LIMITE_DO_DETALHADO = 10_000

export const ROTULO_DO_TIPO: Record<Tipo, string> = { SIMPLES: 'Simples', DETALHADO: 'Detalhado' }
export const TOM_DO_TIPO: Record<Tipo, TomDoSelo> = { SIMPLES: 'info', DETALHADO: 'contorno' }

export const OPCOES_DE_TIPO = [
  { valor: 'SIMPLES', rotulo: 'Simples' },
  { valor: 'DETALHADO', rotulo: 'Detalhado' },
] as const

export const ABAS_DO_RH: readonly { valor: Situacao; rotulo: string }[] = [
  { valor: 'ATIVO', rotulo: 'Ativos' },
  { valor: 'AGENDADO', rotulo: 'Agendados' },
  { valor: 'EXPIRADO', rotulo: 'Expirados' },
  { valor: 'RASCUNHO', rotulo: 'Rascunhos' },
]

/** O último dia em que o aviso vale ('...T02:59:59Z' é o fim do dia anterior em UTC). */
export function diaDaExpiracao(expiraEmUtc: string | null): string | null {
  return expiraEmUtc ? dataDoInstante(expiraEmUtc) : null
}

/** A linha do cartão do RH (tela 27), por situação. */
export function linhaDoAvisoRh(aviso: AvisoRh): string {
  const expira = aviso.expiraEmUtc ? formatarDiaMes(aviso.expiraEmUtc) : null
  switch (aviso.situacao) {
    case 'RASCUNHO':
      return aviso.publicacaoEmUtc
        ? `Rascunho. Publicação prevista ${formatarDiaMesHora(aviso.publicacaoEmUtc)}`
        : 'Rascunho'
    case 'AGENDADO':
      return `Publica em ${formatarDiaMesHora(aviso.publicacaoEmUtc)}`
    case 'EXPIRADO':
      return `Publicado em ${formatarDiaMesHora(aviso.publicacaoEmUtc)}, expirou em ${expira ?? '—'}`
    default:
      return `Publicado em ${formatarDiaMesHora(aviso.publicacaoEmUtc)}, ${
        expira ? `expira em ${expira}` : 'não expira'
      }`
  }
}

/** Toast depois de publicar: "Aviso publicado." ou "Aviso agendado para dd/MM, HH:mm." */
export function mensagemDePublicacao(aviso: {
  situacao: Situacao
  publicacaoEmUtc: string | null
}) {
  return aviso.situacao === 'AGENDADO'
    ? `Aviso agendado para ${formatarDiaMesHora(aviso.publicacaoEmUtc)}.`
    : 'Aviso publicado.'
}
