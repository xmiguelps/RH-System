import { useCallback } from 'react'
import { $api } from '../../shared/api/api'
import type { OpcaoDeSelecao } from '../../shared/ui/Selecao'

export type OpcoesDeApoio = {
  cargos: readonly OpcaoDeSelecao[]
  setores: readonly OpcaoDeSelecao[]
  beneficios: readonly { id: number; nome: string }[]
  /** undefined: nenhum setor escolhido; null: o setor não tem gestor; senão o nome do gestor. */
  gestorDoSetor(setorId: string | null | undefined): string | null | undefined
  carregando: boolean
  erro: unknown
  tentarDeNovo(): void
}

const TODOS = { params: { query: { pagina: 1, tamanhoPagina: 100 } } }

/** Cargos, setores e benefícios do cadastro (telas 02 e 03). */
export function useOpcoesDeApoio(): OpcoesDeApoio {
  const cargos = $api.useQuery('get', '/api/rh/cargos', TODOS)
  const setores = $api.useQuery('get', '/api/rh/setores', TODOS)
  const beneficios = $api.useQuery('get', '/api/rh/beneficios', TODOS)

  const refazerCargos = cargos.refetch
  const refazerSetores = setores.refetch
  const refazerBeneficios = beneficios.refetch
  const tentarDeNovo = useCallback(() => {
    void refazerCargos()
    void refazerSetores()
    void refazerBeneficios()
  }, [refazerCargos, refazerSetores, refazerBeneficios])

  const listaDeSetores = setores.data?.itens ?? []
  return {
    cargos: (cargos.data?.itens ?? []).map((c) => ({ valor: String(c.id), rotulo: c.nome })),
    setores: listaDeSetores.map((s) => ({ valor: String(s.id), rotulo: s.nome })),
    beneficios: [...(beneficios.data?.itens ?? [])]
      .sort((a, b) => a.id - b.id)
      .map((b) => ({ id: b.id, nome: b.nome })),
    gestorDoSetor(setorId) {
      if (!setorId) return undefined
      const setor = listaDeSetores.find((s) => String(s.id) === setorId)
      if (!setor) return undefined
      return setor.gestor?.nome ?? null
    },
    carregando: cargos.isLoading || setores.isLoading || beneficios.isLoading,
    erro: cargos.error ?? setores.error ?? beneficios.error ?? null,
    tentarDeNovo,
  }
}
