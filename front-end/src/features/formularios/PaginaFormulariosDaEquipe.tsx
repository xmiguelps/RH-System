import { useCallback, useEffect, useState } from 'react'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { formatarDiaMes, formatarDiaMesDaData } from '../../shared/formato/datas'
import { plural } from '../../shared/formato/texto'
import { useAuth } from '../autenticacao/useAuth'
import { Aviso } from '../../shared/ui/Aviso'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Avatar } from '../../shared/ui/Avatar'
import { Botao } from '../../shared/ui/Botao'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { cn } from '../../shared/ui/estilo'
import { Progresso } from '../../shared/ui/Progresso'
import { Selo } from '../../shared/ui/Selo'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { NumerosDoPonto } from '../ponto/PecasDoPonto'
import { rotuloDoTipo } from './rotulosDosFormularios'

type ItemDaEquipe = Esquema<'FormularioEquipeItem'>
type MeuItem = Esquema<'FormularioMeuItem'>
const ATE_ENCERRAR = 'disponível ao encerrar'

/** A faixa "Sua parte": a avaliação da equipe que o próprio gestor responde. */
function SuaParte({ item }: { item: MeuItem }) {
  return (
    <Aviso
      tom="alerta"
      titulo={`Sua parte: ${item.titulo.charAt(0).toLowerCase()}${item.titulo.slice(1)}`}
      acao={
        <BotaoLink tamanho="pequeno" to={`/eu/formularios/${item.id}/responder`}>
          Continuar avaliação
        </BotaoLink>
      }
    >
      Você avaliou {item.respondidos} de {item.total} {plural(item.total, 'pessoa', 'pessoas')}.
      {item.prazo ? ` O prazo é ${formatarDiaMesDaData(item.prazo)}.` : ''}
    </Aviso>
  )
}

function CartaoDoPedido({
  item,
  selecionado,
  aoEscolher,
}: {
  item: ItemDaEquipe
  selecionado: boolean
  aoEscolher: () => void
}) {
  const anonimo = item.anonimo
  return (
    <li>
      <button
        type="button"
        aria-pressed={selecionado}
        onClick={aoEscolher}
        className={cn(
          'flex w-full flex-col gap-2 rounded-cartao border p-4 text-left focus-visible:outline-2 focus-visible:outline-primaria-media',
          selecionado ? 'border-primaria bg-superficie-suave' : 'border-borda bg-superficie',
        )}
      >
        <span className="text-pequeno text-texto-suave">
          {rotuloDoTipo(item.tipo, item.sentidoAvaliacao)}
        </span>
        <span className="font-semibold text-texto">{item.titulo}</span>
        <span className="flex justify-between text-pequeno text-texto-suave">
          <span>
            {anonimo
              ? `${item.esperados} ${plural(item.esperados, 'esperado', 'esperados')}, resultado ao encerrar`
              : `${item.respondidos ?? 0} de ${item.esperados} responderam`}
          </span>
          <span>{item.prazo ? `Até ${formatarDiaMesDaData(item.prazo)}` : 'Sem prazo'}</span>
        </span>
        {!anonimo && item.respondidos !== null && (
          <Progresso
            valor={item.respondidos}
            maximo={Math.max(item.esperados, 1)}
            rotulo={`Respostas de ${item.titulo}`}
          />
        )}
      </button>
    </li>
  )
}

