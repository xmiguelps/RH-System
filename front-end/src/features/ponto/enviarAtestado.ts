import { clienteHttp } from '../../shared/api/api'
import type { ClienteHttp } from '../../shared/api/clienteApi'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import type { Esquema } from '../../shared/api/tipos'
import { multipart } from '../../shared/arquivos/multipart'

export type CamposDoAtestado = {
  dataInicio: string
  dataFim: string
  horaInicio?: string
  horaFim?: string
  arquivo: File
}

/** A pessoa envia o atestado (multipart). Dia(s) inteiro(s) não leva hora; "algumas horas", as duas. */
export async function enviarAtestado(
  campos: CamposDoAtestado,
  cliente: ClienteHttp = clienteHttp,
): Promise<Esquema<'Atestado'>> {
  const { data } = await cliente.POST('/api/eu/ponto/atestados', {
    ...multipart<Esquema<'AtestadoEnviar'>>({
      dataInicio: campos.dataInicio,
      dataFim: campos.dataFim,
      horaInicio: campos.horaInicio,
      horaFim: campos.horaFim,
      arquivo: campos.arquivo,
    }),
  })
  if (data === undefined) throw new ErroDeApi(500, 'Resposta inesperada da API.')
  return data
}
