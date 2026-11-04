import { useQueryClient } from '@tanstack/react-query'
import { CircleCheck, ExternalLink } from 'lucide-react'
import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { $api, clienteHttp } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { useBaixarPorLink } from '../../shared/arquivos/useBaixarPorLink'
import { inteiroDaUrl } from '../../shared/lista/valoresDeLista'
import { Aviso } from '../../shared/ui/Aviso'
import { Botao } from '../../shared/ui/Botao'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { cn } from '../../shared/ui/estilo'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Markdown } from '../../shared/ui/Markdown'
import { Progresso } from '../../shared/ui/Progresso'
import { Selo } from '../../shared/ui/Selo'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import {
  diaDoInstante,
  formatarDuracao,
  ROTULO_DO_MODULO,
  seloDoTreinamento,
} from './rotulosDosTreinamentos'

type Detalhe = Esquema<'TreinamentoMeuDetalhe'>

function mensagemDoTreinamento(erro: unknown): string {
  if (erro instanceof ErroDeApi && erro.status === 404) return 'Treinamento não encontrado.'
  if (erro instanceof ErroDeApi && erro.codigo === 'TREINAMENTO_ENCERRADO')
    return 'O RH encerrou este treinamento. Fale com o RH se precisar dele.'
  if (erro instanceof ErroDeApi && erro.codigo === 'INSCRICAO_PENDENTE')
    return 'Inscreva-se no treinamento antes de começar.'
  return mensagemDeErro(erro)
}

function ConteudoDoModulo({
  treinamentoId,
  moduloId,
  aoConcluir,
  concluindo,
}: {
  treinamentoId: number
  moduloId: number
  aoConcluir: () => void
  concluindo: boolean
}) {
  const { baixar } = useBaixarPorLink()
  const consulta = $api.useQuery(
    'get',
    '/api/eu/treinamentos/{treinamentoId}/modulos/{moduloId}',
    { params: { path: { treinamentoId, moduloId } } },
    { retry: false },
  )
  const m = consulta.data
  if (consulta.error) return <Aviso tom="erro">{mensagemDoTreinamento(consulta.error)}</Aviso>
  if (!m) return <Esqueleto className="h-40 w-full" />
  return (
    <section
      aria-label={`Módulo ${m.ordem}: ${m.titulo}`}
      className="flex flex-col gap-4 rounded-cartao border border-borda bg-superficie p-6"
    >
      <div className="flex flex-col gap-1">
        <span className="text-pequeno text-texto-suave">
          {ROTULO_DO_MODULO[m.tipo]}
          {m.duracaoMinutos ? `, ${formatarDuracao(m.duracaoMinutos)}` : ''}
        </span>
        <h2 className="text-t3 font-bold text-texto">{m.titulo}</h2>
      </div>
      {m.tipo === 'TEXTO' && m.conteudoTexto && <Markdown texto={m.conteudoTexto} />}
      {(m.tipo === 'VIDEO' || m.tipo === 'LINK') && m.urlExterna && (
        <div className="flex flex-col gap-2">
          {/* Nunca iframe nem video com endereço de fora: a política do site (default-src 'self') impede. */}
          <a
            href={m.urlExterna}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex w-fit items-center gap-2 rounded-botao bg-primaria px-4 py-2 font-semibold text-pagina"
          >
            {m.tipo === 'VIDEO' ? 'Abrir vídeo' : 'Abrir link'}
            <ExternalLink aria-hidden="true" className="h-4 w-4" />
          </a>
          <p className="text-pequeno text-texto-suave">
            {m.tipo === 'VIDEO' ? 'O vídeo abre em outra aba.' : 'O link abre em outra aba.'}
          </p>
        </div>
      )}
      {m.tipo === 'PDF' && m.temArquivo && (
        <div>
          <Botao
            variante="secundario"
            onClick={() =>
              void baixar(() =>
                clienteHttp.GET(
                  '/api/eu/treinamentos/{treinamentoId}/modulos/{moduloId}/download',
                  {
                    params: { path: { treinamentoId, moduloId } },
                  },
                ),
              )
            }
          >
            Abrir PDF
          </Botao>
        </div>
      )}
      <div className="border-t border-divisor pt-4">
        {m.concluido ? (
          <p className="flex items-center gap-2 text-tabela text-primaria">
            <CircleCheck aria-hidden="true" className="h-4 w-4" />
            Módulo concluído em {diaDoInstante(m.concluidoEmUtc)}
          </p>
        ) : (
          <Botao onClick={aoConcluir} carregando={concluindo}>
            Concluir módulo
          </Botao>
        )}
      </div>
    </section>
  )
}

