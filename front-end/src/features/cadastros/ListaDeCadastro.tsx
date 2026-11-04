import { Plus } from 'lucide-react'
import { useEffect, type ReactNode } from 'react'
import { formatarNumero } from '../../shared/formato/numeros'
import { Botao } from '../../shared/ui/Botao'
import { CampoDeBusca } from '../../shared/ui/CampoDeBusca'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Paginacao } from '../../shared/ui/Paginacao'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'

type Dados<L> = {
  itens: readonly L[]
  pagina?: number
  tamanhoPagina?: number
  total?: number
  totalPaginas?: number
}

/** O padrão de todo cadastro: título, apoio, "Novo ...", busca, tabela e "Mostrando N de M". */
export function ListaDeCadastro<L>({
  titulo,
  apoio,
  rotuloDaTabela,
  botaoNovo,
  colunas,
  chaveDaLinha,
  dados,
  carregando,
  erro,
  aoTentarDeNovo,
  busca,
  aoMudarPagina,
  vazio,
  vazioComBusca,
  filtros,
}: {
  titulo: string
  apoio: string
  rotuloDaTabela: string
  botaoNovo?: { rotulo: string; aoClicar: () => void }
  colunas: readonly ColunaDaTabela<L>[]
  chaveDaLinha: (linha: L) => string | number
  dados: Dados<L> | undefined
  carregando: boolean
  erro: unknown
  aoTentarDeNovo: () => void
  busca?: { valor: string; aoMudar: (texto: string) => void }
  aoMudarPagina?: (pagina: number) => void
  vazio: string
  vazioComBusca: string
  filtros?: ReactNode
}) {
  const pagina = dados?.pagina ?? 1
  const paginaVazia = !!dados && dados.itens.length === 0 && pagina > 1
  // Excluir o último item de uma página deixa a página vazia: volta para a primeira.
  useEffect(() => {
    if (paginaVazia) aoMudarPagina?.(1)
  }, [paginaVazia, aoMudarPagina])

  const temErro = erro !== undefined && erro !== null
  const total = dados?.total ?? dados?.itens.length ?? 0

  return (
    <section aria-label={titulo} className="flex flex-col gap-4">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h2 className="text-t2 font-bold text-texto">{titulo}</h2>
          <p className="mt-1 text-corpo text-texto-suave">{apoio}</p>
        </div>
        {botaoNovo && (
          <Botao icone={Plus} onClick={botaoNovo.aoClicar}>
            {botaoNovo.rotulo}
          </Botao>
        )}
      </div>
      {(busca || filtros) && (
        <div className="flex flex-wrap gap-3">
          {busca && (
            <div className="min-w-[280px] flex-1">
              <CampoDeBusca
                valor={busca.valor}
                aoMudar={busca.aoMudar}
                placeholder="Buscar neste cadastro"
                rotulo="Buscar neste cadastro"
              />
            </div>
          )}
          {filtros}
        </div>
      )}
      <Tabela
        rotulo={rotuloDaTabela}
        colunas={colunas}
        linhas={temErro ? undefined : dados?.itens}
        chaveDaLinha={chaveDaLinha}
        carregando={carregando}
        erro={temErro ? erro : undefined}
        aoTentarDeNovo={aoTentarDeNovo}
        vazio={<EstadoVazio titulo={busca?.valor ? vazioComBusca : vazio} />}
        rodape={
          dados && dados.itens.length > 0 ? (
            dados.totalPaginas && dados.totalPaginas > 1 && aoMudarPagina ? (
              <Paginacao
                dados={{
                  pagina,
                  tamanhoPagina: dados.tamanhoPagina ?? 20,
                  total,
                  totalPaginas: dados.totalPaginas,
                }}
                aoMudar={aoMudarPagina}
              />
            ) : (
              <p className="text-tabela text-texto-suave">
                {`Mostrando ${formatarNumero(dados.itens.length)} de ${formatarNumero(total)}`}
              </p>
            )
          ) : undefined
        }
      />
    </section>
  )
}
