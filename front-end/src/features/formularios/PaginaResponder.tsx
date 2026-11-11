import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { invalidarContadoresDoMenu } from '../../app/useContadoresDoMenu'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { formatarDiaMesDaData } from '../../shared/formato/datas'
import { inteiroDaUrl } from '../../shared/lista/valoresDeLista'
import { Aviso } from '../../shared/ui/Aviso'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Markdown } from '../../shared/ui/Markdown'
import { Progresso } from '../../shared/ui/Progresso'
import { Selo } from '../../shared/ui/Selo'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { CampoDaPergunta } from './CampoDaPergunta'
import { responderFormulario } from './enviarRespostas'
import { estaRespondida, montarEnvio, type RespostaLocal } from './respostasLocais'
import { AVISO_DO_ANONIMO, rotuloDoTipo } from './rotulosDosFormularios'

type ParaResponder = Esquema<'FormularioParaResponder'>
type Pergunta = Esquema<'PerguntaFormulario'>
type Pessoa = Esquema<'FuncionarioResumo'>

/** "respostas[2].texto" volta para a pergunta da posição 2 do envio. */
function errosPorPergunta(
  erro: ErroDeApi,
  enviados: readonly { perguntaId: number }[],
): { porPergunta: Record<number, string>; resto: string[] } {
  const porPergunta: Record<number, string> = {}
  const resto: string[] = []
  for (const [chave, mensagens] of Object.entries(erro.errors)) {
    const achado = /^respostas\[(\d+)\]/i.exec(chave)
    const item = achado ? enviados[Number(achado[1])] : undefined
    if (item) porPergunta[item.perguntaId] = mensagens.join(' ')
    else resto.push(mensagens.join(' '))
  }
  return { porPergunta, resto }
}

function Perguntas({
  formulario,
  avaliado,
  aoEnviado,
}: {
  formulario: ParaResponder
  avaliado: Pessoa | null
  aoEnviado: (recibo: Esquema<'RespostaFormularioRecibo'>) => void
}) {
  const navegar = useNavigate()
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const perguntas: Pergunta[] = [...formulario.perguntas].sort((a, b) => a.ordem - b.ordem)
  const [indice, setIndice] = useState(0)
  // Só na memória da tela: nada vai para localStorage (anônimo nem qualquer outro).
  const [respostas, setRespostas] = useState<Record<number, RespostaLocal>>({})
  const [erros, setErros] = useState<Record<number, string>>({})
  const [erroGeral, setErroGeral] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const atual = perguntas[indice]
  const ultima = indice === perguntas.length - 1

  function seguir() {
    if (!atual) return
    if (atual.obrigatoria && !estaRespondida(atual, respostas[atual.id])) {
      setErros({ ...erros, [atual.id]: 'Responda esta pergunta para seguir.' })
      return
    }
    setErros({})
    if (!ultima) setIndice(indice + 1)
    else void enviar()
  }

  async function enviar() {
    if (enviando) return
    const envio = montarEnvio(perguntas, respostas)
    if (envio.respostas.length === 0) {
      setErroGeral('Responda pelo menos uma pergunta.')
      return
    }
    setEnviando(true)
    setErroGeral(null)
    try {
      const recibo = await responderFormulario(formulario.id, {
        ...envio,
        ...(avaliado ? { avaliadoId: avaliado.id } : {}),
      })
      await Promise.all([
        invalidarRota(queryClient, '/api/eu/formularios'),
        invalidarContadoresDoMenu(queryClient),
      ])
      aoEnviado(recibo)
    } catch (erro) {
      if (erro instanceof ErroDeApi && erro.status === 409) {
        notificacao.erro(mensagemDeErro(erro))
        navegar('/eu/formularios')
        return
      }
      if (erro instanceof ErroDeApi && erro.status === 400) {
        const { porPergunta, resto } = errosPorPergunta(erro, envio.respostas)
        setErros(porPergunta)
        const primeira = perguntas.findIndex((p) => porPergunta[p.id])
        if (primeira >= 0) setIndice(primeira)
        setErroGeral(resto.length ? resto.join(' ') : null)
      } else setErroGeral(mensagemDeErro(erro))
    } finally {
      setEnviando(false)
    }
  }

  if (!atual)
    return <p className="text-corpo text-texto-suave">Este formulário não tem perguntas.</p>
  return (
    <section
      aria-label="Perguntas"
      className="flex flex-col gap-5 rounded-cartao border border-borda bg-superficie p-6"
    >
      {avaliado && (
        <p className="text-tabela font-semibold text-texto">Avaliando: {avaliado.nome}</p>
      )}
      <div className="flex items-center justify-between text-pequeno text-texto-suave">
        <span>
          Pergunta {indice + 1} de {perguntas.length}
        </span>
        <span>{Math.round(((indice + 1) / perguntas.length) * 100)}%</span>
      </div>
      <Progresso valor={indice + 1} maximo={perguntas.length} rotulo="Progresso das respostas" />
      <CampoDaPergunta
        key={atual.id}
        pergunta={atual}
        valor={respostas[atual.id] ?? {}}
        aoMudar={(v) => setRespostas({ ...respostas, [atual.id]: v })}
        erro={erros[atual.id]}
      />
      {erroGeral && <Aviso tom="erro">{erroGeral}</Aviso>}
      {ultima && formulario.anonimo && <Aviso tom="info">{AVISO_DO_ANONIMO}</Aviso>}
      <div className="flex justify-between gap-3">
        <div>
          {indice > 0 && (
            <Botao variante="secundario" onClick={() => setIndice(indice - 1)} disabled={enviando}>
              Voltar
            </Botao>
          )}
        </div>
        <Botao onClick={seguir} carregando={enviando}>
          {ultima ? 'Enviar respostas' : 'Próxima'}
        </Botao>
      </div>
    </section>
  )
}

