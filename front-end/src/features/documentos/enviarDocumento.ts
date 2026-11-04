import { clienteHttp } from '../../shared/api/api'
import type { ClienteHttp } from '../../shared/api/clienteApi'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import type { Esquema } from '../../shared/api/tipos'
import { multipart } from '../../shared/arquivos/multipart'

export type CamposDoNovoDocumento = {
  modo: Esquema<'DocumentoColaboradorModo'>
  tipo: Esquema<'DocumentoColaboradorTipo'>
  descricao?: string
  dataValidade?: string
  arquivo?: File
}

function exigir<T>(dado: T | undefined): T {
  if (dado === undefined) throw new ErroDeApi(500, 'Resposta inesperada da API.')
  return dado
}

/** O RH anexa ou pede um documento da ficha (multipart; sem arquivo em PEDIR_PARA_ENVIAR). */
export async function adicionarDocumento(
  funcionarioId: number,
  campos: CamposDoNovoDocumento,
  cliente: ClienteHttp = clienteHttp,
): Promise<Esquema<'DocumentoColaborador'>> {
  const { data } = await cliente.POST('/api/rh/funcionarios/{funcionarioId}/documentos', {
    params: { path: { funcionarioId } },
    ...multipart<Esquema<'DocumentoColaboradorNovoForm'>>({
      modo: campos.modo,
      tipo: campos.tipo,
      descricao: campos.descricao || undefined,
      dataValidade: campos.dataValidade || undefined,
      arquivo: campos.modo === 'ANEXAR_AGORA' ? campos.arquivo : undefined,
    }),
  })
  return exigir(data)
}

/** A própria pessoa envia o arquivo de um documento pendente (Meus documentos). */
export async function enviarMeuArquivo(
  documentoId: number,
  arquivo: File,
  cliente: ClienteHttp = clienteHttp,
): Promise<Esquema<'DocumentoColaborador'>> {
  const { data } = await cliente.POST('/api/eu/documentos/{documentoId}/arquivo', {
    params: { path: { documentoId } },
    ...multipart<{ arquivo: string }>({ arquivo }),
  })
  return exigir(data)
}
