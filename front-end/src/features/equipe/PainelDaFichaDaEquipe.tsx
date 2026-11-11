import { useEffect, useRef, useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import { inteiroDaUrl } from '../../shared/lista/valoresDeLista'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Janela } from '../../shared/ui/Janela'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { EtiquetaDaSituacao } from '../colaboradores/EtiquetaDaSituacao'
import { MolduraDaFicha } from '../colaboradores/MolduraDaFicha'
import { PainelRegistrarAdvertencia } from '../advertencias/PainelRegistrarAdvertencia'
import { ABAS_DA_FICHA_DA_EQUIPE } from './abasDaFichaDaEquipe'

export function PainelDaFichaDaEquipe() {
  const navegar = useNavigate()
  const notificacao = useNotificacao()
  const funcionarioId = inteiroDaUrl(useParams().funcionarioId)
  const [busca, definirBusca] = useSearchParams()
  const [registrando, setRegistrando] = useState(false)
  // A página da equipe é da fatia 7; até lá, fechar leva ao endereço dela (suponho).
  const fechar = () => navegar('/gestor/equipe')
  const consulta = $api.useQuery(
    'get',
    '/api/gestor/equipe/{funcionarioId}',
    { params: { path: { funcionarioId: funcionarioId ?? 0 } } },
    { enabled: funcionarioId !== undefined },
  )
  const naoEncontrado =
    funcionarioId === undefined ||
    (consulta.error instanceof ErroDeApi && consulta.error.status === 404)
  const avisou = useRef(false)
  useEffect(() => {
    if (!naoEncontrado || avisou.current) return
    avisou.current = true
    notificacao.erro(mensagemDeErro(new ErroDeApi(404, 'Não encontrado.')))
    navegar('/gestor/equipe')
  }, [naoEncontrado, notificacao, navegar])

  if (naoEncontrado) return null
  const ficha = consulta.data
  if (!ficha) {
    return (
      <Janela
        aberta
        aoFechar={fechar}
        titulo="Ficha do colaborador"
        posicao="direita"
        largura="larga"
      >
        {consulta.error ? (
          <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
        ) : (
          <div role="status" aria-label="Carregando a ficha" className="flex flex-col gap-4">
            <Esqueleto className="h-20 w-full" />
            <Esqueleto className="h-40 w-full" />
          </div>
        )}
      </Janela>
    )
  }

  const aba =
    ABAS_DA_FICHA_DA_EQUIPE.find((a) => a.valor === busca.get('aba')) ?? ABAS_DA_FICHA_DA_EQUIPE[0]!
  const linha = [ficha.cargo, ficha.setor ? `setor ${ficha.setor}` : null]
    .filter(Boolean)
    .join(', ')
  return (
    <MolduraDaFicha
      trilha={{ rotulo: ficha.setor ? `Equipe ${ficha.setor}` : 'Equipe', aoClicar: fechar }}
      nome={ficha.nome}
      linhaDeApoio={linha}
      etiqueta={
        <EtiquetaDaSituacao
          situacao={ficha.ativo ? 'ATIVO' : 'INATIVO'}
          situacaoHoje={ficha.situacaoHoje}
        />
      }
      acoes={
        ficha.ativo ? (
          <Botao variante="secundario" onClick={() => setRegistrando(true)}>
            Registrar advertência
          </Botao>
        ) : undefined
      }
      abas={ABAS_DA_FICHA_DA_EQUIPE}
      aba={aba.valor}
      aoMudarAba={(valor) =>
        definirBusca((p) => {
          const novo = new URLSearchParams(p)
          if (valor === 'dados') novo.delete('aba')
          else novo.set('aba', valor)
          return novo
        })
      }
      aoFechar={fechar}
    >
      <aba.Componente ficha={ficha} />
      {registrando && (
        <PainelRegistrarAdvertencia
          modo="gestor"
          pessoaFixa={{ id: ficha.id, nome: ficha.nome }}
          aoFechar={() => setRegistrando(false)}
        />
      )}
    </MolduraDaFicha>
  )
}
