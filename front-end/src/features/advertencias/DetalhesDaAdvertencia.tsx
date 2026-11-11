import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import type { Esquema } from '../../shared/api/tipos'
import { formatarData, formatarDataHora } from '../../shared/formato/datas'
import { primeiroNome } from '../../shared/formato/texto'
import { AreaDeTexto } from '../../shared/ui/AreaDeTexto'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { Campo } from '../../shared/ui/Campo'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Janela } from '../../shared/ui/Janela'
import { OpcoesEmCartao } from '../../shared/ui/OpcoesEmCartao'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { CamposDaAdvertencia } from './PecasDasAdvertencias'
import { LIMITE_DA_RESPOSTA } from './rotulosDasAdvertencias'

type DetalheRh = Esquema<'AdvertenciaDetalheRh'>
type Decisao = 'MANTER' | 'ACATAR'

function ResponderContestacao({ advertencia }: { advertencia: DetalheRh }) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const responder = $api.useMutation('post', '/api/rh/advertencias/{advertenciaId}/resposta')
  const [decisao, setDecisao] = useState<Decisao | null>(null)
  const [resposta, setResposta] = useState('')
  const [erros, setErros] = useState<{ decisao?: string; resposta?: string }>({})
  const [erro, setErro] = useState<unknown>(null)
  const nome = primeiroNome(advertencia.funcionario.nome)

  async function enviar() {
    if (responder.isPending) return
    const texto = resposta.trim()
    const novos = {
      ...(decisao ? {} : { decisao: 'Escolha manter ou acatar.' }),
      ...(texto ? {} : { resposta: 'Escreva a resposta.' }),
    }
    setErros(novos)
    setErro(null)
    if (!decisao || !texto) return
    try {
      await responder.mutateAsync({
        params: { path: { advertenciaId: advertencia.id } },
        body: { decisao, resposta: texto },
      })
      notificacao.sucesso('Resposta enviada por e-mail.')
      await invalidarRota(queryClient, '/api/rh/')
    } catch (e) {
      setErro(e)
      if (e instanceof ErroDeApi && e.status === 422) await invalidarRota(queryClient, '/api/rh/')
    }
  }

  return (
    <section
      aria-label="Responder contestação"
      className="flex flex-col gap-4 border-t border-divisor pt-5"
    >
      <h3 className="text-corpo font-bold text-texto">Responder contestação</h3>
      <OpcoesEmCartao<Decisao>
        rotulo="Decisão"
        valor={decisao}
        aoMudar={setDecisao}
        opcoes={[
          {
            valor: 'MANTER',
            titulo: 'Manter a advertência',
            descricao: 'Ela volta a ficar como "Ciente".',
          },
          {
            valor: 'ACATAR',
            titulo: 'Acatar a contestação',
            descricao: 'A advertência é cancelada e não muda mais.',
          },
        ]}
      />
      {erros.decisao && <p className="text-pequeno text-erro-texto">{erros.decisao}</p>}
      <Campo
        rotulo={`Resposta para ${nome}`}
        dica={`A resposta vai por e-mail para ${nome}. Ela não fica guardada no sistema e a pessoa não a vê na tela, só a nova situação.`}
        erro={erros.resposta}
      >
        <AreaDeTexto
          rows={4}
          limite={LIMITE_DA_RESPOSTA}
          value={resposta}
          onChange={(e) => setResposta(e.target.value)}
        />
      </Campo>
      {!!erro && <AvisoDeErro erro={erro} />}
      <div>
        <Botao onClick={() => void enviar()} carregando={responder.isPending}>
          Enviar resposta
        </Botao>
      </div>
    </section>
  )
}

function textoDoDesfecho(d: DetalheRh['desfechoDaContestacao']): string | null {
  if (d === 'ACATADA') return 'Contestação acatada: advertência cancelada.'
  if (d === 'MANTIDA') return 'Contestação analisada: advertência mantida.'
  return null
}

/** Detalhe do RH (sem print): painel lateral; da 14 é rota, da aba da ficha (11) é estado. */
export function PainelDetalheDaAdvertenciaRh({
  advertenciaId,
  aoFechar,
}: {
  advertenciaId: number
  aoFechar: () => void
}) {
  const consulta = $api.useQuery('get', '/api/rh/advertencias/{advertenciaId}', {
    params: { path: { advertenciaId } },
  })
  const a = consulta.data
  const desfecho = a ? textoDoDesfecho(a.desfechoDaContestacao) : null
  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo={a ? `Advertência de ${formatarData(a.data)}` : 'Advertência'}
      posicao="direita"
      largura="media"
    >
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      ) : !a ? (
        <Esqueleto className="h-60 w-full" />
      ) : (
        <div className="flex flex-col gap-5">
          <CamposDaAdvertencia advertencia={a} />
          {a.textoContestacao && (
            <section
              aria-label="Contestação"
              className="flex flex-col gap-2 rounded-cartao bg-superficie-suave p-4"
            >
              <h3 className="text-corpo font-bold text-texto">Contestação</h3>
              <p className="text-pequeno text-texto-suave">
                {formatarDataHora(a.dataContestacaoUtc).replace(' ', ', ')}
              </p>
              <p className="whitespace-pre-line text-corpo text-texto">{a.textoContestacao}</p>
            </section>
          )}
          {desfecho && <p className="text-corpo font-semibold text-texto">{desfecho}</p>}
          {a.situacao === 'CONTESTADA' && <ResponderContestacao advertencia={a} />}
        </div>
      )}
    </Janela>
  )
}

/** Detalhe do gestor (sem print): sem o texto da contestação nem a resposta do RH. Só leitura. */
export function PainelDetalheDaAdvertenciaDaEquipe({
  advertenciaId,
  aoFechar,
}: {
  advertenciaId: number
  aoFechar: () => void
}) {
  const consulta = $api.useQuery('get', '/api/gestor/advertencias/{advertenciaId}', {
    params: { path: { advertenciaId } },
  })
  const a = consulta.data
  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo={a ? `Advertência de ${formatarData(a.data)}` : 'Advertência'}
      posicao="direita"
      largura="media"
    >
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      ) : !a ? (
        <Esqueleto className="h-60 w-full" />
      ) : (
        <CamposDaAdvertencia advertencia={a} />
      )}
    </Janela>
  )
}
