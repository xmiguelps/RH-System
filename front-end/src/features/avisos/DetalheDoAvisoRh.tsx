import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { useLocation } from 'react-router'
import { invalidarContadoresDoMenu } from '../../app/useContadoresDoMenu'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { formatarDataDoInstante, formatarDiaMesHora } from '../../shared/formato/datas'
import { formatarNumero } from '../../shared/formato/numeros'
import { plural } from '../../shared/formato/texto'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Avatar } from '../../shared/ui/Avatar'
import { Botao } from '../../shared/ui/Botao'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { JanelaDeConfirmacao } from '../../shared/ui/JanelaDeConfirmacao'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { ConteudoDoAviso, SeloDoTipo } from './PecasDosAvisos'
import { mensagemDePublicacao } from './rotulosDosAvisos'

type Aviso = Esquema<'AvisoDetalheRh'>
type Confirmacao = 'encerrar' | 'excluir' | null

function linhaDoAutor(aviso: Aviso): string {
  const autor = aviso.autorNome ? ` por ${aviso.autorNome}` : ''
  const expira = aviso.expiraEmUtc
    ? ` Expira em ${formatarDataDoInstante(aviso.expiraEmUtc).slice(0, 5)}.`
    : ' Não expira.'
  if (aviso.situacao === 'RASCUNHO') return `Rascunho criado${autor}.`
  if (aviso.situacao === 'AGENDADO')
    return `Agendado${autor} para ${formatarDiaMesHora(aviso.publicacaoEmUtc)}.${expira}`
  if (aviso.situacao === 'EXPIRADO')
    return `Publicado${autor} em ${formatarDiaMesHora(aviso.publicacaoEmUtc)}. Expirou em ${formatarDataDoInstante(aviso.expiraEmUtc).slice(0, 5)}.`
  return `Publicado${autor} em ${formatarDiaMesHora(aviso.publicacaoEmUtc)}.${expira}`
}

/** "Quem viu": os totais e os cinco primeiros que ainda não viram (aviso já publicado). */
function QuemViu({ aviso, avisoId }: { aviso: Aviso; avisoId: number }) {
  const notificacao = useNotificacao()
  const leituras = $api.useQuery('get', '/api/rh/avisos/{avisoId}/leituras', {
    params: { path: { avisoId }, query: { situacao: 'NAO_LIDO', tamanhoPagina: 5 } },
  })
  const lembrar = $api.useMutation('post', '/api/rh/avisos/{avisoId}/lembretes')
  const dados = leituras.data
  const restantes = dados ? dados.total - dados.itens.length : 0

  async function enviarLembrete() {
    if (lembrar.isPending) return
    try {
      const resultado = await lembrar.mutateAsync({ params: { path: { avisoId } } })
      const quantos = resultado?.quantidadeDeEmails ?? 0
      notificacao.sucesso(
        quantos === 0
          ? 'Todos já viram este aviso.'
          : `Lembrete na fila para ${quantos} ${plural(quantos, 'pessoa', 'pessoas')}.`,
      )
    } catch (erro) {
      notificacao.erro(mensagemDeErro(erro))
    }
  }

  return (
    <section
      aria-labelledby="titulo-quem-viu"
      className="flex flex-col gap-4 border-t border-divisor pt-5"
    >
      <h3 id="titulo-quem-viu" className="text-corpo font-bold text-texto">
        Quem viu
      </h3>
      {leituras.error ? (
        <AvisoDeErro erro={leituras.error} aoTentarDeNovo={() => void leituras.refetch()} />
      ) : !dados ? (
        <Esqueleto className="h-24 w-full" />
      ) : (
        <>
          <dl className="grid grid-cols-2 gap-4">
            <div className="rounded-cartao bg-superficie-suave p-4">
              <dt className="text-tabela text-texto-suave">Viram</dt>
              <dd className="text-numero font-bold tabular-nums text-texto">
                {formatarNumero(dados.viram)}
              </dd>
            </div>
            <div className="rounded-cartao bg-alerta-fundo p-4">
              <dt className="text-tabela text-alerta-texto">Ainda não viram</dt>
              <dd className="text-numero font-bold tabular-nums text-alerta-texto">
                {formatarNumero(dados.aindaNaoViram)}
              </dd>
            </div>
          </dl>
          {dados.itens.length > 0 && (
            <ul aria-label="Ainda não viram" className="flex flex-col gap-3">
              {dados.itens.map((item) => (
                <li key={item.funcionario.id} className="flex items-center gap-3">
                  <Avatar nome={item.funcionario.nome} />
                  <span className="flex flex-col">
                    <span className="text-corpo text-texto">{item.funcionario.nome}</span>
                    {item.funcionario.setor && (
                      <span className="text-pequeno text-texto-suave">
                        {item.funcionario.setor}
                      </span>
                    )}
                  </span>
                </li>
              ))}
            </ul>
          )}
          {restantes > 0 && (
            <p className="text-tabela text-texto-suave">
              E mais {restantes} {plural(restantes, 'pessoa', 'pessoas')}.
            </p>
          )}
          {aviso.situacao === 'ATIVO' && (
            <Botao onClick={() => void enviarLembrete()} carregando={lembrar.isPending}>
              Lembrar quem não viu
            </Botao>
          )}
        </>
      )}
    </section>
  )
}

