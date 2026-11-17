import { Outlet } from 'react-router'
import { $api } from '../../shared/api/api'
import { plural } from '../../shared/formato/texto'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { CartaoDeFeriasParaDecidir } from '../ferias/CartaoDeFeriasParaDecidir'
import { CartaoDeAjusteDoGestor } from './CartaoDeAjusteDoGestor'
import { FaixaHojeDaEquipe } from './FaixaHojeDaEquipe'
import { QuadroDeFeriasDaEquipe } from './QuadroDeFeriasDaEquipe'
import { TabelaDaEquipe } from './TabelaDaEquipe'

/**
 * Tela 51: o Painel da equipe do gestor (`/gestor/equipe`). Cada bloco carrega e falha sozinho: um erro
 * não derruba a tela. A ficha da pessoa (tela 52) abre por cima, como rota filha (`<Outlet />`).
 */
export function PaginaPainelDaEquipe() {
  const painel = $api.useQuery('get', '/api/gestor/painel')
  const equipe = $api.useQuery('get', '/api/gestor/equipe')
  const calendario = $api.useQuery('get', '/api/gestor/ferias/calendario')
  const p = painel.data
  const semSetor = p !== undefined && p.setor === null
  const ferias = p?.aguardandoAprovacao.ferias ?? []
  const ajustes = p?.aguardandoAprovacao.ajustesDePonto ?? []

  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo={p?.setor ? `Equipe ${p.setor.nome}` : 'Painel da equipe'}
        subtitulo={
          p?.setor
            ? `${p.totalPessoas} ${plural(p.totalPessoas, 'pessoa', 'pessoas')}. Você vê o ponto, as férias, os treinamentos e as advertências de quem está no seu setor.`
            : undefined
        }
        acoes={
          p?.avaliacao && (
            <BotaoLink to={`/eu/formularios/${p.avaliacao.formularioId}/responder`}>
              Avaliar a equipe (ciclo {p.avaliacao.ciclo})
            </BotaoLink>
          )
        }
      />
      {painel.error ? (
        <AvisoDeErro erro={painel.error} aoTentarDeNovo={() => void painel.refetch()} />
      ) : semSetor ? (
        <EstadoVazio titulo="Você ainda não é gestor de um setor." />
      ) : (
        <>
          <FaixaHojeDaEquipe equipe={equipe.data} carregando={equipe.isLoading} />
          <section aria-labelledby="titulo-aguardando" className="flex flex-col gap-3">
            <h2 id="titulo-aguardando" className="text-t3 font-bold text-texto">
              Esperando sua aprovação
            </h2>
            {!p ? (
              <Esqueleto className="h-28 w-full" />
            ) : ferias.length + ajustes.length === 0 ? (
              <p className="text-corpo text-texto-suave">Nada esperando sua aprovação.</p>
            ) : (
              <ul className="grid grid-cols-2 gap-4">
                {ferias.map((pedido) => (
                  <CartaoDeFeriasParaDecidir key={`f${pedido.id}`} pedido={pedido} quem="gestor" />
                ))}
                {ajustes.map((ajuste) => (
                  <CartaoDeAjusteDoGestor key={`a${ajuste.pedidoId}`} ajuste={ajuste} />
                ))}
              </ul>
            )}
          </section>
          <QuadroDeFeriasDaEquipe
            calendario={calendario.data}
            pessoas={(p?.pessoas ?? []).map((x) => x.funcionario)}
            carregando={calendario.isLoading || !p}
            erro={calendario.error}
            aoTentarDeNovo={() => void calendario.refetch()}
          />
          <section aria-labelledby="titulo-pessoas" className="flex flex-col gap-3">
            <h2 id="titulo-pessoas" className="text-t3 font-bold text-texto">
              Pessoas da equipe
            </h2>
            <TabelaDaEquipe
              pessoas={p?.pessoas}
              cicloDaAvaliacao={p?.avaliacao?.ciclo ?? null}
              carregando={painel.isLoading}
            />
          </section>
        </>
      )}
      <Outlet />
    </div>
  )
}
