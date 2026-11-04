import { useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { Outlet, useNavigate } from 'react-router'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { formatarData, formatarDataDoInstante } from '../../shared/formato/datas'
import { formatarNumero } from '../../shared/formato/numeros'
import { Aviso } from '../../shared/ui/Aviso'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Janela } from '../../shared/ui/Janela'
import { Selo } from '../../shared/ui/Selo'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { Progresso } from '../../shared/ui/Progresso'
import { NumerosDoPonto } from '../ponto/PecasDoPonto'
import { CamposDasFerias } from './CamposDasFerias'
import {
  comoFunciona,
  frasesDaDecisao,
  periodoCompleto,
  periodoCurto,
  SITUACAO_DAS_FERIAS,
  TEXTO_DA_RECUSA_DA_SUGESTAO,
  textoDeDias,
} from './rotulosDasFerias'
import {
  comandoDaConferencia,
  FERIAS_VAZIAS,
  useConferenciaDeFerias,
  type DadosDasFerias,
} from './useConferenciaDeFerias'

type Pedido = Esquema<'FeriasPedido'>
type Periodo = Esquema<'FeriasPeriodoAquisitivo'>
type Regra = Esquema<'FeriasRegraConferida'>

function regrasDoErro(erro: unknown): Regra[] | undefined {
  if (erro instanceof ErroDeApi && Array.isArray(erro.corpo?.regras)) return erro.corpo.regras as Regra[]
  return undefined
}

function BlocoDaSugestao({ pedido }: { pedido: Pedido }) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const aceitar = $api.useMutation('post', '/api/eu/ferias/{feriasId}/sugestao/aceitar')
  const recusar = $api.useMutation('post', '/api/eu/ferias/{feriasId}/sugestao/recusar')
  const [erro, setErro] = useState<unknown>(null)
  const [encerrado, setEncerrado] = useState(false)
  const s = pedido.sugestao!
  const params = { params: { path: { feriasId: pedido.id } } }

  async function responder(qual: 'aceitar' | 'recusar') {
    setErro(null)
    try {
      if (qual === 'aceitar') {
        await aceitar.mutateAsync(params)
        notificacao.sucesso('Datas aceitas. Agora o RH confirma.')
      } else {
        await recusar.mutateAsync(params)
        setEncerrado(true)
      }
      await invalidarRota(queryClient, '/api/eu/ferias')
    } catch (e) {
      setErro(e)
      if (e instanceof ErroDeApi && e.status === 409) await invalidarRota(queryClient, '/api/eu/ferias')
    }
  }

  if (encerrado) return <Aviso tom="info">{TEXTO_DA_RECUSA_DA_SUGESTAO}</Aviso>
  const regras = regrasDoErro(erro)
  return (
    <section aria-label="Sugestão de outra data" className="flex flex-col gap-3 rounded-cartao border-2 border-alerta-ponto bg-superficie p-5">
      <p className="text-corpo text-texto">
        {s.sugeridaPor.nome} sugeriu outras datas: {periodoCurto(s.dataInicio, s.dataFim)} ({textoDeDias(s.dias)}).
        Responda até {formatarData(s.expiraEm)}.
      </p>
      {!!erro && <AvisoDeErro erro={erro} />}
      {regras && (
        <ul className="list-disc pl-6 text-tabela text-texto">
          {regras.map((r) => (
            <li key={r.codigo}>{r.mensagem}</li>
          ))}
        </ul>
      )}
      <div className="flex gap-3">
        <Botao onClick={() => void responder('aceitar')} carregando={aceitar.isPending} disabled={recusar.isPending}>
          Aceitar as novas datas
        </Botao>
        <Botao variante="secundario" onClick={() => void responder('recusar')} carregando={recusar.isPending} disabled={aceitar.isPending}>
          Recusar
        </Botao>
      </div>
    </section>
  )
}

