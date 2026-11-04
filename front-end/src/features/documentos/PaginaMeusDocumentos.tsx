import { useState } from 'react'
import { $api, clienteHttp } from '../../shared/api/api'
import type { Esquema } from '../../shared/api/tipos'
import { useBaixarPorLink } from '../../shared/arquivos/useBaixarPorLink'
import { formatarData, formatarDataDoInstante, formatarDiaMes } from '../../shared/formato/datas'
import { Botao } from '../../shared/ui/Botao'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Selo } from '../../shared/ui/Selo'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { ModalEnviarMeuDocumento } from './ModalEnviarMeuDocumento'
import { etiquetaDoDocumento, rotuloDoTipoDeDocumento } from './rotulos'

type Documento = Esquema<'DocumentoColaborador'>

export function PaginaMeusDocumentos() {
  const consulta = $api.useQuery('get', '/api/eu/documentos')
  const { baixar, baixando } = useBaixarPorLink()
  const [enviando, setEnviando] = useState<Documento | null>(null)

  const colunas: ColunaDaTabela<Documento>[] = [
    {
      chave: 'documento',
      titulo: 'Documento',
      celula: (d) => (
        <div className="flex flex-col gap-0.5">
          <span className="font-semibold text-texto">{rotuloDoTipoDeDocumento(d.tipo)}</span>
          {d.descricao && <span className="text-pequeno text-texto-suave">{d.descricao}</span>}
        </div>
      ),
    },
    { chave: 'validade', titulo: 'Validade', celula: (d) => formatarData(d.dataValidade) },
    {
      chave: 'situacao',
      titulo: 'Situação',
      celula: (d) => {
        const { texto, tom } = etiquetaDoDocumento(d.situacao)
        return <Selo tom={tom}>{texto}</Selo>
      },
    },
    {
      chave: 'data',
      titulo: 'Data',
      celula: (d) =>
        d.situacao === 'PENDENTE'
          ? `Pedido em ${formatarDiaMes(d.pedidoEmUtc)}`
          : `Enviado em ${formatarDataDoInstante(d.enviadoEmUtc)}`,
    },
    {
      chave: 'acao',
      titulo: 'Ação',
      alinhamento: 'direita',
      celula: (d) =>
        d.situacao === 'PENDENTE' ? (
          <Botao tamanho="pequeno" onClick={() => setEnviando(d)}>
            Enviar arquivo
          </Botao>
        ) : (
          <Botao
            variante="secundario"
            tamanho="pequeno"
            disabled={baixando}
            onClick={() =>
              void baixar(() =>
                clienteHttp.GET('/api/eu/documentos/{documentoId}/download', {
                  params: { path: { documentoId: d.id } },
                }),
              )
            }
          >
            Ver
          </Botao>
        ),
    },
  ]

  return (
    <>
      <CabecalhoDePagina
        titulo="Meus documentos"
        subtitulo="Os documentos que o RH guarda na sua ficha e os que ele pediu para você enviar."
      />
      <Tabela
        rotulo="Meus documentos"
        colunas={colunas}
        linhas={consulta.isError ? undefined : consulta.data?.itens}
        chaveDaLinha={(d) => d.id}
        carregando={consulta.isFetching}
        erro={consulta.error ?? undefined}
        aoTentarDeNovo={() => void consulta.refetch()}
        vazio={<EstadoVazio titulo="Você ainda não tem documentos na ficha." />}
      />
      {enviando && (
        <ModalEnviarMeuDocumento documento={enviando} aberta aoFechar={() => setEnviando(null)} />
      )}
    </>
  )
}
