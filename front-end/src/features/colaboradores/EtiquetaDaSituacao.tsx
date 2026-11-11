import type { Esquema } from '../../shared/api/tipos'
import { Selo } from '../../shared/ui/Selo'
import { etiquetaDaSituacao } from './rotulos'

export function EtiquetaDaSituacao({
  situacao,
  situacaoHoje,
}: {
  situacao: Esquema<'FuncionarioSituacao'>
  situacaoHoje: Esquema<'SituacaoHoje'> | null
}) {
  const { texto, tom } = etiquetaDaSituacao(situacao, situacaoHoje)
  return <Selo tom={tom}>{texto}</Selo>
}
