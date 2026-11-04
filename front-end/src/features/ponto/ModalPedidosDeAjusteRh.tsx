import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { invalidarContadoresDoMenu } from '../../app/useContadoresDoMenu'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import type { Esquema } from '../../shared/api/tipos'
import { formatarDiaMesDaData, formatarDiaMesHora } from '../../shared/formato/datas'
import { plural } from '../../shared/formato/texto'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Avatar } from '../../shared/ui/Avatar'
import { Botao } from '../../shared/ui/Botao'
import { cn } from '../../shared/ui/estilo'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Janela } from '../../shared/ui/Janela'
import { Paginacao } from '../../shared/ui/Paginacao'
import { Selo } from '../../shared/ui/Selo'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { CampoDaObservacao, MioloDoPedido } from './PartesDoPedido'
import {
  FILA_DO_RH,
  etiquetaDoPedido,
  fraseDoStatusDoPedido,
  rotuloDoDiaDaSemana,
} from './rotulosDoPonto'

type Pedido = Esquema<'PontoPedidoAjuste'>

function diaDaSemanaDaData(data: string): string {
  const [ano, mes, dia] = data.split('-').map(Number)
  const semana = (['DOMINGO', 'SEGUNDA', 'TERCA', 'QUARTA', 'QUINTA', 'SEXTA', 'SABADO'] as const)[
    new Date(Date.UTC(ano ?? 0, (mes ?? 1) - 1, dia ?? 1)).getUTCDay()
  ]
  return semana ? `${rotuloDoDiaDaSemana(semana, true)}, ${formatarDiaMesDaData(data)}` : data
}

function Detalhe({
  pedidoId,
  aoDecidir,
}: {
  pedidoId: number
  aoDecidir: (decidido: number) => void
}) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const consulta = $api.useQuery('get', '/api/rh/ponto/pedidos-de-ajuste/{pedidoId}', {
    params: { path: { pedidoId } },
  })
  const aprovar = $api.useMutation('post', '/api/rh/ponto/pedidos-de-ajuste/{pedidoId}/aprovar')
  const recusar = $api.useMutation('post', '/api/rh/ponto/pedidos-de-ajuste/{pedidoId}/recusar')
  const [observacao, setObservacao] = useState('')
  const [erro, setErro] = useState<unknown>(null)
  const pedido = consulta.data
  const decidindo = aprovar.isPending || recusar.isPending

  async function decidir(qual: 'aprovar' | 'recusar') {
    if (!pedido || decidindo) return
    setErro(null)
    const mutacao = qual === 'aprovar' ? aprovar : recusar
    try {
      await mutacao.mutateAsync({
        params: { path: { pedidoId: pedido.id } },
        body: { observacao: observacao.trim() || null },
      })
      notificacao.sucesso(
        qual === 'aprovar' ? 'Pedido aprovado e ponto corrigido.' : 'Pedido recusado.',
      )
      setObservacao('')
      await Promise.all([
        invalidarRota(queryClient, '/api/rh/ponto/pedidos-de-ajuste'),
        invalidarRota(queryClient, '/api/rh/funcionarios/{funcionarioId}/ponto'),
        invalidarContadoresDoMenu(queryClient),
      ])
      aoDecidir(pedido.id)
    } catch (e) {
      setErro(e)
      if (e instanceof ErroDeApi && e.status === 409) {
        await Promise.all([
          invalidarRota(queryClient, '/api/rh/ponto/pedidos-de-ajuste'),
          consulta.refetch(),
        ])
      }
    }
  }

  if (consulta.error) {
    return <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
  }
  if (!pedido) return <Esqueleto className="h-80 w-full" />
  const etiqueta = etiquetaDoPedido(pedido)
  return (
    <section aria-label={`Pedido de ${pedido.funcionario.nome}`} className="flex flex-col gap-5">
      <div className="flex flex-col gap-1">
        <h3 className="text-t2 font-bold text-texto">{pedido.funcionario.nome}</h3>
        <span className="text-corpo text-texto-suave">
          {[
            pedido.funcionario.cargo,
            pedido.funcionario.setor && `setor ${pedido.funcionario.setor}`,
          ]
            .filter(Boolean)
            .join(', ')}
        </span>
      </div>
      <dl className="grid grid-cols-3 gap-4">
        <div className="flex flex-col gap-1">
          <dt className="text-tabela text-texto-suave">Dia do ponto</dt>
          <dd className="font-semibold text-texto">{diaDaSemanaDaData(pedido.data)}</dd>
        </div>
        <div className="flex flex-col gap-1">
          <dt className="text-tabela text-texto-suave">Pedido em</dt>
          <dd className="text-texto">{formatarDiaMesHora(pedido.criadoEmUtc)}</dd>
        </div>
        <div className="flex flex-col gap-1">
          <dt className="text-tabela text-texto-suave">Gestor(a)</dt>
          <dd className="text-texto">
            {pedido.gestorResponsavel?.nome ?? 'Sem gestor (só o RH decide)'}
          </dd>
        </div>
      </dl>
      <MioloDoPedido pedido={pedido} />
      <div className="flex flex-wrap items-center gap-3">
        <Selo tom={etiqueta.tom}>{etiqueta.texto}</Selo>
        <span className="text-corpo text-texto">{fraseDoStatusDoPedido(pedido)}</span>
      </div>
      {pedido.decisaoGestor?.observacao && (
        <p className="text-pequeno text-texto-suave italic">“{pedido.decisaoGestor.observacao}”</p>
      )}
      <CampoDaObservacao
        pedido={pedido}
        valor={observacao}
        aoMudar={setObservacao}
        desabilitado={!pedido.podeDecidir}
      />
      {!!erro && <AvisoDeErro erro={erro} />}
      <div className="flex flex-wrap items-center gap-3">
        <Botao
          onClick={() => void decidir('aprovar')}
          disabled={!pedido.podeDecidir || recusar.isPending}
          carregando={aprovar.isPending}
        >
          Aprovar e corrigir o ponto
        </Botao>
        <Botao
          variante="perigo"
          onClick={() => void decidir('recusar')}
          disabled={!pedido.podeDecidir || aprovar.isPending}
          carregando={recusar.isPending}
        >
          Recusar
        </Botao>
        {!pedido.podeDecidir && pedido.motivoNaoPodeDecidir && (
          <span className="text-pequeno text-texto-suave">{pedido.motivoNaoPodeDecidir}</span>
        )}
      </div>
    </section>
  )
}