/** O painel de detalhe da tela 27 (obterAviso), com as ações de cada situação. */
export function DetalheDoAvisoRh({ avisoId }: { avisoId: number }) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const { search } = useLocation()
  const consulta = $api.useQuery(
    'get',
    '/api/rh/avisos/{avisoId}',
    { params: { path: { avisoId } } },
    { retry: false },
  )
  const encerrar = $api.useMutation('post', '/api/rh/avisos/{avisoId}/encerrar')
  const excluir = $api.useMutation('delete', '/api/rh/avisos/{avisoId}')
  const publicar = $api.useMutation('post', '/api/rh/avisos/{avisoId}/publicar')
  const [confirmacao, setConfirmacao] = useState<Confirmacao>(null)
  const [erroDaAcao, setErroDaAcao] = useState<unknown>(null)

  const sumiu = consulta.error instanceof ErroDeApi && consulta.error.status === 404
  const avisou = useRef<number | null>(null)
  useEffect(() => {
    if (!sumiu || avisou.current === avisoId) return
    avisou.current = avisoId
    void invalidarRota(queryClient, '/api/rh/avisos')
  }, [sumiu, avisoId, queryClient])

  async function atualizar() {
    await Promise.all([
      invalidarRota(queryClient, '/api/rh/avisos'),
      invalidarContadoresDoMenu(queryClient),
    ])
  }

  async function confirmar() {
    const qual = confirmacao
    if (!qual) return
    setErroDaAcao(null)
    try {
      if (qual === 'encerrar') {
        await encerrar.mutateAsync({ params: { path: { avisoId } } })
        notificacao.sucesso('Aviso encerrado.')
      } else {
        await excluir.mutateAsync({ params: { path: { avisoId } } })
        notificacao.sucesso('Aviso excluído.')
      }
      setConfirmacao(null)
      await atualizar()
    } catch (erro) {
      setErroDaAcao(erro)
      if (erro instanceof ErroDeApi && erro.status === 422) await atualizar()
    }
  }

  async function publicarAgora() {
    if (publicar.isPending) return
    try {
      const publicado = await publicar.mutateAsync({ params: { path: { avisoId } } })
      if (publicado) notificacao.sucesso(mensagemDePublicacao(publicado))
      await atualizar()
    } catch (erro) {
      notificacao.erro(mensagemDeErro(erro))
      if (erro instanceof ErroDeApi && erro.status === 422) await atualizar()
    }
  }

  if (sumiu) {
    return <p className="text-corpo text-texto-suave">Este aviso não existe mais.</p>
  }
  if (consulta.error) {
    return <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
  }
  const aviso = consulta.data
  if (!aviso) {
    return (
      <div role="status" aria-label="Carregando o aviso" className="flex flex-col gap-3">
        <Esqueleto className="h-6 w-40" />
        <Esqueleto className="h-48 w-full" />
      </div>
    )
  }
  const publicado = aviso.situacao === 'ATIVO' || aviso.situacao === 'EXPIRADO'
  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <SeloDoTipo tipo={aviso.tipo} />
        <div className="flex flex-wrap gap-2">
          <BotaoLink
            variante="secundario"
            tamanho="pequeno"
            to={{ pathname: `${avisoId}/editar`, search }}
          >
            Editar
          </BotaoLink>
          {aviso.situacao === 'RASCUNHO' && (
            <Botao
              tamanho="pequeno"
              onClick={() => void publicarAgora()}
              carregando={publicar.isPending}
            >
              Publicar
            </Botao>
          )}
          {aviso.situacao === 'ATIVO' && (
            <Botao
              variante="perigo"
              tamanho="pequeno"
              onClick={() => {
                setErroDaAcao(null)
                setConfirmacao('encerrar')
              }}
            >
              Encerrar agora
            </Botao>
          )}
          {!publicado && (
            <Botao
              variante="perigo"
              tamanho="pequeno"
              onClick={() => {
                setErroDaAcao(null)
                setConfirmacao('excluir')
              }}
            >
              Excluir
            </Botao>
          )}
        </div>
      </div>
      <h2 className="text-t2 font-bold text-texto">{aviso.titulo}</h2>
      <p className="text-tabela text-texto-suave">{linhaDoAutor(aviso)}</p>
      <ConteudoDoAviso tipo={aviso.tipo} conteudo={aviso.conteudo} />
      {publicado ? (
        <QuemViu aviso={aviso} avisoId={avisoId} />
      ) : (
        <p className="border-t border-divisor pt-5 text-tabela text-texto-suave">
          {aviso.situacao === 'AGENDADO'
            ? `Será publicado em ${formatarDiaMesHora(aviso.publicacaoEmUtc)}.`
            : 'Este aviso ainda não foi publicado.'}
        </p>
      )}
      <JanelaDeConfirmacao
        aberta={confirmacao === 'encerrar'}
        titulo="Encerrar este aviso agora?"
        descricao="Ele sai do ar para todos. As leituras já feitas continuam na contagem."
        rotuloDeConfirmar="Encerrar agora"
        perigo
        confirmando={encerrar.isPending}
        erro={erroDaAcao ?? undefined}
        aoCancelar={() => setConfirmacao(null)}
        aoConfirmar={confirmar}
      />
      <JanelaDeConfirmacao
        aberta={confirmacao === 'excluir'}
        titulo="Excluir este aviso?"
        descricao="Ninguém o viu ainda. Esta ação não pode ser desfeita."
        rotuloDeConfirmar="Excluir"
        perigo
        confirmando={excluir.isPending}
        erro={erroDaAcao ?? undefined}
        aoCancelar={() => setConfirmacao(null)}
        aoConfirmar={confirmar}
      />
    </div>
  )
}