function CartaoDoPeriodo({ p }: { p: Periodo }) {
  const total = Math.max(p.diasDeDireito, 1)
  return (
    <li className="flex flex-col gap-2 rounded-cartao border border-borda bg-superficie p-4">
      <div className="flex items-center justify-between gap-2">
        <span className="font-semibold text-texto">{periodoCompleto(p.inicio, p.fim)}</span>
        {p.situacao === 'EM_AQUISICAO' ? (
          <Selo tom="info">Em andamento</Selo>
        ) : (
          p.diasDisponiveis > 0 && <Selo tom="alerta">Tire até {formatarData(p.prazoParaTirar)}</Selo>
        )}
      </div>
      {p.situacao === 'EM_AQUISICAO' ? (
        <>
          <Progresso valor={p.mesesTrabalhados ?? 0} maximo={12} rotulo={`Meses trabalhados do período ${periodoCompleto(p.inicio, p.fim)}`} />
          <p className="text-pequeno text-texto-suave">
            {p.mesesTrabalhados ?? 0} de 12 meses trabalhados. Os 30 dias desse período ficam disponíveis a
            partir de {formatarData(p.disponivelAPartirDe)}.
          </p>
        </>
      ) : (
        <>
          <Progresso valor={p.diasTirados} maximo={total} rotulo={`Dias tirados do período ${periodoCompleto(p.inicio, p.fim)}`} />
          <p className="text-pequeno text-texto-suave">
            {textoDeDias(p.diasTirados)} {p.diasTirados === 1 ? 'tirado' : 'tirados'} e {textoDeDias(p.diasDisponiveis)}{' '}
            {p.diasDisponiveis === 1 ? 'disponível' : 'disponíveis'}
            {p.diasEmPedido > 0 ? ` (${p.diasEmPedido} reservados em pedido)` : ''}
          </p>
        </>
      )}
    </li>
  )
}

/** Tela 35: o painel "Pedir férias" (/eu/ferias/pedir). Nenhuma regra é reimplementada aqui. */
export function PainelPedirFerias() {
  const navegar = useNavigate()
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const minhas = $api.useQuery('get', '/api/eu/ferias')
  const pedir = $api.useMutation('post', '/api/eu/ferias')
  const [valores, setValores] = useState<DadosDasFerias>(FERIAS_VAZIAS)
  const [erro, setErro] = useState<unknown>(null)
  const { conferencia, conferindo, erro: erroDaConferencia } = useConferenciaDeFerias({ tipo: 'eu' }, valores)
  const fechar = () => navegar('/eu/ferias')
  const resumo = minhas.data?.resumo
  const gestor = minhas.data?.gestorQueAnalisa ?? null

  async function enviar() {
    if (pedir.isPending || !conferencia?.podeEnviar) return
    setErro(null)
    try {
      await pedir.mutateAsync({
        body: {
          ...comandoDaConferencia(valores),
          adiantar13Salario: valores.adiantar13Salario,
          mensagem: valores.texto.trim() || null,
        },
      })
      notificacao.sucesso('Pedido de férias enviado.')
      await Promise.all([invalidarRota(queryClient, '/api/eu/ferias'), invalidarRota(queryClient, '/api/eu/painel')])
      fechar()
    } catch (e) {
      setErro(e)
    }
  }

  return (
    <Janela
      aberta
      aoFechar={fechar}
      titulo="Pedir férias"
      posicao="direita"
      largura="media"
      rodape={
        <div className="flex w-full flex-col gap-3">
          <p className="text-pequeno text-texto-suave">
            {gestor
              ? `${gestor.nome} analisa primeiro e depois o RH confirma. Você recebe um e-mail a cada decisão.`
              : 'O RH analisa o pedido. Você recebe um e-mail a cada decisão.'}
          </p>
          <div className="flex justify-end gap-3">
            <Botao variante="secundario" onClick={fechar}>
              Cancelar
            </Botao>
            <Botao onClick={() => void enviar()} disabled={!conferencia?.podeEnviar || conferindo} carregando={pedir.isPending}>
              Enviar pedido
            </Botao>
          </div>
        </div>
      }
    >
      <div className="flex flex-col gap-5">
        {resumo && (
          <Aviso tom="info">
            Você tem {textoDeDias(resumo.diasDisponiveis)} {resumo.diasDisponiveis === 1 ? 'disponível' : 'disponíveis'}
            {resumo.prazoParaTirarSaldo ? ` para tirar até ${formatarData(resumo.prazoParaTirarSaldo)}` : ''}.
          </Aviso>
        )}
        {!!erro && !regrasDoErro(erro) && <AvisoDeErro erro={erro} />}
        {!!erro && regrasDoErro(erro) && <Aviso tom="erro">{mensagemDeErro(erro)}</Aviso>}
        <CamposDasFerias
          valores={valores}
          aoMudar={(v) => {
            setErro(null)
            setValores(v)
          }}
          conferencia={conferencia}
          conferindo={conferindo}
          erroDaConferencia={erroDaConferencia}
          rotuloDoTexto={gestor ? `Mensagem para ${gestor.nome} (opcional)` : 'Mensagem para o RH (opcional)'}
          exemploDoTexto="Ex.: queria emendar com o feriado de 20/11"
          comSobra
          regrasDoErro={regrasDoErro(erro)}
        />
      </div>
    </Janela>
  )
}

