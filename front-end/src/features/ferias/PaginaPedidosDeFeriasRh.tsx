import { useSearchParams } from 'react-router'
import { $api } from '../../shared/api/api'
import { inteiroDaUrl } from '../../shared/lista/valoresDeLista'
import { FiltroDeSelecao } from '../../shared/lista/FiltroDeSelecao'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { CampoDeBusca } from '../../shared/ui/CampoDeBusca'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Paginacao } from '../../shared/ui/Paginacao'
import type { Esquema } from '../../shared/api/tipos'
import { CartaoDeFeriasParaDecidir } from './CartaoDeFeriasParaDecidir'
import { OPCOES_DE_SITUACAO } from './rotulosDasFerias'

type Situacao = Esquema<'FeriasSituacao'>

const PADRAO: Situacao = 'COM_O_RH'
const TAMANHO_DA_PAGINA = 8

/**
 * Tela 7.2: os pedidos de férias para o RH (`/rh/pedidos-de-ferias`), com o mesmo cartão do Painel da
 * equipe. `?pedido=<feriasId>` destaca um pedido (é o que o painel do RH e as pendências usam).
 */
export function PaginaPedidosDeFeriasRh() {
  const [busca, definirParametros] = useSearchParams()
  const situacao = (OPCOES_DE_SITUACAO.find((o) => o.valor === busca.get('situacao'))?.valor ??
    PADRAO) as Situacao
  const setorId = inteiroDaUrl(busca.get('setorId') ?? undefined)
  const texto = busca.get('busca') ?? ''
  const pagina = inteiroDaUrl(busca.get('pagina') ?? undefined) ?? 1
  const pedidoEmDestaque = inteiroDaUrl(busca.get('pedido') ?? undefined)

  const lista = $api.useQuery('get', '/api/rh/ferias', {
    params: {
      query: {
        situacao,
        ...(setorId ? { setorId } : {}),
        ...(texto ? { busca: texto } : {}),
        pagina,
        tamanhoPagina: TAMANHO_DA_PAGINA,
      },
    },
  })
  const setores = $api.useQuery('get', '/api/rh/setores', {
    params: { query: { pagina: 1, tamanhoPagina: 100 } },
  })
  const dados = lista.data

  function mudar(chave: string, valor: string | null, substituir = false) {
    definirParametros(
      (atual) => {
        const novo = new URLSearchParams(atual)
        if (valor) novo.set(chave, valor)
        else novo.delete(chave)
        if (chave !== 'pagina') novo.delete('pagina')
        if (chave !== 'pedido') novo.delete('pedido')
        return novo
      },
      { replace: substituir },
    )
  }

  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo="Pedidos de férias"
        subtitulo={
          situacao === PADRAO
            ? 'Os que esperam a confirmação do RH'
            : 'Pedidos de férias da empresa'
        }
      />
      <div className="grid grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1fr)] items-end gap-4">
        <CampoDeBusca
          rotulo="Buscar colaborador"
          placeholder="Buscar colaborador"
          valor={texto}
          aoMudar={(t) => mudar('busca', t.trim() || null, true)}
        />
        <FiltroDeSelecao
          rotulo="Setor"
          todos="Todos os setores"
          opcoes={(setores.data?.itens ?? []).map((s) => ({ valor: String(s.id), rotulo: s.nome }))}
          valor={setorId ? String(setorId) : undefined}
          aoMudar={(v) => mudar('setorId', v)}
          carregando={setores.isLoading}
        />
        <FiltroDeSelecao
          rotulo="Situação"
          todos="Aguardando o RH"
          opcoes={OPCOES_DE_SITUACAO.filter((o) => o.valor !== PADRAO).map((o) => ({
            valor: o.valor,
            rotulo: o.rotulo,
          }))}
          valor={situacao === PADRAO ? undefined : situacao}
          aoMudar={(v) => mudar('situacao', v)}
        />
      </div>
      {lista.error ? (
        <AvisoDeErro erro={lista.error} aoTentarDeNovo={() => void lista.refetch()} />
      ) : !dados ? (
        <Esqueleto className="h-40 w-full" />
      ) : dados.itens.length === 0 ? (
        <EstadoVazio
          titulo={
            texto || setorId
              ? 'Nenhum pedido de férias encontrado para este filtro.'
              : situacao === PADRAO
                ? 'Nenhum pedido de férias esperando o RH.'
                : 'Nenhum pedido de férias nesta situação.'
          }
        />
      ) : (
        <ul aria-label="Pedidos de férias" className="grid grid-cols-2 gap-4">
          {dados.itens.map((pedido) => (
            <CartaoDeFeriasParaDecidir
              key={pedido.id}
              pedido={pedido}
              quem="rh"
              comCargo
              destacado={pedido.id === pedidoEmDestaque}
            />
          ))}
        </ul>
      )}
      {dados && dados.total > 0 && (
        <div className="flex flex-wrap items-center justify-between gap-3">
          <Paginacao dados={dados} aoMudar={(p) => mudar('pagina', p > 1 ? String(p) : null)} />
        </div>
      )}
    </div>
  )
}
