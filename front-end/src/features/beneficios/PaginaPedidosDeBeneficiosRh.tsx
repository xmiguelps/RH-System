import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { invalidarContadoresDoMenu } from '../../app/useContadoresDoMenu'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { formatarData, formatarDataDoInstante, hojeEmSaoPaulo } from '../../shared/formato/datas'
import { FiltroDeSelecao } from '../../shared/lista/FiltroDeSelecao'
import { inteiroDaUrl } from '../../shared/lista/valoresDeLista'
import { AreaDeTexto } from '../../shared/ui/AreaDeTexto'
import { Aviso } from '../../shared/ui/Aviso'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { Campo } from '../../shared/ui/Campo'
import { CampoDeBusca } from '../../shared/ui/CampoDeBusca'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Janela } from '../../shared/ui/Janela'
import { Paginacao } from '../../shared/ui/Paginacao'
import { Selo } from '../../shared/ui/Selo'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { SITUACAO_DO_PEDIDO_DE_BENEFICIO } from './rotulosDosBeneficios'

type Adesao = Esquema<'BeneficioAdesaoRh'>
type Situacao = Esquema<'BeneficioAdesaoSituacao'>

const TAMANHO_DA_PAGINA = 8

/** O que o RH está decidindo: a adesão inteira ou um dependente ou um trajeto dela. */
type Alvo =
  | { tipo: 'adesao'; adesaoId: number; titulo: string }
  | { tipo: 'dependente'; adesaoId: number; dependenteId: number; titulo: string }
  | { tipo: 'trajeto'; adesaoId: number; trajetoId: number; titulo: string }

function ModalDeRecusa({
  alvo,
  aoFechar,
  aoConfirmar,
}: {
  alvo: Alvo | null
  aoFechar: () => void
  aoConfirmar: (motivo: string) => Promise<unknown>
}) {
  return (
    <Janela aberta={alvo !== null} aoFechar={aoFechar} titulo="Recusar pedido de benefício">
      {alvo && (
        <CorpoDaRecusa
          key={`${alvo.tipo}-${alvo.titulo}`}
          alvo={alvo}
          aoFechar={aoFechar}
          aoConfirmar={aoConfirmar}
        />
      )}
    </Janela>
  )
}

function CorpoDaRecusa({
  alvo,
  aoFechar,
  aoConfirmar,
}: {
  alvo: Alvo
  aoFechar: () => void
  aoConfirmar: (motivo: string) => Promise<unknown>
}) {
  const [motivo, setMotivo] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  async function confirmar() {
    const texto = motivo.trim()
    if (!texto) return setErro('Escreva o motivo da recusa.')
    setEnviando(true)
    try {
      const falha = await aoConfirmar(texto)
      if (falha) setErro(mensagemDeErro(falha))
    } finally {
      setEnviando(false)
    }
  }
  return (
    <div className="flex flex-col gap-4">
      <p className="text-corpo text-texto-suave">
        {alvo.titulo}. A pessoa vê o motivo em Seus pedidos.
      </p>
      <Campo rotulo="Motivo" erro={erro}>
        <AreaDeTexto
          rows={4}
          limite={500}
          value={motivo}
          onChange={(e) => setMotivo(e.target.value)}
        />
      </Campo>
      <div className="flex justify-end gap-3">
        <Botao variante="secundario" onClick={aoFechar} disabled={enviando}>
          Cancelar
        </Botao>
        <Botao variante="perigo" onClick={() => void confirmar()} carregando={enviando}>
          Recusar
        </Botao>
      </div>
    </div>
  )
}

