import { clienteHttp } from '../../shared/api/api'
import type { ClienteHttp } from '../../shared/api/clienteApi'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import type { Esquema } from '../../shared/api/tipos'
import { multipart } from '../../shared/arquivos/multipart'

type Tipo = Esquema<'HoleriteTipo'>
type Quando = { publicarEm?: string; noDiaDoPagamento?: boolean }

export type CamposDoHolerite = { mes: number; ano: number; tipo: Tipo; arquivo: File } & Quando

function semResposta(): never {
  throw new ErroDeApi(500, 'Resposta inesperada da API.')
}

/** Tela 08: um holerite (multipart). `publicarEm` e `noDiaDoPagamento` nunca vão juntos. */
export async function enviarHolerite(
  funcionarioId: number,
  campos: CamposDoHolerite,
  cliente: ClienteHttp = clienteHttp,
): Promise<Esquema<'HoleriteItemRh'>> {
  const { data } = await cliente.POST('/api/rh/funcionarios/{funcionarioId}/holerites', {
    params: { path: { funcionarioId } },
    ...multipart<Esquema<'HoleriteEnvioComando'>>({
      mes: campos.mes,
      ano: campos.ano,
      tipo: campos.tipo,
      arquivo: campos.arquivo,
      publicarEm: campos.publicarEm,
      noDiaDoPagamento: campos.noDiaDoPagamento,
    }),
  })
  return data ?? semResposta()
}

/** "Substituir holerite": a nova versão (multipart). */
export async function substituirHolerite(
  holeriteId: number,
  campos: { arquivo: File } & Quando,
  cliente: ClienteHttp = clienteHttp,
): Promise<Esquema<'HoleriteItemRh'>> {
  const { data } = await cliente.POST('/api/rh/holerites/{holeriteId}/versoes', {
    params: { path: { holeriteId } },
    ...multipart<Esquema<'HoleriteNovaVersaoComando'>>({
      arquivo: campos.arquivo,
      publicarEm: campos.publicarEm,
      noDiaDoPagamento: campos.noDiaDoPagamento,
    }),
  })
  return data ?? semResposta()
}

export type ItemDoLote = { funcionarioId: number; arquivo: File }

/**
 * Lote: os campos comuns, `itens` em JSON (parte application/json, como o `encoding` do contrato)
 * ligando cada arquivo à pessoa, e um `arquivos` por PDF.
 */
export async function enviarHoleritesEmLote(
  campos: { mes: number; ano: number; tipo: Tipo; itens: readonly ItemDoLote[] } & Quando,
  cliente: ClienteHttp = clienteHttp,
): Promise<Esquema<'HoleriteLoteResultado'>> {
  const itens = campos.itens.map((i) => ({
    funcionarioId: i.funcionarioId,
    nomeDoArquivo: i.arquivo.name,
  }))
  const corpo = {
    mes: campos.mes,
    ano: campos.ano,
    tipo: campos.tipo,
    ...(campos.publicarEm ? { publicarEm: campos.publicarEm } : {}),
    ...(campos.noDiaDoPagamento ? { noDiaDoPagamento: true } : {}),
    itens,
    arquivos: campos.itens.map((i) => i.arquivo.name),
  } satisfies Esquema<'HoleriteLoteComando'>
  const { data } = await cliente.POST('/api/rh/holerites/lote', {
    body: corpo,
    bodySerializer: () => {
      const formulario = new FormData()
      formulario.append('mes', String(corpo.mes))
      formulario.append('ano', String(corpo.ano))
      formulario.append('tipo', corpo.tipo)
      if (corpo.publicarEm) formulario.append('publicarEm', corpo.publicarEm)
      if (corpo.noDiaDoPagamento) formulario.append('noDiaDoPagamento', 'true')
      formulario.append('itens', new Blob([JSON.stringify(itens)], { type: 'application/json' }))
      for (const item of campos.itens) formulario.append('arquivos', item.arquivo)
      return formulario
    },
  })
  return data ?? semResposta()
}
