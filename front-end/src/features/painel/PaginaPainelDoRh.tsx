import { Search } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { $api } from '../../shared/api/api'
import { dataPorExtenso, saudacao } from '../../shared/formato/datas'
import { plural, primeiroNome } from '../../shared/formato/texto'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { Entrada } from '../../shared/ui/Entrada'
import { useAuth } from '../autenticacao/useAuth'
import {
  BlocoDeAlteracoes,
  BlocoDePendencias,
  BlocoDeProximosDias,
  BlocoDeTreinamentos,
  CartaoPresencaAgora,
} from './BlocosDoPainelDoRh'

/** Tela 00: o Painel do RH (`/rh/painel`). Os blocos vêm de uma rota só (`obterPainelDoRh`) e falham juntos, cada um com o seu aviso. */
export function PaginaPainelDoRh() {
  const { usuario } = useAuth()
  const navegar = useNavigate()
  const [busca, setBusca] = useState('')
  const painel = $api.useQuery('get', '/api/rh/painel')
  const agora = new Date()
  const total = painel.data?.totalPendencias
  const estado = {
    painel: painel.data,
    carregando: painel.isLoading,
    erro: painel.error,
    aoTentarDeNovo: () => void painel.refetch(),
  }
  const frase =
    total === undefined
      ? dataPorExtenso(agora)
      : total === 0
        ? `${dataPorExtenso(agora)}. Nenhum pedido esperando revisão.`
        : `${dataPorExtenso(agora)}. Há ${total} ${plural(total, 'pedido esperando', 'pedidos esperando')} revisão.`

  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo={`${saudacao(agora)}, ${primeiroNome(usuario?.nome ?? '')}`}
        subtitulo={frase}
        acoes={
          <>
            <form
              role="search"
              onSubmit={(evento) => {
                evento.preventDefault()
                const texto = busca.trim()
                navegar(
                  texto
                    ? `/rh/colaboradores?busca=${encodeURIComponent(texto)}`
                    : '/rh/colaboradores',
                )
              }}
            >
              <Entrada
                type="search"
                icone={Search}
                aria-label="Buscar colaborador"
                placeholder="Buscar colaborador"
                value={busca}
                onChange={(e) => setBusca(e.target.value)}
              />
            </form>
            <BotaoLink to="/rh/avisos/novo" variante="secundario">
              Publicar aviso
            </BotaoLink>
            <BotaoLink to="/rh/colaboradores/novo">Adicionar colaborador</BotaoLink>
          </>
        }
      />
      <BlocoDePendencias estado={estado} />
      <div className="grid grid-cols-2 items-start gap-6">
        <div className="flex flex-col gap-6">
          <BlocoDeProximosDias estado={estado} />
          <BlocoDeTreinamentos estado={estado} />
        </div>
        <CartaoPresencaAgora estado={estado} />
      </div>
      <BlocoDeAlteracoes estado={estado} />
    </div>
  )
}
