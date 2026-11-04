import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { $api } from '../../shared/api/api'
import { formatarDataDoInstante } from '../../shared/formato/datas'
import { inteiroDaUrl } from '../../shared/lista/valoresDeLista'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Selecao } from '../../shared/ui/Selecao'
import { RespostasDadas } from './RespostasDadas'

/** Minhas respostas (sem print): o arquivo aparece só pelo nome (não há download de volta). */
export function PaginaMinhasRespostas() {
  const formularioId = inteiroDaUrl(useParams().formularioId) ?? 0
  const formulario = $api.useQuery(
    'get',
    '/api/eu/formularios/{formularioId}',
    { params: { path: { formularioId } } },
    { retry: false },
  )
  const ehAvaliacao = formulario.data?.tipo === 'AVALIACAO'
  const jaAvaliados = (formulario.data?.avaliados ?? []).filter((a) => a.respondido)
  const [escolhido, setEscolhido] = useState<number | null>(null)
  const avaliadoId = ehAvaliacao ? (escolhido ?? jaAvaliados[0]?.funcionario.id ?? null) : null
  const consulta = $api.useQuery(
    'get',
    '/api/eu/formularios/{formularioId}/minhas-respostas',
    { params: { path: { formularioId }, query: avaliadoId ? { avaliadoId } : {} } },
    { enabled: formulario.data !== undefined || formulario.error !== null, retry: false },
  )
  const r = consulta.data
  return (
    <div className="flex max-w-3xl flex-col gap-6">
      <nav aria-label="Trilha" className="text-tabela text-texto-suave">
        <Link to="/eu/formularios?aba=respondidos" className="text-primaria-media hover:underline">
          Formulários
        </Link>
        <span> / Minhas respostas</span>
      </nav>
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} />
      ) : !r ? (
        <div role="status" aria-label="Carregando as respostas">
          <Esqueleto className="h-60 w-full" />
        </div>
      ) : (
        <>
          <header className="flex flex-col gap-1">
            <h1 className="text-t1 font-bold text-texto">{r.titulo}</h1>
            <p className="text-corpo text-texto-suave">
              Respondido em {formatarDataDoInstante(r.respondidoEmUtc)}
            </p>
          </header>
          {ehAvaliacao && jaAvaliados.length > 1 && (
            <div className="w-72">
              <Selecao
                rotulo="Avaliação de"
                valor={avaliadoId ? String(avaliadoId) : null}
                opcoes={jaAvaliados.map((a) => ({
                  valor: String(a.funcionario.id),
                  rotulo: a.funcionario.nome,
                }))}
                aoMudar={(v) => v && setEscolhido(Number(v))}
              />
            </div>
          )}
          {r.avaliado && <p className="text-tabela text-texto">Avaliando: {r.avaliado.nome}</p>}
          <RespostasDadas respostas={r.respostas} />
        </>
      )}
    </div>
  )
}
