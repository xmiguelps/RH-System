import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { invalidarContadoresDoMenu } from '../../app/useContadoresDoMenu'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import {
  formatarDiaMesDaData,
  formatarDiaMesHora,
  formatarMesAno,
  hojeEmSaoPaulo,
} from '../../shared/formato/datas'
import { inteiroDaUrl } from '../../shared/lista/valoresDeLista'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Avatar } from '../../shared/ui/Avatar'
import { Botao } from '../../shared/ui/Botao'
import { Campo } from '../../shared/ui/Campo'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Janela } from '../../shared/ui/Janela'
import { Selecao } from '../../shared/ui/Selecao'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { AtestadosDaEquipe, EspelhoDaPessoaDaEquipe } from './EspelhoDaEquipe'
import { CampoDaObservacao, MioloDoPedido } from './PartesDoPedido'

const VOLTAR = '/gestor/ponto-da-equipe'

/** Tela 55: o gestor aprova ou recusa (primeira etapa). 404 = pedido fora da equipe. */
export function ModalPedidoDaEquipe() {
  const navegar = useNavigate()
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const pedidoId = inteiroDaUrl(useParams().pedidoId)
  const consulta = $api.useQuery(
    'get',
    '/api/gestor/ponto/pedidos-de-ajuste/{pedidoId}',
    { params: { path: { pedidoId: pedidoId ?? 0 } } },
    { enabled: pedidoId !== undefined },
  )
  const aprovar = $api.useMutation('post', '/api/gestor/ponto/pedidos-de-ajuste/{pedidoId}/aprovar')
  const recusar = $api.useMutation('post', '/api/gestor/ponto/pedidos-de-ajuste/{pedidoId}/recusar')
  const [observacao, setObservacao] = useState('')
  const [erro, setErro] = useState<unknown>(null)
  const fechar = () => navegar(VOLTAR)
  const naoEncontrado =
    pedidoId === undefined || (consulta.error instanceof ErroDeApi && consulta.error.status === 404)
  const avisou = useRef(false)
  useEffect(() => {
    if (!naoEncontrado || avisou.current) return
    avisou.current = true
    notificacao.erro(mensagemDeErro(new ErroDeApi(404, 'Não encontrado.')))
    navegar(VOLTAR)
  }, [naoEncontrado, notificacao, navegar])

  const pedido = consulta.data
  const decidindo = aprovar.isPending || recusar.isPending

  async function decidir(qual: 'aprovar' | 'recusar') {
    if (!pedido || decidindo) return
    setErro(null)
    try {
      await (qual === 'aprovar' ? aprovar : recusar).mutateAsync({
        params: { path: { pedidoId: pedido.id } },
        body: { observacao: observacao.trim() || null },
      })
      notificacao.sucesso(
        qual === 'aprovar' ? 'Pedido aprovado. Agora o RH confirma.' : 'Pedido recusado.',
      )
      await Promise.all([
        invalidarRota(queryClient, '/api/gestor/ponto'),
        invalidarContadoresDoMenu(queryClient),
      ])
      fechar()
    } catch (e) {
      setErro(e)
      if (e instanceof ErroDeApi && e.status === 409) {
        await invalidarRota(queryClient, '/api/gestor/ponto')
      }
    }
  }

  if (naoEncontrado) return null
  return (
    <Janela
      aberta
      aoFechar={fechar}
      titulo="Pedido de ajuste de ponto"
      tamanho="media"
      rodape={
        <div className="flex w-full items-center justify-between gap-3">
          <span className="text-pequeno text-texto-suave">
            Depois de você, o RH confirma o ajuste.
          </span>
          <div className="flex gap-3">
            <Botao
              variante="perigo"
              onClick={() => void decidir('recusar')}
              disabled={!pedido?.podeDecidir || aprovar.isPending}
              carregando={recusar.isPending}
            >
              Recusar
            </Botao>
            <Botao
              onClick={() => void decidir('aprovar')}
              disabled={!pedido?.podeDecidir || recusar.isPending}
              carregando={aprovar.isPending}
            >
              Aprovar
            </Botao>
          </div>
        </div>
      }
    >
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      ) : !pedido ? (
        <Esqueleto className="h-60 w-full" />
      ) : (
        <div className="flex flex-col gap-5">
          <div className="flex items-center gap-3">
            <Avatar nome={pedido.funcionario.nome} />
            <div className="flex flex-col">
              <span className="font-bold text-texto">{pedido.funcionario.nome}</span>
              <span className="text-pequeno text-texto-suave">
                Ajuste do dia {formatarDiaMesDaData(pedido.data)}, pedido em{' '}
                {formatarDiaMesHora(pedido.criadoEmUtc)}
              </span>
            </div>
          </div>
          <MioloDoPedido pedido={pedido} />
          <CampoDaObservacao
            pedido={pedido}
            valor={observacao}
            aoMudar={setObservacao}
            desabilitado={!pedido.podeDecidir}
          />
          {!pedido.podeDecidir && pedido.motivoNaoPodeDecidir && (
            <p className="text-pequeno text-texto-suave">{pedido.motivoNaoPodeDecidir}</p>
          )}
          {!!erro && <AvisoDeErro erro={erro} />}
        </div>
      )}
    </Janela>
  )
}

