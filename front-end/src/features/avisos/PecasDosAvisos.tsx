import type { Esquema } from '../../shared/api/tipos'
import { Markdown } from '../../shared/ui/Markdown'
import { Selo } from '../../shared/ui/Selo'
import { ROTULO_DO_TIPO, TOM_DO_TIPO } from './rotulosDosAvisos'

/** O conteúdo do aviso: o simples é texto puro; o detalhado passa pelo Markdown da 1a (nunca HTML). */
export function ConteudoDoAviso({
  tipo,
  conteudo,
}: {
  tipo: Esquema<'AvisoTipo'>
  conteudo: string
}) {
  if (tipo === 'DETALHADO') return <Markdown texto={conteudo} />
  return <p className="whitespace-pre-line text-corpo text-texto">{conteudo}</p>
}

export function SeloDoTipo({ tipo }: { tipo: Esquema<'AvisoTipo'> }) {
  return <Selo tom={TOM_DO_TIPO[tipo]}>{ROTULO_DO_TIPO[tipo]}</Selo>
}
