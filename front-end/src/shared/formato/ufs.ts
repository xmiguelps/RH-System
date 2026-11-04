import type { Esquema } from '../api/tipos'
import type { OpcaoDeSelecao } from '../ui/Selecao'

type Uf = Esquema<'EmpresaUf'>

/** As 27 siglas de EmpresaUf, na ordem do contrato. */
export const UFS: readonly Uf[] = [
  'AC',
  'AL',
  'AP',
  'AM',
  'BA',
  'CE',
  'DF',
  'ES',
  'GO',
  'MA',
  'MT',
  'MS',
  'MG',
  'PA',
  'PB',
  'PR',
  'PE',
  'PI',
  'RJ',
  'RN',
  'RS',
  'RO',
  'RR',
  'SC',
  'SP',
  'SE',
  'TO',
]

export const OPCOES_DE_UF: readonly OpcaoDeSelecao<Uf>[] = UFS.map((uf) => ({
  valor: uf,
  rotulo: uf,
}))
