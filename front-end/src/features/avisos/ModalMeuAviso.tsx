import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
import { useLocation, useNavigate, useParams } from 'react-router'
import { invalidarContadoresDoMenu } from '../../app/useContadoresDoMenu'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import { formatarDataHora } from '../../shared/formato/datas'
import { inteiroDaUrl } from '../../shared/lista/valoresDeLista'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Janela } from '../../shared/ui/Janela'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { ConteudoDoAviso, SeloDoTipo } from './PecasDosAvisos'

/** Tela 38: o aviso aberto. Abrir chama também marcarAvisoComoLido (o GET não marca). */
export function ModalMeuAviso() {
  const navegar = useNavigate()
  const { search } = useLocation()
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const avisoId = inteiroDaUrl(useParams().avisoId)
  const consulta = $api.useQuery(
    'get',
    '/api/eu/avisos/{avisoId}',
    { params: { path: { avisoId: avisoId ?? 0 } } },
    { enabled: avisoId !== undefined, retry: false },
  )
  const marcado = useRef(false)
  const aposMarcar = () => {
    marcado.current = true
    void Promise.all([
      invalidarRota(queryClient, '/api/eu/avisos'),
      invalidarContadoresDoMenu(queryClient),
    ])
  }
  const marcar = $api.useMutation('post', '/api/eu/avisos/{avisoId}/leitura', {
    onSuccess: aposMarcar,
  })
  const { mutate: marcarAgora } = marcar

  const naoDisponivel =
    avisoId === undefined || (consulta.error instanceof ErroDeApi && consulta.error.status === 404)

  const tentou = useRef(false)
  useEffect(() => {
    if (avisoId === undefined || tentou.current) return
    tentou.current = true
    marcarAgora({ params: { path: { avisoId } } })
  }, [avisoId, marcarAgora])

  const avisou = useRef(false)
  useEffect(() => {
    if (!naoDisponivel || avisou.current) return
    avisou.current = true
    notificacao.erro('Este aviso não está mais disponível.')
    void invalidarRota(queryClient, '/api/eu/avisos')
    navegar({ pathname: '/eu/avisos', search })
  }, [naoDisponivel, notificacao, navegar, search, queryClient])

  async function fechar() {
    if (!marcado.current && avisoId !== undefined && !naoDisponivel) {
      try {
        await marcar.mutateAsync({ params: { path: { avisoId } } })
      } catch (erro) {
        notificacao.erro(mensagemDeErro(erro))
      }
    }
    navegar({ pathname: '/eu/avisos', search })
  }

  if (naoDisponivel) return null
  const aviso = consulta.data
  return (
    <Janela
      aberta
      aoFechar={() => void fechar()}
      titulo={aviso?.titulo ?? 'Aviso'}
      tamanho="media"
      rodape={
        <div className="flex w-full items-center justify-between gap-3">
          <span className="text-pequeno text-texto-suave">
            Ao abrir, o aviso fica marcado como lido para o RH.
          </span>
          <Botao onClick={() => void fechar()}>Fechar</Botao>
        </div>
      }
    >
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      ) : !aviso ? (
        <div role="status" aria-label="Carregando o aviso" className="flex flex-col gap-3">
          <Esqueleto className="h-5 w-40" />
          <Esqueleto className="h-40 w-full" />
        </div>
      ) : (
        <div className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center gap-3">
            <SeloDoTipo tipo={aviso.tipo} />
            <span className="text-tabela text-texto-suave">
              {aviso.autorNome
                ? `Publicado por ${aviso.autorNome} em ${formatarDataHora(aviso.publicadoEmUtc).replace(' ', ', ')}.`
                : `Publicado em ${formatarDataHora(aviso.publicadoEmUtc).replace(' ', ', ')}.`}
            </span>
          </div>
          <ConteudoDoAviso tipo={aviso.tipo} conteudo={aviso.conteudo} />
        </div>
      )}
    </Janela>
  )
}
