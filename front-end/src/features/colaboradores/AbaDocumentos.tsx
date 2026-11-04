import { Plus } from 'lucide-react'
import { useState } from 'react'
import { clienteHttp, $api } from '../../shared/api/api'
import type { Esquema } from '../../shared/api/tipos'
import { useBaixarPorLink } from '../../shared/arquivos/useBaixarPorLink'
import { formatarData, formatarDataDoInstante, formatarDiaMes } from '../../shared/formato/datas'
import { primeiroNome } from '../../shared/formato/texto'
import { Botao } from '../../shared/ui/Botao'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Selo } from '../../shared/ui/Selo'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { ModalAdicionarDocumento } from '../documentos/ModalAdicionarDocumento'
import { etiquetaDoDocumento, rotuloDoTipoDeDocumento } from '../documentos/rotulos'
import type { PropsDaAba } from './abasDaFicha'

type Documento = Esquema<'DocumentoColaborador'>

export function AbaDocumentos({ ficha }: PropsDaAba) {
  const [adicionando, setAdicionando] = useState(false)
  const { baixar, baixando } = useBaixarPorLink()
  const consulta = $api.useQuery('get', '/api/rh/funcionarios/{funcionarioId}/documentos', {
    params: { path: { funcionarioId: ficha.id } },
  })

  const colunas: ColunaDaTabela<Documento>[] = [
    {
      chave: 'documento',
      titulo: 'Documento',
      celula: (d) => (
        <div className="flex flex-col gap-0.5">
          <span className="font-semibold text-texto">{rotuloDoTipoDeDocumento(d.tipo)}</span>
          {d.descricao && <span className="text-pequeno text-texto-suave">{d.descricao}</span>}
          {d.dataValidade && (
            <span className="text-pequeno text-texto-suave">{`Válido até ${formatarData(d.dataValidade)}`}</span>
          )}
        </div>
      ),
    },
    {
      chave: 'enviado',
      titulo: 'Enviado em',
      celula: (d) =>
        d.situacao === 'PENDENTE'
          ? `Atualização pedida em ${formatarDiaMes(d.pedidoEmUtc)}`
          : formatarDataDoInstante(d.enviadoEmUtc),
    },
    {
      chave: 'situacao',
      titulo: 'Situação',
      celula: (d) => {
        const { texto, tom } = etiquetaDoDocumento(d.situacao)
        return <Selo tom={tom}>{texto}</Selo>
      },
    },
    {
      chave: 'acao',
      titulo: 'Ação',
      alinhamento: 'direita',
      celula: (d) =>
        d.situacao === 'ENVIADO' ? (
          <Botao
            variante="secundario"
            tamanho="pequeno"
            disabled={baixando}
            onClick={() =>
              void baixar(() =>
                clienteHttp.GET(
                  '/api/rh/funcionarios/{funcionarioId}/documentos/{documentoId}/download',
                  { params: { path: { funcionarioId: ficha.id, documentoId: d.id } } },
                ),
              )
            }
          >
            Ver
          </Botao>
        ) : null,
    },
  ]

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-4">
        <p className="text-corpo text-texto-suave">
          Documentos guardados na ficha. Só o RH e o próprio colaborador conseguem ver.
        </p>
        <Botao icone={Plus} onClick={() => setAdicionando(true)}>
          Adicionar documento
        </Botao>
      </div>
      <Tabela
        rotulo="Documentos"
        colunas={colunas}
        linhas={consulta.isError ? undefined : consulta.data?.itens}
        chaveDaLinha={(d) => d.id}
        carregando={consulta.isFetching}
        erro={consulta.error ?? undefined}
        aoTentarDeNovo={() => void consulta.refetch()}
        vazio={<EstadoVazio titulo="Nenhum documento na ficha ainda." />}
      />
      <ModalAdicionarDocumento
        funcionarioId={ficha.id}
        primeiroNome={primeiroNome(ficha.nome)}
        aberta={adicionando}
        aoFechar={() => setAdicionando(false)}
      />
    </div>
  )
}
