import { ChevronRight } from 'lucide-react'
import { Link, Outlet, useLocation, useSearchParams } from 'react-router'
import { $api } from '../../shared/api/api'
import type { Esquema } from '../../shared/api/tipos'
import { formatarDiaMes } from '../../shared/formato/datas'
import { Abas } from '../../shared/ui/Abas'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { CampoDeBusca } from '../../shared/ui/CampoDeBusca'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { cn } from '../../shared/ui/estilo'
import { SeloDoTipo } from './PecasDosAvisos'

type Aba = 'todos' | 'nao-lidos'
type AvisoEu = Esquema<'AvisoItemEu'>

function CartaoDoMeuAviso({ aviso }: { aviso: AvisoEu }) {
  const { search } = useLocation()
  return (
    <li>
      <Link
        to={{ pathname: String(aviso.id), search }}
        aria-label={`Abrir o aviso ${aviso.titulo}`}
        className={cn(
          'flex items-center gap-4 rounded-cartao border border-borda p-5 hover:border-primaria-media focus-visible:outline-2 focus-visible:outline-primaria-media',
          aviso.lido ? 'bg-superficie' : 'bg-[#fffaf0]',
        )}
      >
        <span
          aria-hidden="true"
          className={cn('h-2.5 w-2.5 shrink-0 rounded-full', !aviso.lido && 'bg-alerta-ponto')}
        />
        <span className="flex min-w-0 flex-1 flex-col gap-1">
          <span className="flex flex-wrap items-center gap-2">
            <span className="font-bold text-texto">{aviso.titulo}</span>
            <SeloDoTipo tipo={aviso.tipo} />
            {!aviso.lido && (
              <span className="text-pequeno font-semibold text-alerta-texto">Não lido</span>
            )}
          </span>
          <span className="truncate text-tabela text-texto-suave">{aviso.previa}</span>
        </span>
        <span className="shrink-0 text-tabela text-texto-suave tabular-nums">
          {formatarDiaMes(aviso.publicadoEmUtc)}
        </span>
        <ChevronRight aria-hidden="true" className="h-5 w-5 shrink-0 text-texto-suave" />
      </Link>
    </li>
  )
}

function textoDoVazio(aba: Aba, busca: string): string {
  if (busca) return 'Nenhum aviso encontrado para esta busca.'
  return aba === 'nao-lidos' ? 'Você leu todos os avisos.' : 'Nenhum aviso por enquanto.'
}

/** Telas 37 e 50 (a mesma tela para os três perfis; só o menu muda). O aviso aberto (38) é a rota filha. */
export function PaginaMeusAvisos() {
  const [parametros, definirParametros] = useSearchParams()
  const aba: Aba = parametros.get('aba') === 'nao-lidos' ? 'nao-lidos' : 'todos'
  const busca = parametros.get('busca') ?? ''
  const consulta = $api.useInfiniteQuery(
    'get',
    '/api/eu/avisos',
    {
      params: {
        query: {
          ...(aba === 'nao-lidos' ? { apenasNaoLidos: true } : {}),
          ...(busca ? { busca } : {}),
        },
      },
    },
    {
      pageParamName: 'pagina',
      initialPageParam: 1,
      getNextPageParam: (ultima) =>
        ultima.pagina < ultima.totalPaginas ? ultima.pagina + 1 : undefined,
    },
  )
  const paginas = consulta.data?.pages
  const contagens = paginas?.[0]?.contagens
  const avisos = paginas?.flatMap((p) => p.itens)

  function mudar(chave: 'aba' | 'busca', valor: string | null) {
    definirParametros(
      (atual) => {
        const novo = new URLSearchParams(atual)
        if (valor) novo.set(chave, valor)
        else novo.delete(chave)
        return novo
      },
      { replace: chave === 'busca' },
    )
  }

  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina titulo="Avisos" subtitulo="Comunicados do RH para você" />
      <div className="flex flex-wrap items-center justify-between gap-4">
        <Abas<Aba>
          rotulo="Avisos"
          itens={[
            { valor: 'nao-lidos', rotulo: 'Não lidos', contagem: contagens?.naoLidos },
            { valor: 'todos', rotulo: 'Todos', contagem: contagens?.total },
          ]}
          valor={aba}
          aoMudar={(v) => mudar('aba', v === 'todos' ? null : v)}
        />
        <div className="w-80">
          <CampoDeBusca
            rotulo="Buscar aviso"
            placeholder="Buscar aviso"
            valor={busca}
            aoMudar={(texto) => mudar('busca', texto.trim() || null)}
          />
        </div>
      </div>
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      ) : !avisos ? (
        <div role="status" aria-label="Carregando os avisos" className="flex flex-col gap-3">
          <Esqueleto className="h-20 w-full" />
          <Esqueleto className="h-20 w-full" />
          <Esqueleto className="h-20 w-full" />
        </div>
      ) : avisos.length === 0 ? (
        <EstadoVazio titulo={textoDoVazio(aba, busca)} />
      ) : (
        <>
          <ul aria-label="Lista de avisos" className="flex flex-col gap-3">
            {avisos.map((aviso) => (
              <CartaoDoMeuAviso key={aviso.id} aviso={aviso} />
            ))}
          </ul>
          {consulta.hasNextPage && (
            <div className="flex justify-center">
              <Botao
                variante="secundario"
                carregando={consulta.isFetchingNextPage}
                onClick={() => void consulta.fetchNextPage()}
              >
                Carregar mais
              </Botao>
            </div>
          )}
        </>
      )}
      <Outlet />
    </div>
  )
}
