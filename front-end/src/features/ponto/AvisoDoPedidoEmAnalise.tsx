import { useState } from 'react'
import { $api } from '../../shared/api/api'
import { formatarDiaMesDaData, formatarDiaMesHora } from '../../shared/formato/datas'
import { Aviso } from '../../shared/ui/Aviso'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Janela } from '../../shared/ui/Janela'
import { Selo } from '../../shared/ui/Selo'
import { ChipsDosPeriodos } from './PecasDoPonto'
import { etiquetaDoMeuPedido, respostaDoPedido } from './rotulosDoPonto'

/** "Ver pedido" da tela 32: detalhe só de leitura, num modal sem caminho. */
function ModalDoMeuPedido({ pedidoId, aoFechar }: { pedidoId: number; aoFechar: () => void }) {
  const consulta = $api.useQuery('get', '/api/eu/ponto/pedidos-de-ajuste/{pedidoId}', {
    params: { path: { pedidoId } },
  })
  const pedido = consulta.data
  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo="Pedido de ajuste de ponto"
      rodape={
        <Botao variante="secundario" onClick={aoFechar}>
          Fechar
        </Botao>
      }
    >
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      ) : !pedido ? (
        <Esqueleto className="h-40 w-full" />
      ) : (
        <dl className="flex flex-col gap-4">
          <div className="flex flex-col gap-1">
            <dt className="text-tabela text-texto-suave">Dia do ponto</dt>
            <dd className="font-semibold text-texto">{formatarDiaMesDaData(pedido.data)}</dd>
          </div>
          <div className="flex flex-col gap-1">
            <dt className="text-tabela text-texto-suave">Pedido em</dt>
            <dd className="text-texto">{formatarDiaMesHora(pedido.criadoEmUtc)}</dd>
          </div>
          <div className="flex flex-col gap-1">
            <dt className="text-tabela text-texto-suave">Registro atual</dt>
            <dd>
              <ChipsDosPeriodos periodos={pedido.periodosDoDia} ehHoje={false} />
            </dd>
          </div>
          <div className="flex flex-col gap-1">
            <dt className="text-tabela text-texto-suave">Correção pedida</dt>
            <dd className="rounded-campo bg-sucesso-fundo px-3 py-2 text-sucesso-texto">
              {pedido.resumoDaCorrecao}
            </dd>
          </div>
          <div className="flex flex-col gap-1">
            <dt className="text-tabela text-texto-suave">Justificativa</dt>
            <dd className="rounded-campo bg-superficie-suave px-3 py-2 text-texto">
              “{pedido.justificativa}”
            </dd>
          </div>
          <div className="flex flex-col gap-1">
            <dt className="text-tabela text-texto-suave">Situação</dt>
            <dd className="flex flex-col items-start gap-1">
              <Selo tom={etiquetaDoMeuPedido(pedido.status).tom}>
                {etiquetaDoMeuPedido(pedido.status).texto}
              </Selo>
              <span className="text-pequeno text-texto-suave">{respostaDoPedido(pedido)}</span>
            </dd>
          </div>
        </dl>
      )}
    </Janela>
  )
}

/** A faixa âmbar da tela 32: os pedidos de ajuste da pessoa que estão em análise. */
export function AvisoDoPedidoEmAnalise() {
  const [aberto, setAberto] = useState<number | null>(null)
  const consulta = $api.useQuery('get', '/api/eu/ponto/pedidos-de-ajuste', {
    params: { query: { status: 'PENDENTE' } },
  })
  const pendentes = consulta.data?.itens ?? []
  const primeiro = pendentes[0]
  if (!primeiro) return null
  const total = consulta.data?.total ?? pendentes.length
  const texto =
    total > 1
      ? `Você tem ${total} pedidos de ajuste em análise.`
      : `Seu pedido de ajuste do dia ${formatarDiaMesDaData(primeiro.data)} está em análise ${
          primeiro.etapa === 'GESTOR' ? 'pelo gestor' : 'pelo RH'
        }. Você recebe um e-mail quando houver resposta.`
  return (
    <>
      <Aviso
        tom="alerta"
        acao={
          <Botao variante="texto" tamanho="pequeno" onClick={() => setAberto(primeiro.id)}>
            Ver pedido
          </Botao>
        }
      >
        {texto}
      </Aviso>
      {aberto !== null && <ModalDoMeuPedido pedidoId={aberto} aoFechar={() => setAberto(null)} />}
    </>
  )
}