/** Responder um formulário (sem print): /eu/formularios/:formularioId/responder. */
export function PaginaResponder() {
  const formularioId = inteiroDaUrl(useParams().formularioId) ?? 0
  const consulta = $api.useQuery(
    'get',
    '/api/eu/formularios/{formularioId}',
    { params: { path: { formularioId } } },
    { retry: false },
  )
  const [avaliado, setAvaliado] = useState<Pessoa | null>(null)
  const [recibo, setRecibo] = useState<Esquema<'RespostaFormularioRecibo'> | null>(null)
  const [rodada, setRodada] = useState(0)
  const f = consulta.data

  if (consulta.error)
    return (
      <div className="flex flex-col gap-4">
        <AvisoDeErro erro={consulta.error} />
        <div>
          <BotaoLink variante="secundario" to="/eu/formularios">
            Voltar aos formulários
          </BotaoLink>
        </div>
      </div>
    )
  if (!f)
    return (
      <div role="status" aria-label="Carregando o formulário" className="flex flex-col gap-4">
        <Esqueleto className="h-24 w-full" />
        <Esqueleto className="h-60 w-full" />
      </div>
    )

  const ehAvaliacao = f.tipo === 'AVALIACAO'
  const umSo = ehAvaliacao && f.avaliados.length === 1
  const avaliadoDaVez = umSo ? (f.avaliados[0]?.funcionario ?? null) : avaliado

  return (
    <div className="flex max-w-3xl flex-col gap-6">
      <header className="flex flex-col gap-2">
        <div>
          <Selo tom="contorno">{rotuloDoTipo(f.tipo, f.sentidoAvaliacao)}</Selo>
        </div>
        <h1 className="text-t1 font-bold text-texto">{f.titulo}</h1>
        {f.descricao && <Markdown texto={f.descricao} className="text-corpo text-texto-suave" />}
        <p className="text-pequeno text-texto-suave">
          {f.minutosEstimados} min ·{' '}
          {f.prazo ? `Até ${formatarDiaMesDaData(f.prazo)}` : 'Sempre aberto'}
        </p>
      </header>
      {f.anonimo && <Aviso tom="info">{AVISO_DO_ANONIMO}</Aviso>}
      {recibo ? (
        <section
          aria-label="Respostas enviadas"
          className="flex flex-col gap-4 rounded-cartao border border-borda bg-superficie p-6"
        >
          <h2 className="text-t3 font-bold text-texto">Respostas enviadas</h2>
          {recibo.participacaoId && (
            <p className="text-corpo text-texto">Participação registrada.</p>
          )}
          <div className="flex gap-3">
            {ehAvaliacao && recibo.restantes > 0 && !umSo && (
              <Botao
                onClick={() => {
                  setRecibo(null)
                  setAvaliado(null)
                  setRodada(rodada + 1)
                  void consulta.refetch()
                }}
              >
                Avaliar a próxima pessoa
              </Botao>
            )}
            <BotaoLink variante="secundario" to="/eu/formularios">
              Voltar aos formulários
            </BotaoLink>
          </div>
        </section>
      ) : ehAvaliacao && !avaliadoDaVez ? (
        <section aria-label="Quem avaliar" className="flex flex-col gap-3">
          <h2 className="text-t3 font-bold text-texto">Quem avaliar</h2>
          <ul className="flex flex-col gap-2">
            {f.avaliados.map((a) => (
              <li
                key={a.funcionario.id}
                className="flex items-center justify-between gap-3 rounded-cartao border border-borda bg-superficie p-4"
              >
                <span className="flex flex-col">
                  <span className="text-corpo font-semibold text-texto">{a.funcionario.nome}</span>
                  {a.funcionario.cargo && (
                    <span className="text-pequeno text-texto-suave">{a.funcionario.cargo}</span>
                  )}
                </span>
                {a.respondido ? (
                  <Selo tom="sucesso">Já avaliado</Selo>
                ) : (
                  <Botao
                    tamanho="pequeno"
                    aria-label={`Avaliar ${a.funcionario.nome}`}
                    onClick={() => setAvaliado(a.funcionario)}
                  >
                    Avaliar
                  </Botao>
                )}
              </li>
            ))}
          </ul>
        </section>
      ) : (
        <>
          {umSo && avaliadoDaVez && (
            <p className="text-corpo text-texto">Você vai avaliar {avaliadoDaVez.nome}.</p>
          )}
          <Perguntas
            key={`${avaliadoDaVez?.id ?? 0}-${rodada}`}
            formulario={f}
            avaliado={avaliadoDaVez}
            aoEnviado={setRecibo}
          />
        </>
      )}
    </div>
  )
}
