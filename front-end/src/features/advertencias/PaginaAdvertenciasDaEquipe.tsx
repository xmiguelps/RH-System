import { Plus } from 'lucide-react'
import { Navigate, Outlet, useNavigate, useParams, useSearchParams } from 'react-router'
import { $api } from '../../shared/api/api'
import { formatarNumero } from '../../shared/formato/numeros'
import { usePainelSobreLista } from '../../shared/lista/usePainelSobreLista'
import { inteiroDaUrl } from '../../shared/lista/valoresDeLista'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Avatar } from '../../shared/ui/Avatar'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { cn } from '../../shared/ui/estilo'
import { Paginacao } from '../../shared/ui/Paginacao'
import { NumerosDoPonto } from '../ponto/PecasDoPonto'
import { PainelDetalheDaAdvertenciaDaEquipe } from './DetalhesDaAdvertencia'
import { TabelaDeAdvertencias } from './PecasDasAdvertencias'
import { PainelRegistrarAdvertencia } from './PainelRegistrarAdvertencia'

/** Tela 60: /gestor/advertencias/nova (o gestor escolhe a pessoa da equipe). */
export function RotaRegistrarAdvertenciaDaEquipe() {
  const { fechar } = usePainelSobreLista()
  return <PainelRegistrarAdvertencia modo="gestor" aoFechar={() => fechar()} />
}

/** O detalhe do gestor: /gestor/advertencias/:advertenciaId. */
export function RotaDetalheDaAdvertenciaDaEquipe() {
  const id = inteiroDaUrl(useParams().advertenciaId)
  const { fechar } = usePainelSobreLista()
  if (id === undefined) return <Navigate to="/gestor/advertencias" replace />
  return <PainelDetalheDaAdvertenciaDaEquipe advertenciaId={id} aoFechar={() => fechar()} />
}

function textoDaPendencia(pessoas: readonly { nome: string }[]): string | undefined {
  const [primeira, ...resto] = pessoas
  if (!primeira) return undefined
  return resto.length > 0 ? `${primeira.nome} e mais ${resto.length}` : primeira.nome
}

/** Tela 59: advertências do setor (ano atual). Os números não contam as canceladas. */
export function PaginaAdvertenciasDaEquipe() {
  const navegar = useNavigate()
  const { paraPainel } = usePainelSobreLista()
  const [parametros, definirParametros] = useSearchParams()
  const pagina = inteiroDaUrl(parametros.get('pagina') ?? undefined) ?? 1
  const equipe = $api.useQuery('get', '/api/gestor/equipe')
  const resumo = $api.useQuery('get', '/api/gestor/advertencias/resumo')
  const lista = $api.useQuery('get', '/api/gestor/advertencias', {
    params: { query: pagina > 1 ? { pagina } : {} },
  })
  const numeros = resumo.data
  const dados = lista.data
  const setor = equipe.data?.setor?.nome
  const semEquipe = numeros?.totalDaEquipe === 0

  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo="Advertências do setor"
        subtitulo={
          setor
            ? `Setor ${setor}. O RH acompanha todas as advertências registradas.`
            : 'O RH acompanha todas as advertências registradas.'
        }
        acoes={
          <BotaoLink to={paraPainel('nova')}>
            <Plus aria-hidden="true" className="h-4 w-4" />
            Registrar advertência
          </BotaoLink>
        }
      />
      {semEquipe ? (
        <EstadoVazio titulo="Você ainda não é gestor de um setor." />
      ) : (
        <>
          {resumo.error ? (
            <AvisoDeErro erro={resumo.error} aoTentarDeNovo={() => void resumo.refetch()} />
          ) : (
            <NumerosDoPonto
              rotulo="Números das advertências do setor"
              carregando={!numeros}
              itens={[
                {
                  rotulo: numeros ? `Registradas em ${numeros.ano}` : 'Registradas no ano',
                  valor: numeros ? formatarNumero(numeros.registradas) : '—',
                },
                {
                  rotulo: 'Aguardando ciência',
                  valor: numeros ? formatarNumero(numeros.aguardandoCiencia.quantidade) : '—',
                  tom: numeros && numeros.aguardandoCiencia.quantidade > 0 ? 'alerta' : 'neutro',
                  apoio: numeros ? textoDaPendencia(numeros.aguardandoCiencia.pessoas) : undefined,
                },
                {
                  rotulo: 'Pessoas com advertência',
                  valor: numeros
                    ? `${numeros.pessoasComAdvertencia} de ${numeros.totalDaEquipe}`
                    : '—',
                },
              ]}
            />
          )}
          <section aria-labelledby="titulo-registros" className="flex flex-col gap-3">
            <h2 id="titulo-registros" className="text-t3 font-bold text-texto">
              Registros
            </h2>
            <TabelaDeAdvertencias
              rotulo="Registros"
              linhas={dados?.itens}
              colaborador="so-nome"
              comAplicadaPor={false}
              aoAbrir={(a) => navegar(paraPainel(String(a.id)))}
              carregando={lista.isLoading}
              erro={lista.error ?? undefined}
              aoTentarDeNovo={() => void lista.refetch()}
              vazio={<EstadoVazio titulo="Nenhuma advertência registrada neste ano." />}
              rodape={
                dados && dados.totalPaginas > 1 ? (
                  <Paginacao
                    dados={dados}
                    aoMudar={(p) =>
                      definirParametros((atual) => {
                        const novo = new URLSearchParams(atual)
                        if (p > 1) novo.set('pagina', String(p))
                        else novo.delete('pagina')
                        return novo
                      })
                    }
                  />
                ) : undefined
              }
            />
          </section>
          <section aria-labelledby="titulo-por-pessoa" className="flex flex-col gap-3">
            <h2 id="titulo-por-pessoa" className="text-t3 font-bold text-texto">
              Por pessoa
            </h2>
            {!numeros ? (
              <Esqueleto className="h-24 w-full" />
            ) : (
              <ul aria-label="Por pessoa" className="grid grid-cols-4 gap-3">
                {numeros.porPessoa.map(({ funcionario, quantidade }) => (
                  <li
                    key={funcionario.id}
                    className="flex items-center gap-3 rounded-cartao border border-borda bg-superficie p-4"
                  >
                    <Avatar nome={funcionario.nome} />
                    <span className="flex min-w-0 flex-1 flex-col">
                      <span className="truncate text-corpo text-texto">{funcionario.nome}</span>
                    </span>
                    <span
                      className={cn(
                        'text-t3 font-bold tabular-nums',
                        quantidade > 0 ? 'text-alerta-texto' : 'text-texto-suave',
                      )}
                    >
                      {quantidade}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      )}
      <Outlet />
    </div>
  )
}
