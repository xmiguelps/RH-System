import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { $api, clienteHttp } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { baixarExportacao } from '../../shared/arquivos/baixar'
import { plural } from '../../shared/formato/texto'
import { inteiroDaUrl } from '../../shared/lista/valoresDeLista'
import { Aviso } from '../../shared/ui/Aviso'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Progresso } from '../../shared/ui/Progresso'
import { Selo } from '../../shared/ui/Selo'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { rotuloDoTipo, SITUACAO_DO_FORMULARIO } from './rotulosDosFormularios'

type Resultado = Esquema<'FormularioResultado'>
type PerguntaDoResultado = Esquema<'FormularioResultadoPergunta'>

const umaCasa = (n: number) =>
  n.toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 })

function mensagemDoResultado(erro: unknown): string {
  if (erro instanceof ErroDeApi && erro.codigo === 'RESULTADO_ANONIMO_INDISPONIVEL')
    return 'O resultado de um formulário anônimo só aparece depois que ele é encerrado.'
  if (erro instanceof ErroDeApi && erro.codigo === 'RESULTADO_SEM_RESPOSTAS_SUFICIENTES')
    return (
      erro.detail ??
      'Esta pessoa recebeu menos de 3 avaliações, então o resultado dela não aparece.'
    )
  return mensagemDeErro(erro)
}

