import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router'

type Direcao = 'ASC' | 'DESC'
type Ordenacao = { campo: string; direcao: Direcao }

export type ConsultaDeLista<F extends string> = {
  pagina: number
  tamanhoPagina: number
  busca?: string
  ordenarPor?: string
  direcao?: Direcao
} & { [K in F]?: string }

function lerPagina(texto: string | null): number {
  if (!texto || !/^\d+$/.test(texto)) return 1
  const numero = Number(texto)
  return numero >= 1 ? numero : 1
}

/** Busca, filtros, página e ordenação vivem na URL: o Voltar do navegador mantém o filtro. */
export function useEstadoDaLista<F extends string>(opcoes: {
  filtros: readonly F[]
  tamanhoPagina?: number
  ordenacaoPadrao?: Ordenacao
}) {
  const [parametros, definirParametros] = useSearchParams()
  const { filtros: nomesDosFiltros, tamanhoPagina = 20, ordenacaoPadrao } = opcoes

  // Chaves em texto: o objeto da consulta só muda quando a URL ou as opções mudam de verdade.
  const textoDaUrl = parametros.toString()
  const listaDeFiltros = nomesDosFiltros.join(',')
  const campoPadrao = ordenacaoPadrao?.campo ?? null
  const direcaoPadrao = ordenacaoPadrao?.direcao ?? null

  const estado = useMemo(() => {
    const p = new URLSearchParams(textoDaUrl)
    const ordenarPor = p.get('ordenarPor')
    const ordenacao: Ordenacao | null = ordenarPor
      ? { campo: ordenarPor, direcao: p.get('direcao') === 'DESC' ? 'DESC' : 'ASC' }
      : campoPadrao
        ? { campo: campoPadrao, direcao: direcaoPadrao ?? 'ASC' }
        : null
    const filtros: Partial<Record<F, string>> = {}
    for (const nome of listaDeFiltros.split(',').filter(Boolean) as F[]) {
      const valor = p.get(nome)
      if (valor) filtros[nome] = valor
    }
    const busca = p.get('busca') ?? ''
    const pagina = lerPagina(p.get('pagina'))
    const consulta: Record<string, string | number> = { pagina, tamanhoPagina }
    if (busca) consulta.busca = busca
    if (ordenacao) {
      consulta.ordenarPor = ordenacao.campo
      consulta.direcao = ordenacao.direcao
    }
    Object.assign(consulta, filtros)
    return { ordenacao, filtros, busca, pagina, consulta: consulta as ConsultaDeLista<F> }
  }, [textoDaUrl, listaDeFiltros, campoPadrao, direcaoPadrao, tamanhoPagina])

  const { ordenacao, filtros, busca, pagina, consulta } = estado

  const mudar = useCallback(
    (alterar: (p: URLSearchParams) => void, opcoesDeNavegacao?: { replace?: boolean }) => {
      definirParametros((atual) => {
        const novo = new URLSearchParams(atual)
        alterar(novo)
        for (const [chave, valor] of [...novo.entries()]) if (!valor) novo.delete(chave)
        if (novo.get('pagina') === '1') novo.delete('pagina')
        return novo
      }, opcoesDeNavegacao)
    },
    [definirParametros],
  )

  const definirBusca = useCallback(
    (texto: string) =>
      mudar(
        (p) => {
          p.set('busca', texto.trim())
          p.delete('pagina')
        },
        { replace: true },
      ),
    [mudar],
  )

  const definirFiltro = useCallback(
    (nome: F, valor: string | null) =>
      mudar((p) => {
        p.set(nome, valor ?? '')
        p.delete('pagina')
      }),
    [mudar],
  )

  const definirPagina = useCallback(
    (novaPagina: number) => mudar((p) => p.set('pagina', String(novaPagina))),
    [mudar],
  )

  const alternarOrdenacao = useCallback(
    (campo: string) =>
      mudar((p) => {
        const mesmo = ordenacao?.campo === campo
        p.set('ordenarPor', campo)
        p.set('direcao', mesmo && ordenacao?.direcao === 'ASC' ? 'DESC' : 'ASC')
        p.delete('pagina')
      }),
    [mudar, ordenacao?.campo, ordenacao?.direcao],
  )

  return {
    consulta,
    busca,
    pagina,
    filtros,
    ordenacao,
    definirBusca,
    definirFiltro,
    definirPagina,
    alternarOrdenacao,
  }
}
