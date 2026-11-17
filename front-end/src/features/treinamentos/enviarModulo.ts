import { clienteHttp } from '../../shared/api/api'
import type { ClienteHttp } from '../../shared/api/clienteApi'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import type { Esquema } from '../../shared/api/tipos'
import { multipart } from '../../shared/arquivos/multipart'

type Tipo = Esquema<'TreinamentoModuloTipo'>

export type CamposDoModulo = {
  titulo: string
  tipo: Tipo
  duracaoMinutos?: number | null
  urlExterna?: string | null
  conteudoTexto?: string | null
  arquivo?: File | null
}

/**
 * Só o campo do tipo vai (campo que não é do tipo é recusado com 400). O `VIDEO` é link **ou** arquivo (um só dos dois):
 * com arquivo novo vai o arquivo; sem arquivo vai o endereço, se houver; sem nenhum dos dois, na edição, o arquivo atual fica.
 */
function corpoDoModulo(c: CamposDoModulo) {
  const videoPorArquivo = c.tipo === 'VIDEO' && !!c.arquivo
  return multipart<Esquema<'TreinamentoModuloComando'>>({
    titulo: c.titulo,
    tipo: c.tipo,
    duracaoMinutos: c.duracaoMinutos ?? undefined,
    urlExterna:
      (c.tipo === 'VIDEO' && !videoPorArquivo) || c.tipo === 'LINK' ? c.urlExterna : undefined,
    conteudoTexto: c.tipo === 'TEXTO' ? c.conteudoTexto : undefined,
    arquivo: c.tipo === 'PDF' || videoPorArquivo ? (c.arquivo ?? undefined) : undefined,
  })
}

function semResposta(): never {
  throw new ErroDeApi(500, 'Resposta inesperada da API.')
}

/** Acrescentar módulo (multipart). */
export async function adicionarModulo(
  treinamentoId: number,
  campos: CamposDoModulo,
  cliente: ClienteHttp = clienteHttp,
): Promise<Esquema<'TreinamentoModuloRh'>> {
  const { data } = await cliente.POST('/api/rh/treinamentos/{treinamentoId}/modulos', {
    params: { path: { treinamentoId } },
    ...corpoDoModulo(campos),
  })
  return data ?? semResposta()
}

/** Salvar o editor de um módulo (multipart). Sem arquivo novo num PDF (ou num vídeo que já é arquivo), o arquivo atual fica. */
export async function atualizarModulo(
  treinamentoId: number,
  moduloId: number,
  campos: CamposDoModulo,
  cliente: ClienteHttp = clienteHttp,
): Promise<Esquema<'TreinamentoModuloRh'>> {
  const { data } = await cliente.PUT('/api/rh/treinamentos/{treinamentoId}/modulos/{moduloId}', {
    params: { path: { treinamentoId, moduloId } },
    ...corpoDoModulo(campos),
  })
  return data ?? semResposta()
}