function SecaoDaPergunta({ p }: { p: PerguntaDoResultado }) {
  return (
    <section
      aria-label={`Pergunta ${p.ordem}`}
      className="flex flex-col gap-3 rounded-cartao border border-borda bg-superficie p-5"
    >
      <h2 className="text-corpo font-bold text-texto">
        {p.ordem}. {p.texto}
      </h2>
      <p className="text-pequeno text-texto-suave">
        {p.totalRespostas} {plural(p.totalRespostas, 'resposta', 'respostas')}
      </p>
      {p.tipo === 'MULTIPLA_ESCOLHA' && (
        <ul className="flex flex-col gap-2">
          {p.alternativas.map((a) => (
            <li key={a.alternativaId} className="flex flex-col gap-1">
              <span className="flex justify-between text-tabela text-texto">
                <span>{a.texto}</span>
                <span className="tabular-nums">
                  {a.total} ({umaCasa(a.percentual)}%)
                </span>
              </span>
              <Progresso valor={a.percentual} rotulo={a.texto} />
            </li>
          ))}
        </ul>
      )}
      {p.tipo === 'ESCALA' && p.escala && (
        <div className="flex flex-col gap-2">
          <p className="text-corpo text-texto">Média {umaCasa(p.escala.media)}</p>
          <dl className="grid grid-cols-5 gap-2">
            {[1, 2, 3, 4, 5].map((valor) => (
              <div
                key={valor}
                className="flex flex-col items-center rounded-campo bg-superficie-suave p-2"
              >
                <dt className="text-pequeno text-texto-suave">{valor}</dt>
                <dd className="text-corpo font-semibold tabular-nums text-texto">
                  {p.escala?.distribuicao.find((d) => d.valor === valor)?.total ?? 0}
                </dd>
              </div>
            ))}
          </dl>
        </div>
      )}
      {(p.tipo === 'RESPOSTA_CURTA' || p.tipo === 'RESPOSTA_LONGA') && (
        // Na ordem recebida (alfabética no anônimo): sem numerar e sem reordenar.
        <ul className="flex list-none flex-col gap-2">
          {p.textos.map((texto, i) => (
            <li
              key={i}
              className="whitespace-pre-line rounded-campo bg-superficie-suave p-3 text-tabela text-texto"
            >
              {texto}
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function CorpoDoResultado({ resultado }: { resultado: Resultado }) {
  const ocultas = resultado.perguntasOcultas
  const perguntas = [...resultado.perguntas].sort((a, b) => a.ordem - b.ordem)
  return (
    <div className="flex flex-col gap-4">
      <p className="text-corpo text-texto">
        {resultado.totalRespostas} {plural(resultado.totalRespostas, 'resposta', 'respostas')}
      </p>
      {!resultado.anonimo && resultado.situacao === 'PUBLICADO' && (
        <Aviso tom="info">Resultado parcial: o formulário ainda está aberto.</Aviso>
      )}
      {perguntas.map((p) => (
        <SecaoDaPergunta key={p.perguntaId} p={p} />
      ))}
      {ocultas > 0 && (
        <p className="text-tabela text-texto-suave">
          {ocultas === 1
            ? `1 pergunta não aparece porque teve menos de ${resultado.minimoPorPergunta ?? 3} respostas.`
            : `${ocultas} perguntas não aparecem porque tiveram menos de ${resultado.minimoPorPergunta ?? 3} respostas.`}
        </p>
      )}
    </div>
  )
}

/** O resultado de um formulário (sem print): /rh/formularios/:formularioId/resultado. */
export function PaginaResultado() {
  const notificacao = useNotificacao()
  const formularioId = inteiroDaUrl(useParams().formularioId) ?? 0
  const [avaliadoId, setAvaliadoId] = useState<number | null>(null)
  const [baixando, setBaixando] = useState(false)
  const formulario = $api.useQuery('get', '/api/rh/formularios/{formularioId}', {
    params: { path: { formularioId } },
  })
  const ehAvaliacao = formulario.data?.tipo === 'AVALIACAO'
  const avaliados = $api.useQuery(
    'get',
    '/api/rh/formularios/{formularioId}/avaliados',
    { params: { path: { formularioId } } },
    { enabled: ehAvaliacao, retry: false },
  )
  const precisaDeAvaliado = ehAvaliacao && avaliadoId === null
  const resultado = $api.useQuery(
    'get',
    '/api/rh/formularios/{formularioId}/resultado',
    { params: { path: { formularioId }, query: avaliadoId ? { avaliadoId } : {} } },
    { enabled: formulario.data !== undefined && !precisaDeAvaliado, retry: false },
  )
  const f = formulario.data

  async function baixarRespostas() {
    if (baixando) return
    setBaixando(true)
    try {
      await baixarExportacao(
        clienteHttp.GET('/api/rh/formularios/{formularioId}/exportacao', {
          params: { path: { formularioId } },
          parseAs: 'blob',
        }),
        `respostas-formulario-${formularioId}.csv`,
      )
    } catch (erro) {
      notificacao.erro(mensagemDoResultado(erro))
    } finally {
      setBaixando(false)
    }
  }

  if (formulario.error) return <AvisoDeErro erro={formulario.error} />
  if (!f) return <Esqueleto className="h-60 w-full" />
  const situacao = SITUACAO_DO_FORMULARIO[f.situacao]
  const lista = avaliados.data
  return (
    <div className="flex flex-col gap-6">
      <nav aria-label="Trilha" className="text-tabela text-texto-suave">
        <Link to="/rh/formularios" className="text-primaria-media hover:underline">
          Formulários
        </Link>
        <span> / Resultado</span>
      </nav>
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex flex-col gap-2">
          <h1 className="text-t1 font-bold text-texto">{f.titulo}</h1>
          <div className="flex gap-2">
            <Selo tom="contorno">{rotuloDoTipo(f.tipo, f.sentidoAvaliacao)}</Selo>
            <Selo tom={situacao.tom}>{situacao.texto}</Selo>
          </div>
        </div>
        <Botao variante="secundario" onClick={() => void baixarRespostas()} carregando={baixando}>
          Baixar respostas
        </Botao>
      </header>
      {ehAvaliacao && (
        <section aria-label="Quem foi avaliado" className="flex flex-col gap-2">
          <h2 className="text-corpo font-bold text-texto">Quem foi avaliado</h2>
          {avaliados.error ? (
            <Aviso tom="info">{mensagemDoResultado(avaliados.error)}</Aviso>
          ) : !lista ? (
            <Esqueleto className="h-20 w-full" />
          ) : (
            <ul className="flex flex-wrap gap-2">
              {lista.itens.map((a) => (
                <li key={a.funcionario.id}>
                  <button
                    type="button"
                    disabled={!a.resultadoLiberado}
                    aria-pressed={avaliadoId === a.funcionario.id}
                    onClick={() => setAvaliadoId(a.funcionario.id)}
                    className="flex flex-col items-start rounded-cartao border border-borda bg-superficie px-4 py-2 text-left disabled:cursor-not-allowed disabled:opacity-60 aria-pressed:border-primaria"
                  >
                    <span className="text-corpo font-semibold text-texto">
                      {a.funcionario.nome}
                    </span>
                    <span className="text-pequeno text-texto-suave">
                      {[a.funcionario.cargo, a.funcionario.setor].filter(Boolean).join(', ')}
                    </span>
                    <span className="text-pequeno text-texto-suave">
                      {a.totalRespostas === null
                        ? `menos de ${lista.minimoPorAvaliado ?? 3} respostas`
                        : `${a.totalRespostas} de ${a.esperadas} avaliações`}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
      {precisaDeAvaliado ? (
        <p className="text-corpo text-texto-suave">
          Escolha quem foi avaliado para ver o resultado.
        </p>
      ) : resultado.error ? (
        <Aviso tom="info">{mensagemDoResultado(resultado.error)}</Aviso>
      ) : !resultado.data ? (
        <div role="status" aria-label="Carregando o resultado">
          <Esqueleto className="h-60 w-full" />
        </div>
      ) : (
        <CorpoDoResultado resultado={resultado.data} />
      )}
    </div>
  )
}