function ultimosMeses(hoje: string) {
  let ano = Number(hoje.slice(0, 4))
  let mes = Number(hoje.slice(5, 7))
  const meses: { valor: string; rotulo: string }[] = []
  for (let i = 0; i < 12; i++) {
    meses.push({
      valor: `${ano}-${String(mes).padStart(2, '0')}`,
      rotulo: formatarMesAno(ano, mes),
    })
    mes -= 1
    if (mes === 0) {
      mes = 12
      ano -= 1
    }
  }
  return meses
}

/** Tela 56: o espelho de uma pessoa da equipe, num painel sobre a tela 54. */
export function PainelEspelhoDaPessoa() {
  const navegar = useNavigate()
  const funcionarioId = inteiroDaUrl(useParams().funcionarioId)
  const hoje = hojeEmSaoPaulo()
  const [periodo, setPeriodo] = useState(hoje.slice(0, 7))
  const ano = Number(periodo.slice(0, 4))
  const mes = Number(periodo.slice(5, 7))
  const consulta = $api.useQuery(
    'get',
    '/api/gestor/equipe/{funcionarioId}/ponto',
    { params: { path: { funcionarioId: funcionarioId ?? 0 }, query: { ano, mes } } },
    { enabled: funcionarioId !== undefined },
  )
  const fechar = () => navegar(VOLTAR)
  const pessoa = consulta.data?.funcionario
  const horas = consulta.data?.jornada.cargaSemanalMinutos
  return (
    <Janela
      aberta
      aoFechar={fechar}
      titulo="Espelho de ponto"
      posicao="direita"
      largura="larga"
      rodape={
        <Botao variante="secundario" onClick={fechar}>
          Fechar
        </Botao>
      }
    >
      {funcionarioId === undefined ? null : (
        <div className="flex flex-col gap-5">
          <div className="flex items-end justify-between gap-4">
            <div className="flex items-center gap-3">
              {pessoa && <Avatar nome={pessoa.nome} />}
              <div className="flex flex-col">
                <span className="font-bold text-texto">{pessoa?.nome ?? '—'}</span>
                <span className="text-pequeno text-texto-suave">
                  {[pessoa?.cargo, horas != null ? `jornada de ${horas / 60} h semanais` : null]
                    .filter(Boolean)
                    .join(', ')}
                </span>
              </div>
            </div>
            <div className="w-56">
              <Campo rotulo="Período">
                <Selecao
                  rotulo="Período"
                  opcoes={ultimosMeses(hoje)}
                  valor={periodo}
                  aoMudar={(v) => v && setPeriodo(v)}
                />
              </Campo>
            </div>
          </div>
          <EspelhoDaPessoaDaEquipe funcionarioId={funcionarioId} ano={ano} mes={mes} />
        </div>
      )}
    </Janela>
  )
}

/** Atestados da equipe inteira (sem print), em /gestor/ponto-da-equipe/atestados. */
export function PainelAtestadosDaEquipe() {
  const navegar = useNavigate()
  return (
    <Janela
      aberta
      aoFechar={() => navegar(VOLTAR)}
      titulo="Atestados da equipe"
      posicao="direita"
      largura="larga"
    >
      <AtestadosDaEquipe comPessoa />
    </Janela>
  )
}