function CartaoDaAdesao({
  adesao,
  aoDecidir,
  aoPedirRecusa,
  erro,
}: {
  adesao: Adesao
  aoDecidir: (alvo: Alvo) => void
  aoPedirRecusa: (alvo: Alvo) => void
  erro: string | null
}) {
  const hoje = hojeEmSaoPaulo()
  const pendente = adesao.situacao === 'PENDENTE'
  const atrasado =
    pendente && adesao.respostaPrevistaAte !== null && adesao.respostaPrevistaAte < hoje
  const tituloDaAdesao = `${adesao.beneficio.nome} de ${adesao.funcionario.nome}`
  return (
    <li
      aria-label={`Benefício ${tituloDaAdesao}`}
      className="flex flex-col gap-3 rounded-cartao border border-borda bg-superficie p-5"
    >
      <div className="flex flex-wrap items-center justify-between gap-2">
        <Selo tom="info">{adesao.beneficio.nome}</Selo>
        <Selo tom={SITUACAO_DO_PEDIDO_DE_BENEFICIO[adesao.situacao].tom}>
          {SITUACAO_DO_PEDIDO_DE_BENEFICIO[adesao.situacao].texto}
        </Selo>
      </div>
      <p className="text-corpo text-texto">
        <span className="font-semibold">{adesao.funcionario.nome}</span>
        {adesao.funcionario.cargo || adesao.funcionario.setor
          ? ` (${[adesao.funcionario.cargo, adesao.funcionario.setor].filter(Boolean).join(', ')})`
          : ''}
      </p>
      <ul className="flex flex-col gap-1 text-tabela text-texto-suave">
        <li>
          {adesao.pedidoEmUtc
            ? `Pedido em ${formatarDataDoInstante(adesao.pedidoEmUtc)}`
            : 'Dado pelo RH, sem pedido'}
        </li>
        {adesao.respostaPrevistaAte && pendente && (
          <li className={atrasado ? 'font-semibold text-erro-texto' : undefined}>
            Responder até {formatarData(adesao.respostaPrevistaAte)}
          </li>
        )}
        {adesao.dataInicioDesejada && <li>Começar em {formatarData(adesao.dataInicioDesejada)}</li>}
        <li>Autoriza o desconto em folha: {adesao.autorizaDescontoEmFolha ? 'sim' : 'não'}</li>
        {adesao.motivoRecusa && <li>Motivo da recusa: {adesao.motivoRecusa}</li>}
      </ul>

      {adesao.dependentes.length > 0 && (
        <ul aria-label="Dependentes" className="flex flex-col gap-2 border-t border-divisor pt-3">
          {adesao.dependentes.map((d) => (
            <li
              key={d.id}
              className="flex flex-wrap items-center justify-between gap-2 text-tabela text-texto"
            >
              <span>
                {d.nome}, {d.parentesco}, nascido em {formatarData(d.dataNascimento)}
              </span>
              <span className="flex items-center gap-2">
                <Selo tom={SITUACAO_DO_PEDIDO_DE_BENEFICIO[d.situacao].tom}>
                  {SITUACAO_DO_PEDIDO_DE_BENEFICIO[d.situacao].texto}
                </Selo>
                {d.situacao === 'PENDENTE' && adesao.situacao === 'APROVADO' && (
                  <>
                    <Botao
                      tamanho="pequeno"
                      aria-label={`Aprovar o dependente ${d.nome}`}
                      onClick={() =>
                        aoDecidir({
                          tipo: 'dependente',
                          adesaoId: adesao.adesaoId,
                          dependenteId: d.id,
                          titulo: d.nome,
                        })
                      }
                    >
                      Aprovar
                    </Botao>
                    <Botao
                      variante="perigo"
                      tamanho="pequeno"
                      aria-label={`Recusar o dependente ${d.nome}`}
                      onClick={() =>
                        aoPedirRecusa({
                          tipo: 'dependente',
                          adesaoId: adesao.adesaoId,
                          dependenteId: d.id,
                          titulo: `Dependente ${d.nome}`,
                        })
                      }
                    >
                      Recusar
                    </Botao>
                  </>
                )}
              </span>
            </li>
          ))}
        </ul>
      )}

      {adesao.trajetos.length > 0 && (
        <ul aria-label="Trajetos" className="flex flex-col gap-2 border-t border-divisor pt-3">
          {adesao.trajetos.map((t) => (
            <li
              key={t.id}
              className="flex flex-wrap items-center justify-between gap-2 text-tabela text-texto"
            >
              <span>
                De {t.origem} para {t.destino}: {t.linhasMeios}
              </span>
              <span className="flex items-center gap-2">
                <Selo tom={SITUACAO_DO_PEDIDO_DE_BENEFICIO[t.situacao].tom}>
                  {SITUACAO_DO_PEDIDO_DE_BENEFICIO[t.situacao].texto}
                </Selo>
                {t.situacao === 'PENDENTE' && adesao.situacao === 'APROVADO' && (
                  <>
                    <Botao
                      tamanho="pequeno"
                      aria-label={`Aprovar o trajeto de ${adesao.funcionario.nome}`}
                      onClick={() =>
                        aoDecidir({
                          tipo: 'trajeto',
                          adesaoId: adesao.adesaoId,
                          trajetoId: t.id,
                          titulo: 'Trajeto',
                        })
                      }
                    >
                      Aprovar
                    </Botao>
                    <Botao
                      variante="perigo"
                      tamanho="pequeno"
                      aria-label={`Recusar o trajeto de ${adesao.funcionario.nome}`}
                      onClick={() =>
                        aoPedirRecusa({
                          tipo: 'trajeto',
                          adesaoId: adesao.adesaoId,
                          trajetoId: t.id,
                          titulo: `Trajeto de ${adesao.funcionario.nome}`,
                        })
                      }
                    >
                      Recusar
                    </Botao>
                  </>
                )}
              </span>
            </li>
          ))}
        </ul>
      )}

      {erro && <Aviso tom="erro">{erro}</Aviso>}
      {pendente && (
        <>
          <div className="flex flex-wrap gap-2">
            <Botao
              tamanho="pequeno"
              aria-label={`Aprovar o pedido de ${tituloDaAdesao}`}
              onClick={() =>
                aoDecidir({ tipo: 'adesao', adesaoId: adesao.adesaoId, titulo: tituloDaAdesao })
              }
            >
              Aprovar
            </Botao>
            <Botao
              variante="perigo"
              tamanho="pequeno"
              aria-label={`Recusar o pedido de ${tituloDaAdesao}`}
              onClick={() =>
                aoPedirRecusa({ tipo: 'adesao', adesaoId: adesao.adesaoId, titulo: tituloDaAdesao })
              }
            >
              Recusar
            </Botao>
          </div>
          {(adesao.dependentes.some((d) => d.situacao === 'PENDENTE') ||
            adesao.trajetos.some((t) => t.situacao === 'PENDENTE')) && (
            <p className="text-pequeno text-texto-suave">
              Aprovar o pedido aprova junto os dependentes e o trajeto que vieram com ele. Para
              recusar só um deles, aprove o pedido e depois recuse o que não vale.
            </p>
          )}
        </>
      )}
    </li>
  )
}

