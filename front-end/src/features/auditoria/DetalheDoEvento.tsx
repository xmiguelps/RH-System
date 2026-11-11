import { useEffect, useRef } from 'react'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import { formatarDataHoraComSegundos } from '../../shared/formato/datas'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { useNotificacao } from '../../shared/ui/useNotificacao'

const NAO_REGISTRADO = 'valor não registrado'

export function DetalheDoEvento({ eventoId, aoSumir }: { eventoId: number; aoSumir?: () => void }) {
  const notificacao = useNotificacao()
  const consulta = $api.useQuery('get', '/api/rh/auditoria/{eventoId}', {
    params: { path: { eventoId } },
  })
  const avisou = useRef(false)
  const sumiu = consulta.error instanceof ErroDeApi && consulta.error.status === 404
  useEffect(() => {
    if (!sumiu || avisou.current) return
    avisou.current = true
    notificacao.erro(mensagemDeErro(consulta.error))
    aoSumir?.()
  }, [sumiu, notificacao, aoSumir, consulta.error])

  if (consulta.isLoading) return <Esqueleto className="h-24 w-full" />
  if (consulta.error) {
    return sumiu ? null : (
      <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
    )
  }
  const evento = consulta.data
  if (!evento) return null
  const origem =
    evento.origem.canal === 'SISTEMA'
      ? 'Automático'
      : `Portal no navegador${evento.origem.ip ? `, IP ${evento.origem.ip}` : ''}`

  return (
    <div className="flex flex-col gap-4 py-2">
      <dl className="grid grid-cols-3 gap-4">
        <div>
          <dt className="text-pequeno text-texto-suave">Registro afetado</dt>
          <dd>{evento.registroAfetado ?? '—'}</dd>
        </div>
        <div>
          <dt className="text-pequeno text-texto-suave">Data e hora exatas</dt>
          <dd>{formatarDataHoraComSegundos(evento.quandoUtc)}</dd>
        </div>
        <div>
          <dt className="text-pequeno text-texto-suave">Origem</dt>
          <dd>{origem}</dd>
        </div>
      </dl>
      <div>
        <p className="mb-2 text-tabela font-semibold text-texto">O que mudou</p>
        {evento.mudancas.length === 0 ? (
          <p className="text-tabela text-texto-suave">Este evento não altera dados.</p>
        ) : (
          <table aria-label="O que mudou" className="w-full text-left text-tabela">
            <thead>
              <tr className="text-texto-suave">
                <th scope="col" className="py-1 font-medium">
                  Campo
                </th>
                <th scope="col" className="py-1 font-medium">
                  Antes
                </th>
                <th scope="col" className="py-1 font-medium">
                  Depois
                </th>
              </tr>
            </thead>
            <tbody>
              {evento.mudancas.map((m) => (
                <tr key={m.campo} className="border-t border-divisor">
                  <td className="py-1">{m.campo}</td>
                  <td className="py-1 text-erro-texto">
                    {m.valorOculto ? NAO_REGISTRADO : (m.antes ?? '—')}
                  </td>
                  <td className="py-1 text-sucesso-texto">
                    {m.valorOculto ? NAO_REGISTRADO : (m.depois ?? '—')}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}