/** Fazer o treinamento (sem print): /eu/treinamentos/:treinamentoId. Módulos em qualquer ordem. */
export function PaginaFazerTreinamento() {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const { baixar } = useBaixarPorLink()
  const treinamentoId = inteiroDaUrl(useParams().treinamentoId) ?? 0
  const consulta = $api.useQuery(
    'get',
    '/api/eu/treinamentos/{treinamentoId}',
    { params: { path: { treinamentoId } } },
    { retry: false },
  )
  const concluir = $api.useMutation(
    'post',
    '/api/eu/treinamentos/{treinamentoId}/modulos/{moduloId}/concluir',
  )
  const inscrever = $api.useMutation('post', '/api/eu/treinamentos/{treinamentoId}/inscricao')
  const [devolvido, setDevolvido] = useState<Detalhe | null>(null)
  const [escolhido, setEscolhido] = useState<number | null>(null)
  const [concluiu, setConcluiu] = useState(false)
  const [erro, setErro] = useState<unknown>(null)
  const t = devolvido ?? consulta.data

  if (consulta.error)
    return (
      <div className="flex flex-col gap-4">
        <Aviso tom="erro">{mensagemDoTreinamento(consulta.error)}</Aviso>
        <div>
          <BotaoLink variante="secundario" to="/eu/treinamentos">
            Voltar aos treinamentos
          </BotaoLink>
        </div>
      </div>
    )
  if (!t)
    return (
      <div role="status" aria-label="Carregando o treinamento" className="flex flex-col gap-4">
        <Esqueleto className="h-24 w-full" />
        <Esqueleto className="h-60 w-full" />
      </div>
    )

  const modulos = [...t.modulos].sort((a, b) => a.ordem - b.ordem)
  const atual = escolhido ?? modulos.find((m) => !m.concluido)?.id ?? modulos[0]?.id ?? null
  const params = { params: { path: { treinamentoId } } }

  async function concluirModulo(moduloId: number) {
    if (concluir.isPending) return
    setErro(null)
    try {
      const r = await concluir.mutateAsync({ params: { path: { treinamentoId, moduloId } } })
      if (r) {
        setDevolvido(r.treinamento)
        if (r.treinamentoConcluido) setConcluiu(true)
      }
      await invalidarRota(queryClient, '/api/eu/treinamentos')
    } catch (e) {
      setErro(e)
    }
  }

  async function inscreverMe() {
    try {
      await inscrever.mutateAsync(params)
      notificacao.sucesso('Inscrição feita.')
      setErro(null)
      setDevolvido(null)
      await invalidarRota(queryClient, '/api/eu/treinamentos')
    } catch (e) {
      setErro(e)
    }
  }

  const pendenteDeInscricao =
    t.situacao === 'DISPONIVEL' ||
    (erro instanceof ErroDeApi && erro.codigo === 'INSCRICAO_PENDENTE')
  const duracao = formatarDuracao(t.duracaoTotalMinutos)
  return (
    <div className="flex flex-col gap-6">
      <nav aria-label="Trilha" className="text-tabela text-texto-suave">
        <Link to="/eu/treinamentos" className="text-primaria-media hover:underline">
          Meus treinamentos
        </Link>
        <span> / {t.titulo}</span>
      </nav>
      <header className="flex flex-col gap-2">
        <div>
          <Selo tom={t.obrigatorio ? 'alerta' : 'contorno'}>{seloDoTreinamento(t)}</Selo>
        </div>
        <h1 className="text-t1 font-bold text-texto">{t.titulo}</h1>
        {t.descricao && <Markdown texto={t.descricao} className="text-corpo text-texto-suave" />}
        <p className="text-pequeno text-texto-suave">
          {t.modulosConcluidos} de {t.totalModulos} módulos concluídos
          {duracao ? ` · ${duracao}` : ''}
        </p>
        <Progresso valor={t.progressoPercentual} rotulo="Andamento do treinamento" />
        {t.encerrado && t.situacao !== 'CONCLUIDO' && (
          <Aviso tom="alerta">Encerrado pelo RH; você ainda pode terminar</Aviso>
        )}
      </header>
      {concluiu && (
        <section
          aria-label="Você concluiu"
          className="flex flex-col gap-3 rounded-cartao bg-sucesso-fundo p-6"
        >
          <h2 className="text-t3 font-bold text-sucesso-texto">Você concluiu {t.titulo}!</h2>
          {t.certificado && (
            <>
              <p className="text-corpo text-texto">Número do certificado: {t.certificado.numero}</p>
              <p className="text-tabela text-texto-suave">
                O certificado fica guardado em Meus treinamentos.
              </p>
              <div>
                <Botao
                  onClick={() =>
                    void baixar(() =>
                      clienteHttp.GET(
                        '/api/eu/treinamentos/{treinamentoId}/certificado/download',
                        params,
                      ),
                    )
                  }
                >
                  Ver certificado
                </Botao>
              </div>
            </>
          )}
        </section>
      )}
      {!!erro && !pendenteDeInscricao && <Aviso tom="erro">{mensagemDoTreinamento(erro)}</Aviso>}
      {pendenteDeInscricao ? (
        <div className="flex flex-col gap-3">
          <p className="text-corpo text-texto">Inscreva-se no treinamento antes de começar.</p>
          <div>
            <Botao onClick={() => void inscreverMe()} carregando={inscrever.isPending}>
              Quero me inscrever
            </Botao>
          </div>
        </div>
      ) : (
        <div className="grid grid-cols-[minmax(0,1fr)_minmax(0,2fr)] items-start gap-6">
          <ol aria-label="Módulos" className="flex flex-col gap-2">
            {modulos.map((m) => (
              <li key={m.id}>
                <button
                  type="button"
                  aria-current={m.id === atual ? 'step' : undefined}
                  onClick={() => setEscolhido(m.id)}
                  className={cn(
                    'flex w-full flex-col gap-1 rounded-cartao border p-3 text-left',
                    m.id === atual
                      ? 'border-primaria bg-superficie-suave'
                      : 'border-borda bg-superficie',
                  )}
                >
                  <span className="flex items-center justify-between gap-2">
                    <span className="font-semibold text-texto">
                      {m.ordem}. {m.titulo}
                    </span>
                    {m.concluido && (
                      <CircleCheck aria-label="Concluído" className="h-4 w-4 text-primaria" />
                    )}
                  </span>
                  <span className="text-pequeno text-texto-suave">
                    {ROTULO_DO_MODULO[m.tipo]}
                    {m.duracaoMinutos ? `, ${formatarDuracao(m.duracaoMinutos)}` : ''}
                    {m.concluido
                      ? `, concluído em ${diaDoInstante(m.concluidoEmUtc).slice(0, 5)}`
                      : ''}
                  </span>
                </button>
              </li>
            ))}
          </ol>
          {atual !== null && (
            <ConteudoDoModulo
              key={`${atual}-${t.modulosConcluidos}`}
              treinamentoId={treinamentoId}
              moduloId={atual}
              concluindo={concluir.isPending}
              aoConcluir={() => void concluirModulo(atual)}
            />
          )}
        </div>
      )}
    </div>
  )
}