function PainelQuemRespondeu({
  formularioId,
  aoSumir,
}: {
  formularioId: number
  aoSumir: (id: number, mensagem: string) => void
}) {
  const notificacao = useNotificacao()
  const consulta = $api.useQuery(
    'get',
    '/api/gestor/formularios/{formularioId}/respondentes',
    { params: { path: { formularioId } } },
    { retry: false },
  )
  const lembrar = $api.useMutation('post', '/api/gestor/formularios/{formularioId}/lembretes')
  const dados = consulta.data
  const sumiu = consulta.error instanceof ErroDeApi && consulta.error.status === 404
  const erro = consulta.error
  useEffect(() => {
    if (sumiu) aoSumir(formularioId, mensagemDeErro(erro))
  }, [sumiu, erro, aoSumir, formularioId])

  async function lembrarQuemFalta() {
    if (lembrar.isPending) return
    try {
      const r = await lembrar.mutateAsync({ params: { path: { formularioId } } })
      notificacao.sucesso(
        !r || r.anonimo || r.enviados === null
          ? 'Lembrete enviado.'
          : `Lembrete enviado a ${r.enviados} ${plural(r.enviados, 'pessoa', 'pessoas')} da sua equipe.`,
      )
    } catch (erro) {
      notificacao.erro(mensagemDeErro(erro))
    }
  }

  if (sumiu) return null
  if (consulta.error)
    return <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
  if (!dados) return <Esqueleto className="h-60 w-full" />
  const fechadoAinda = dados.anonimo && dados.respondidos === null
  return (
    <section
      aria-label="Quem respondeu"
      className="flex flex-col gap-4 rounded-cartao border border-borda bg-superficie p-5"
    >
      <div className="flex items-start justify-between gap-3">
        <div className="flex flex-col gap-1">
          <h2 className="text-t3 font-bold text-texto">{dados.titulo}</h2>
          <p className="text-tabela text-texto-suave">
            Você vê só quem já respondeu. As respostas ficam com o RH.
          </p>
        </div>
        <Botao
          variante="secundario"
          tamanho="pequeno"
          onClick={() => void lembrarQuemFalta()}
          carregando={lembrar.isPending}
        >
          Lembrar quem falta por e-mail
        </Botao>
      </div>
      <NumerosDoPonto
        rotulo="Participação da equipe"
        itens={
          fechadoAinda
            ? [
                { rotulo: 'Esperados', valor: String(dados.esperados) },
                { rotulo: 'Responderam', valor: ATE_ENCERRAR },
                { rotulo: 'Faltam', valor: ATE_ENCERRAR },
              ]
            : [
                { rotulo: 'Responderam', valor: String(dados.respondidos ?? '—') },
                {
                  rotulo: 'Faltam',
                  valor: String(dados.faltam ?? '—'),
                  tom: (dados.faltam ?? 0) > 0 ? 'alerta' : 'neutro',
                },
              ]
        }
      />
      {!dados.anonimo && (
        <ul aria-label="Pessoas da equipe" className="flex flex-col divide-y divide-divisor">
          {dados.itens.map((p) => (
            <li key={p.funcionario.id} className="flex items-center justify-between gap-3 py-3">
              <span className="flex items-center gap-3">
                <Avatar nome={p.funcionario.nome} />
                <span className="flex flex-col">
                  <span className="text-corpo text-texto">{p.funcionario.nome}</span>
                  <span className="text-pequeno text-texto-suave">
                    {[p.funcionario.cargo, p.observacao].filter(Boolean).join(', ')}
                  </span>
                </span>
              </span>
              <span className="flex items-center gap-3">
                {p.respondidoEmUtc && (
                  <span className="text-pequeno text-texto-suave">
                    em {formatarDiaMes(p.respondidoEmUtc)}
                  </span>
                )}
                {p.situacao === 'RESPONDIDO' ? (
                  <Selo tom="sucesso">Respondido</Selo>
                ) : (
                  <Selo tom="alerta">Não respondeu</Selo>
                )}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

/** Tela 57: formulários do setor (sem a avaliação do gestor, que é o avaliado). */
export function PaginaFormulariosDaEquipe() {
  const { usuario } = useAuth()
  const notificacao = useNotificacao()
  const lista = $api.useQuery('get', '/api/gestor/formularios')
  const meus = $api.useQuery('get', '/api/eu/formularios')
  const [escolhido, setEscolhido] = useState<number | null>(null)
  const [escondidos, setEscondidos] = useState<number[]>([])
  const itens = (lista.data?.itens ?? []).filter(
    (f) => f.sentidoAvaliacao !== 'EQUIPE_AVALIA_GESTOR' && !escondidos.includes(f.id),
  )
  const atual = itens.find((f) => f.id === escolhido) ?? itens[0]
  const esconder = useCallback(
    (id: number, mensagem: string) => {
      notificacao.erro(mensagem)
      setEscondidos((e) => (e.includes(id) ? e : [...e, id]))
    },
    [notificacao],
  )
  const suaParte = (meus.data?.itens ?? []).filter(
    (f) => f.tipo === 'AVALIACAO' && f.sentidoAvaliacao === 'GESTOR_AVALIA_EQUIPE',
  )
  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo="Formulários do setor"
        subtitulo={
          usuario?.setor
            ? `Veja quem do setor ${usuario.setor} já respondeu o que o RH pediu`
            : 'Veja quem da sua equipe já respondeu o que o RH pediu'
        }
      />
      {suaParte.map((f) => (
        <SuaParte key={f.id} item={f} />
      ))}
      {lista.error ? (
        <AvisoDeErro erro={lista.error} aoTentarDeNovo={() => void lista.refetch()} />
      ) : !lista.data ? (
        <div
          role="status"
          aria-label="Carregando os pedidos do RH"
          className="grid grid-cols-[1fr_2fr] gap-6"
        >
          <Esqueleto className="h-60" />
          <Esqueleto className="h-60" />
        </div>
      ) : itens.length === 0 ? (
        <EstadoVazio titulo="Nenhum pedido do RH aberto para o seu setor" />
      ) : (
        <div className="grid grid-cols-[minmax(0,1fr)_minmax(0,2fr)] items-start gap-6">
          <section aria-labelledby="titulo-pedidos" className="flex flex-col gap-3">
            <h2 id="titulo-pedidos" className="text-t3 font-bold text-texto">
              Pedidos do RH
            </h2>
            <ul className="flex flex-col gap-3">
              {itens.map((f) => (
                <CartaoDoPedido
                  key={f.id}
                  item={f}
                  selecionado={f.id === atual?.id}
                  aoEscolher={() => setEscolhido(f.id)}
                />
              ))}
            </ul>
          </section>
          {atual && (
            <PainelQuemRespondeu key={atual.id} formularioId={atual.id} aoSumir={esconder} />
          )}
        </div>
      )}
    </div>
  )
}
