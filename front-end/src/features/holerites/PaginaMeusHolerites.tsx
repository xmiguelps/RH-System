import { useQueryClient } from '@tanstack/react-query'
import { useSearchParams } from 'react-router'
import { $api, clienteHttp } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { abrirLinkTemporario } from '../../shared/arquivos/baixar'
import { formatarDataDoInstante, hojeEmSaoPaulo } from '../../shared/formato/datas'
import { plural } from '../../shared/formato/texto'
import { valorDeEnum } from '../../shared/lista/valoresDeLista'
import { Abas } from '../../shared/ui/Abas'
import { Botao } from '../../shared/ui/Botao'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Paginacao } from '../../shared/ui/Paginacao'
import { Selecao } from '../../shared/ui/Selecao'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { CartaoProximoPagamento } from './CartaoProximoPagamento'
import { referenciaDoHolerite, ROTULO_DO_TIPO, TIPOS_DO_HOLERITE } from './rotulosDosHolerites'

type Tipo = Esquema<'HoleriteTipo'>
type Holerite = Esquema<'HoleriteItemEu'>
type Aba = Tipo | 'TODOS'

/** Telas 36 e 49 (a mesma para os três perfis): só os próprios holerites, publicados. */
export function PaginaMeusHolerites() {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const [parametros, definirParametros] = useSearchParams()
  const tipo = valorDeEnum(parametros.get('tipo') ?? undefined, TIPOS_DO_HOLERITE)
  const anoDaUrl = Number(parametros.get('ano'))
  const ano = Number.isInteger(anoDaUrl) && anoDaUrl >= 2000 && anoDaUrl <= 2100 ? anoDaUrl : null
  const pagina = Number(parametros.get('pagina') ?? '1') || 1
  const lista = $api.useQuery('get', '/api/eu/holerites', {
    params: {
      query: {
        ...(ano ? { ano } : {}),
        ...(tipo ? { tipo } : {}),
        ...(pagina > 1 ? { pagina } : {}),
      },
    },
  })
  const proximo = $api.useQuery('get', '/api/eu/pagamento/proximo')
  const dados = lista.data
  const anoMostrado = dados?.ano ?? ano ?? Number(hojeEmSaoPaulo().slice(0, 4))

  function mudar(valores: Record<string, string | null>) {
    definirParametros((atual) => {
      const novo = new URLSearchParams(atual)
      for (const [chave, valor] of Object.entries(valores)) {
        if (valor) novo.set(chave, valor)
        else novo.delete(chave)
      }
      return novo
    })
  }

  async function baixar(holerite: Holerite) {
    try {
      const { data } = await clienteHttp.GET('/api/eu/holerites/{holeriteId}/download', {
        params: { path: { holeriteId: holerite.id } },
      })
      if (data) abrirLinkTemporario(data)
    } catch (erro) {
      if (erro instanceof ErroDeApi && erro.status === 404) {
        notificacao.erro('Este holerite não está mais disponível.')
        await invalidarRota(queryClient, '/api/eu/holerites')
      } else {
        notificacao.erro(mensagemDeErro(erro))
      }
    }
  }

  const colunas: readonly ColunaDaTabela<Holerite>[] = [
    { chave: 'referencia', titulo: 'Referência', celula: (h) => referenciaDoHolerite(h) },
    { chave: 'tipo', titulo: 'Tipo', celula: (h) => ROTULO_DO_TIPO[h.tipo] },
    {
      chave: 'publicado',
      titulo: 'Publicado em',
      celula: (h) => formatarDataDoInstante(h.publicadoEmUtc),
    },
    {
      chave: 'acoes',
      titulo: 'Ações',
      alinhamento: 'direita',
      celula: (h) => (
        <span className="flex justify-end gap-2">
          <Botao
            variante="secundario"
            tamanho="pequeno"
            aria-label={`Ver ${referenciaDoHolerite(h)}`}
            onClick={() => void baixar(h)}
          >
            Ver
          </Botao>
          <Botao
            tamanho="pequeno"
            aria-label={`Baixar PDF de ${referenciaDoHolerite(h)}`}
            onClick={() => void baixar(h)}
          >
            Baixar PDF
          </Botao>
        </span>
      ),
    },
  ]

  const c = dados?.contagens
  const anos = dados?.anosDisponiveis.length ? dados.anosDisponiveis : [anoMostrado]
  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo="Holerites"
        subtitulo="Todos os seus holerites ficam guardados aqui"
      />
      <CartaoProximoPagamento
        data={proximo.data?.pagamento?.data}
        mesReferencia={proximo.data?.competencia?.mes}
      />
      <div className="flex flex-wrap items-center justify-between gap-4">
        <Abas<Aba>
          rotulo="Tipos de holerite"
          itens={[
            { valor: 'TODOS', rotulo: 'Todos', contagem: c?.todos },
            { valor: 'MENSAL', rotulo: 'Mensais', contagem: c?.mensais },
            { valor: 'FERIAS', rotulo: 'Férias', contagem: c?.ferias },
            { valor: 'DECIMO_TERCEIRO', rotulo: '13º salário', contagem: c?.decimoTerceiro },
          ]}
          valor={tipo ?? 'TODOS'}
          aoMudar={(v) => mudar({ tipo: v === 'TODOS' ? null : v, pagina: null })}
        />
        <div className="w-36">
          <Selecao
            rotulo="Ano"
            opcoes={anos.map((a) => ({ valor: String(a), rotulo: String(a) }))}
            valor={String(anoMostrado)}
            aoMudar={(v) => v && mudar({ ano: v, pagina: null })}
          />
        </div>
      </div>
      <Tabela
        rotulo="Holerites"
        colunas={colunas}
        linhas={dados?.itens}
        chaveDaLinha={(h) => h.id}
        carregando={lista.isLoading}
        erro={lista.error ?? undefined}
        aoTentarDeNovo={() => void lista.refetch()}
        vazio={
          <EstadoVazio
            titulo={
              c && c.todos === 0
                ? `Você ainda não tem holerites em ${anoMostrado}.`
                : 'Nenhum holerite deste tipo.'
            }
          />
        }
        rodape={
          dados ? (
            <div className="flex w-full flex-wrap items-center justify-between gap-3">
              <span className="text-tabela text-texto-suave">
                {dados.total} {plural(dados.total, 'holerite', 'holerites')} em {anoMostrado}
              </span>
              {dados.totalPaginas > 1 && (
                <Paginacao
                  dados={dados}
                  aoMudar={(p) => mudar({ pagina: p > 1 ? String(p) : null })}
                />
              )}
            </div>
          ) : undefined
        }
      />
    </div>
  )
}
