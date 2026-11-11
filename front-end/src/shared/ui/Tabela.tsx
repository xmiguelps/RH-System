import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react'
import { Fragment, type ReactNode } from 'react'
import { AvisoDeErro } from './AvisoDeErro'
import { Esqueleto } from './Esqueleto'
import { cn } from './estilo'

export type DirecaoDeOrdenacao = 'ASC' | 'DESC'
export type OrdenacaoDaTabela = { campo: string; direcao: DirecaoDeOrdenacao } | null

export type ColunaDaTabela<L> = {
  chave: string
  titulo: string
  celula: (linha: L) => ReactNode
  ordenavel?: boolean
  alinhamento?: 'esquerda' | 'direita'
  largura?: string
}

type TabelaProps<L> = {
  rotulo: string
  colunas: readonly ColunaDaTabela<L>[]
  linhas: readonly L[] | undefined
  chaveDaLinha: (linha: L) => string | number
  carregando?: boolean
  erro?: unknown
  aoTentarDeNovo?: () => void
  vazio?: ReactNode
  ordenacao?: OrdenacaoDaTabela
  aoOrdenar?: (campo: string) => void
  detalhe?: (linha: L) => ReactNode | null
  rodape?: ReactNode
  linhasDoEsqueleto?: number
}

function ariaSort(coluna: string, ordenacao: OrdenacaoDaTabela | undefined) {
  if (!ordenacao || ordenacao.campo !== coluna) return 'none'
  return ordenacao.direcao === 'ASC' ? 'ascending' : 'descending'
}

export function Tabela<L>({
  rotulo,
  colunas,
  linhas,
  chaveDaLinha,
  carregando = false,
  erro,
  aoTentarDeNovo,
  vazio,
  ordenacao,
  aoOrdenar,
  detalhe,
  rodape,
  linhasDoEsqueleto = 5,
}: TabelaProps<L>) {
  const temErro = erro !== undefined && erro !== null
  const semDados = linhas === undefined
  const quantasColunas = colunas.length

  let corpo: ReactNode
  if (temErro) {
    corpo = (
      <tr>
        <td colSpan={quantasColunas} className="p-[18px]">
          <AvisoDeErro erro={erro} aoTentarDeNovo={aoTentarDeNovo} />
        </td>
      </tr>
    )
  } else if (semDados) {
    corpo = Array.from({ length: linhasDoEsqueleto }, (_, i) => (
      <tr key={i} className="border-t border-divisor">
        {colunas.map((coluna) => (
          <td key={coluna.chave} className="h-[73px] px-[18px]">
            <Esqueleto className="h-4 w-3/4" />
          </td>
        ))}
      </tr>
    ))
  } else if (linhas.length === 0) {
    corpo = (
      <tr>
        <td colSpan={quantasColunas}>{vazio}</td>
      </tr>
    )
  } else {
    corpo = linhas.map((linha) => {
      const extra = detalhe?.(linha)
      return (
        <Fragment key={chaveDaLinha(linha)}>
          <tr className="border-t border-divisor">
            {colunas.map((coluna) => (
              <td
                key={coluna.chave}
                className={cn(
                  'h-[73px] px-[18px] text-tabela text-texto',
                  coluna.alinhamento === 'direita' && 'text-right',
                )}
              >
                {coluna.celula(linha)}
              </td>
            ))}
          </tr>
          {extra && (
            <tr className="bg-superficie-suave">
              <td colSpan={quantasColunas} className="px-[18px] py-3 text-tabela text-texto">
                {extra}
              </td>
            </tr>
          )}
        </Fragment>
      )
    })
  }

  return (
    <div className="overflow-hidden rounded-cartao border border-borda bg-superficie">
      {carregando && (
        <span role="status" className="sr-only">
          Carregando…
        </span>
      )}
      <div className="overflow-x-auto">
        <table
          aria-label={rotulo}
          aria-busy={carregando || undefined}
          className="w-full border-collapse text-left"
        >
          <thead className="bg-superficie-suave">
            <tr>
              {colunas.map((coluna) => {
                const ordenavel = coluna.ordenavel && aoOrdenar
                const sentido = ariaSort(coluna.chave, ordenacao)
                const Icone =
                  sentido === 'ascending'
                    ? ArrowUp
                    : sentido === 'descending'
                      ? ArrowDown
                      : ArrowUpDown
                return (
                  <th
                    key={coluna.chave}
                    scope="col"
                    aria-sort={ordenavel ? sentido : undefined}
                    className={cn(
                      'h-[45px] px-[18px] text-tabela font-medium text-texto-suave',
                      coluna.alinhamento === 'direita' && 'text-right',
                      coluna.largura,
                    )}
                  >
                    {ordenavel ? (
                      <button
                        type="button"
                        onClick={() => aoOrdenar(coluna.chave)}
                        className="inline-flex items-center gap-1 font-medium aria-[sort]:text-primaria focus-visible:outline-2 focus-visible:outline-primaria-media"
                      >
                        {coluna.titulo}
                        <Icone size={14} strokeWidth={1.75} aria-hidden="true" />
                      </button>
                    ) : (
                      coluna.titulo
                    )}
                  </th>
                )
              })}
            </tr>
          </thead>
          <tbody>{corpo}</tbody>
        </table>
      </div>
      {rodape && <div className="border-t border-divisor px-[18px] py-3">{rodape}</div>}
    </div>
  )
}
