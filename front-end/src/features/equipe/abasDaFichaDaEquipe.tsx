import type { ReactElement } from 'react'
import type { Esquema } from '../../shared/api/tipos'
import { AbaPontoDaEquipe } from '../ponto/AbaPontoDaEquipe'
import { AbaAdvertenciasDaEquipe } from '../advertencias/AbasDeAdvertencias'
import { AbaTreinamentosDaEquipe } from '../treinamentos/TelasDoGestor'
import { AbaDadosDaEquipe } from './AbaDadosDaEquipe'

export type PropsDaAbaDaEquipe = { ficha: Esquema<'FuncionarioFichaGestor'> }

export type AbaDaFichaDaEquipe = {
  valor: string
  rotulo: string
  Componente: (props: PropsDaAbaDaEquipe) => ReactElement
}

// As fatias 3, 4, 6 e 7 acrescentam ponto, advertências, treinamentos e férias aqui.
export const ABAS_DA_FICHA_DA_EQUIPE: readonly AbaDaFichaDaEquipe[] = [
  { valor: 'dados', rotulo: 'Dados', Componente: AbaDadosDaEquipe },
  { valor: 'ponto', rotulo: 'Ponto', Componente: AbaPontoDaEquipe },
  { valor: 'treinamentos', rotulo: 'Treinamentos', Componente: AbaTreinamentosDaEquipe },
  { valor: 'advertencias', rotulo: 'Advertências', Componente: AbaAdvertenciasDaEquipe },
]
