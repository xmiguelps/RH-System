import { clienteHttp } from '../../shared/api/api'
import type { ClienteHttp } from '../../shared/api/clienteApi'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import type { Esquema } from '../../shared/api/tipos'
import { multipart } from '../../shared/arquivos/multipart'

/** Envia a planilha inteira (multipart, campo `arquivo`): a API lê e devolve a prévia. */
export async function validarPlanilha(
  arquivo: File,
  cliente: ClienteHttp = clienteHttp,
): Promise<Esquema<'ImportacaoPrevia'>> {
  const { data } = await cliente.POST('/api/rh/importacoes', {
    ...multipart<Esquema<'ImportacaoEnvioForm'>>({ arquivo }),
  })
  if (data === undefined) throw new ErroDeApi(500, 'Resposta inesperada da API.')
  return data
}