/** Telas 34 e 48 (a mesma para os três perfis): saldo, períodos, histórico e "Como funciona". */
export function PaginaMinhasFerias() {
  const consulta = $api.useQuery('get', '/api/eu/ferias')
  const d = consulta.data
  const r = d?.resumo
  const sugestoes = (d?.historico ?? []).filter((p) => p.situacao === 'SUGESTAO_DE_DATA' && p.sugestao)
  const colunas: readonly ColunaDaTabela<Pedido>[] = [
    {
      chave: 'periodo',
      titulo: 'Período',
      celula: (p) => (
        <span className="flex flex-col gap-1">
          <span>{periodoCompleto(p.dataInicio, p.dataFim)}</span>
          {frasesDaDecisao(p).map((f) => (
            <span key={f} className="text-pequeno text-texto-suave">
              {f}
            </span>
          ))}
        </span>
      ),
    },
    { chave: 'dias', titulo: 'Dias', celula: (p) => textoDeDias(p.dias) },
    { chave: 'pedido', titulo: 'Pedido em', celula: (p) => formatarDataDoInstante(p.solicitadaEmUtc) },
    {
      chave: 'situacao',
      titulo: 'Situação',
      celula: (p) => <Selo tom={SITUACAO_DAS_FERIAS[p.situacao].tom}>{SITUACAO_DAS_FERIAS[p.situacao].texto}</Selo>,
    },
  ]
  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo="Férias"
        subtitulo="Seu saldo, seus períodos e seus pedidos"
        acoes={
          <BotaoLink to="pedir">
            <Plus aria-hidden="true" className="h-4 w-4" />
            Pedir férias
          </BotaoLink>
        }
      />
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      ) : (
        <NumerosDoPonto
          rotulo="Números das férias"
          carregando={!r}
          itens={[
            {
              rotulo: 'Dias disponíveis',
              valor: r ? textoDeDias(r.diasDisponiveis) : '—',
              apoio: r?.periodoDoSaldo
                ? `Do período ${r.periodoDoSaldo.inicio.slice(0, 4)} a ${r.periodoDoSaldo.fim.slice(0, 4)}`
                : undefined,
            },
            { rotulo: 'Prazo para tirar esse saldo', valor: r?.prazoParaTirarSaldo ? formatarData(r.prazoParaTirarSaldo) : '—' },
            {
              rotulo: 'Próximas férias',
              valor: r?.proximasFerias ? periodoCurto(r.proximasFerias.dataInicio, r.proximasFerias.dataFim) : 'Nenhuma',
              apoio: r?.proximasFerias ? textoDeDias(r.proximasFerias.dias) : 'Nada agendado ainda',
            },
            {
              rotulo: 'Pedidos em análise',
              valor: r ? formatarNumero(r.pedidosEmAnalise) : '—',
              tom: r && r.pedidosEmAnalise > 0 ? 'alerta' : 'neutro',
            },
          ]}
        />
      )}
      {sugestoes.map((p) => (
        <BlocoDaSugestao key={p.id} pedido={p} />
      ))}
      <div className="grid grid-cols-[minmax(0,2fr)_minmax(0,1fr)] items-start gap-6">
        <div className="flex flex-col gap-6">
          <section aria-labelledby="titulo-periodos" className="flex flex-col gap-3">
            <h2 id="titulo-periodos" className="text-t3 font-bold text-texto">
              Seus períodos aquisitivos
            </h2>
            {!d ? (
              <Esqueleto className="h-24 w-full" />
            ) : (
              <ul className="flex flex-col gap-3">
                {d.periodosAquisitivos.map((p) => (
                  <CartaoDoPeriodo key={p.inicio} p={p} />
                ))}
              </ul>
            )}
          </section>
          <section aria-labelledby="titulo-historico" className="flex flex-col gap-3">
            <h2 id="titulo-historico" className="text-t3 font-bold text-texto">
              Histórico
            </h2>
            <Tabela
              rotulo="Histórico"
              colunas={colunas}
              linhas={d?.historico}
              chaveDaLinha={(p) => p.id}
              carregando={consulta.isLoading}
              vazio={<EstadoVazio titulo="Você ainda não pediu férias." />}
            />
          </section>
        </div>
        <section aria-labelledby="titulo-como" className="flex flex-col gap-3 rounded-cartao bg-superficie-suave p-5">
          <h2 id="titulo-como" className="text-corpo font-bold text-texto">
            Como funciona
          </h2>
          <ul className="flex list-disc flex-col gap-2 pl-5 text-tabela text-texto">
            {comoFunciona(d?.gestorQueAnalisa ?? null).map((t) => (
              <li key={t}>{t}</li>
            ))}
          </ul>
        </section>
      </div>
      <Outlet />
    </div>
  )
}
