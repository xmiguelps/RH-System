import type { PropsDaAbaDaEquipe } from '../equipe/abasDaFichaDaEquipe'
import { EspelhoDaPessoaDaEquipe } from './EspelhoDaEquipe'

/** Aba Ponto da ficha da equipe (sem print): o conteúdo da tela 56, mês atual, sem as folgas. */
export function AbaPontoDaEquipe({ ficha }: PropsDaAbaDaEquipe) {
  return <EspelhoDaPessoaDaEquipe funcionarioId={ficha.id} semFolgas />
}
