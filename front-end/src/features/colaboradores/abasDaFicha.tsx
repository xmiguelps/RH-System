import type { ReactElement } from 'react'
import type { Esquema } from '../../shared/api/tipos'
import { AbaDados } from './AbaDados'
import { AbaPonto } from '../ponto/AbaPonto'
import { AbaDocumentos } from './AbaDocumentos'
import { AbaHolerites } from '../holerites/AbaHolerites'
import { AbaAdvertencias } from '../advertencias/AbasDeAdvertencias'

export type PropsDaAba = {
  ficha: Esquema<'FuncionarioFichaRh'>
  editando: boolean
  aoEncerrarEdicao: () => void
}

export type AbaDaFicha = {
  valor: string
  rotulo: string
  Componente: (props: PropsDaAba) => ReactElement
}

// As abas da ficha do RH. Cada fatia acrescenta a sua (Ponto: 3; Holerites e Advertências: 4;
// Férias: 7); nunca aparece aba sem componente.
export const ABAS_DA_FICHA_RH: readonly AbaDaFicha[] = [
  { valor: 'dados', rotulo: 'Dados', Componente: AbaDados },
  { valor: 'ponto', rotulo: 'Ponto', Componente: AbaPonto },
  { valor: 'holerites', rotulo: 'Holerites', Componente: AbaHolerites },
  { valor: 'documentos', rotulo: 'Documentos', Componente: AbaDocumentos },
  { valor: 'advertencias', rotulo: 'Advertências', Componente: AbaAdvertencias },
]