/** Tela 7.3: os pedidos de benefícios para o RH (`/rh/pedidos-de-beneficios`). */
export function PaginaPedidosDeBeneficiosRh() {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const [busca, definirParametros] = useSearchParams()
  const situacao =
    (['PENDENTE', 'APROVADO', 'RECUSADO', 'CANCELADO'] as const).find(
      (s) => s === busca.get('situacao'),
    ) ?? 'PENDENTE'
  const beneficioId = inteiroDaUrl(busca.get('beneficioId') ?? undefined)
  const texto = busca.get('busca') ?? ''
  const pagina = inteiroDaUrl(busca.get('pagina') ?? undefined) ?? 1
  const [recusando, setRecusando] = useState<Alvo | null>(null)
  const [erros, setErros] = useState<Record<number, string>>({})

  const lista = $api.useQuery('get', '/api/rh/beneficios-adesoes', {
    params: {
      query: {
        situacao,
        ...(beneficioId ? { beneficioId } : {}),
        ...(texto ? { busca: texto } : {}),
        pagina,
        tamanhoPagina: TAMANHO_DA_PAGINA,
      },
    },
  })
  const catalogo = $api.useQuery('get', '/api/rh/beneficios', {
    params: { query: { pagina: 1, tamanhoPagina: 100 } },
  })
  const aprovarAdesao = $api.useMutation('post', '/api/rh/beneficios-adesoes/{adesaoId}/aprovar')
  const recusarAdesao = $api.useMutation('post', '/api/rh/beneficios-adesoes/{adesaoId}/recusar')
  const aprovarDependente = $api.useMutation(
    'post',
    '/api/rh/beneficios-adesoes/{adesaoId}/dependentes/{dependenteId}/aprovar',
  )
  const recusarDependente = $api.useMutation(
    'post',
    '/api/rh/beneficios-adesoes/{adesaoId}/dependentes/{dependenteId}/recusar',
  )
  const aprovarTrajeto = $api.useMutation(
    'post',
    '/api/rh/beneficios-adesoes/{adesaoId}/trajetos/{trajetoId}/aprovar',
  )
  const recusarTrajeto = $api.useMutation(
    'post',
    '/api/rh/beneficios-adesoes/{adesaoId}/trajetos/{trajetoId}/recusar',
  )
  const dados = lista.data

  function mudar(chave: string, valor: string | null, substituir = false) {
    definirParametros(
      (atual) => {
        const novo = new URLSearchParams(atual)
        if (valor) novo.set(chave, valor)
        else novo.delete(chave)
        if (chave !== 'pagina') novo.delete('pagina')
        return novo
      },
      { replace: substituir },
    )
  }

  async function recarregar() {
    await Promise.all([
      invalidarRota(queryClient, '/api/rh/'),
      invalidarContadoresDoMenu(queryClient),
    ])
  }

  /** Devolve o erro (para a janela mostrar) ou undefined; 409 e 404 mostram o texto e recarregam (outra pessoa decidiu antes). */
  async function decidir(alvo: Alvo, motivo?: string): Promise<unknown> {
    setErros((e) => ({ ...e, [alvo.adesaoId]: '' }))
    try {
      if (alvo.tipo === 'adesao') {
        if (motivo === undefined)
          await aprovarAdesao.mutateAsync({ params: { path: { adesaoId: alvo.adesaoId } } })
        else
          await recusarAdesao.mutateAsync({
            params: { path: { adesaoId: alvo.adesaoId } },
            body: { motivo },
          })
      } else if (alvo.tipo === 'dependente') {
        const path = { adesaoId: alvo.adesaoId, dependenteId: alvo.dependenteId }
        if (motivo === undefined) await aprovarDependente.mutateAsync({ params: { path } })
        else await recusarDependente.mutateAsync({ params: { path }, body: { motivo } })
      } else {
        const path = { adesaoId: alvo.adesaoId, trajetoId: alvo.trajetoId }
        if (motivo === undefined) await aprovarTrajeto.mutateAsync({ params: { path } })
        else await recusarTrajeto.mutateAsync({ params: { path }, body: { motivo } })
      }
      notificacao.sucesso(motivo === undefined ? 'Pedido aprovado.' : 'Pedido recusado.')
      setRecusando(null)
      await recarregar()
      return undefined
    } catch (e) {
      if (e instanceof ErroDeApi && (e.status === 409 || e.status === 404)) {
        notificacao.erro(mensagemDeErro(e))
        setRecusando(null)
        await recarregar()
        return undefined
      }
      if (motivo === undefined)
        setErros((atual) => ({ ...atual, [alvo.adesaoId]: mensagemDeErro(e) }))
      return e
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo="Pedidos de benefícios"
        subtitulo="O RH responde em até 5 dias úteis"
      />
      <div className="grid grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1fr)] items-end gap-4">
        <CampoDeBusca
          rotulo="Buscar colaborador"
          placeholder="Buscar colaborador"
          valor={texto}
          aoMudar={(t) => mudar('busca', t.trim() || null, true)}
        />
        <FiltroDeSelecao
          rotulo="Benefício"
          todos="Todos os benefícios"
          opcoes={(catalogo.data?.itens ?? []).map((b) => ({
            valor: String(b.id),
            rotulo: b.nome,
          }))}
          valor={beneficioId ? String(beneficioId) : undefined}
          aoMudar={(v) => mudar('beneficioId', v)}
          carregando={catalogo.isLoading}
        />
        <FiltroDeSelecao<Situacao>
          rotulo="Situação"
          todos="Pendente"
          opcoes={(['APROVADO', 'RECUSADO', 'CANCELADO'] as const).map((s) => ({
            valor: s,
            rotulo: SITUACAO_DO_PEDIDO_DE_BENEFICIO[s].texto,
          }))}
          valor={situacao === 'PENDENTE' ? undefined : situacao}
          aoMudar={(v) => mudar('situacao', v)}
        />
      </div>
      {lista.error ? (
        <AvisoDeErro erro={lista.error} aoTentarDeNovo={() => void lista.refetch()} />
      ) : !dados ? (
        <Esqueleto className="h-40 w-full" />
      ) : dados.itens.length === 0 ? (
        <EstadoVazio
          titulo={
            texto || beneficioId
              ? 'Nenhum pedido de benefício encontrado para este filtro.'
              : situacao === 'PENDENTE'
                ? 'Nenhum pedido de benefício esperando o RH.'
                : 'Nenhum pedido de benefício nesta situação.'
          }
        />
      ) : (
        <ul aria-label="Pedidos de benefícios" className="grid grid-cols-2 gap-4">
          {dados.itens.map((adesao) => (
            <CartaoDaAdesao
              key={adesao.adesaoId}
              adesao={adesao}
              erro={erros[adesao.adesaoId] || null}
              aoDecidir={(alvo) => void decidir(alvo)}
              aoPedirRecusa={setRecusando}
            />
          ))}
        </ul>
      )}
      {dados && dados.total > 0 && (
        <Paginacao dados={dados} aoMudar={(p) => mudar('pagina', p > 1 ? String(p) : null)} />
      )}
      <ModalDeRecusa
        alvo={recusando}
        aoFechar={() => setRecusando(null)}
        aoConfirmar={(motivo) => decidir(recusando!, motivo)}
      />
    </div>
  )
}