function CartaoDaFila({
  pedido,
  selecionado,
  aoEscolher,
}: {
  pedido: Pedido
  selecionado: boolean
  aoEscolher: () => void
}) {
  const etiqueta = etiquetaDoPedido(pedido)
  return (
    <button
      type="button"
      aria-pressed={selecionado}
      onClick={aoEscolher}
      className={cn(
        'flex w-full items-center gap-3 rounded-cartao border border-borda bg-superficie p-3 text-left',
        selecionado && 'border-primaria bg-superficie-destaque',
      )}
    >
      <Avatar nome={pedido.funcionario.nome} />
      <span className="flex min-w-0 flex-1 flex-col">
        <span className="truncate font-semibold text-texto">{pedido.funcionario.nome}</span>
        <span className="text-pequeno text-texto-suave">
          Dia {formatarDiaMesDaData(pedido.data)}
          {pedido.funcionario.setor ? `, ${pedido.funcionario.setor}` : ''}
        </span>
      </span>
      <Selo tom={etiqueta.tom}>{etiqueta.texto}</Selo>
    </button>
  )
}

/** Tela 13: a fila do RH (mais antigo primeiro) e o detalhe; os botões seguem `podeDecidir`. */
export function ModalPedidosDeAjusteRh({
  pedidoInicial,
  aoFechar,
}: {
  pedidoInicial: number | null
  aoFechar: () => void
}) {
  const [pagina, setPagina] = useState(1)
  const [escolhido, setEscolhido] = useState<number | null>(pedidoInicial)
  const lista = $api.useQuery('get', '/api/rh/ponto/pedidos-de-ajuste', {
    params: { query: { ...FILA_DO_RH, ...(pagina > 1 ? { pagina } : {}) } },
  })
  const itens = lista.data?.itens
  const atual = escolhido ?? itens?.[0]?.id ?? null
  const comRh = lista.data?.resumo.comRh

  function passarAoProximo(decidido: number) {
    if (!itens) return setEscolhido(null)
    const indice = itens.findIndex((p) => p.id === decidido)
    const proximo = itens[indice + 1] ?? itens.find((p) => p.id !== decidido)
    setEscolhido(proximo?.id ?? null)
  }

  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo="Pedidos de ajuste de ponto"
      tamanho="extra"
      rodape={
        <div className="flex w-full items-center justify-between gap-3">
          <span className="text-pequeno text-texto-suave">
            {comRh === undefined
              ? ''
              : `${comRh} ${plural(comRh, 'pedido esperando', 'pedidos esperando')} o RH. O gestor do setor também pode analisar.`}
          </span>
          <Botao variante="secundario" onClick={aoFechar}>
            Fechar
          </Botao>
        </div>
      }
    >
      <div className="grid grid-cols-[360px_1fr] gap-6">
        <div className="flex flex-col gap-3">
          {lista.error ? (
            <AvisoDeErro erro={lista.error} aoTentarDeNovo={() => void lista.refetch()} />
          ) : !itens ? (
            <Esqueleto className="h-60 w-full" />
          ) : itens.length === 0 ? (
            <p className="text-corpo text-texto-suave">Nenhum pedido de ajuste em análise.</p>
          ) : (
            <ul aria-label="Pedidos em análise" className="flex flex-col gap-2">
              {itens.map((p) => (
                <li key={p.id}>
                  <CartaoDaFila
                    pedido={p}
                    selecionado={p.id === atual}
                    aoEscolher={() => setEscolhido(p.id)}
                  />
                </li>
              ))}
            </ul>
          )}
          {lista.data && lista.data.totalPaginas > 1 && (
            <Paginacao dados={lista.data} aoMudar={setPagina} />
          )}
        </div>
        <div>
          {atual !== null ? (
            <Detalhe key={atual} pedidoId={atual} aoDecidir={passarAoProximo} />
          ) : (
            itens && <p className="text-corpo text-texto-suave">Nenhum pedido escolhido.</p>
          )}
        </div>
      </div>
    </Janela>
  )
}
