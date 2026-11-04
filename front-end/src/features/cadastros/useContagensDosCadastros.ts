import { $api } from '../../shared/api/api'
import type { ChaveDoCadastro } from './itensDoCadastro'

const UM = { params: { query: { pagina: 1, tamanhoPagina: 1 } } }

/** Os números do menu de Cadastros: o `total` de cada lista (Gestores: a quantidade de itens). */
export function useContagensDosCadastros(): Partial<Record<ChaveDoCadastro, number>> {
  const sem = { retry: false } as const
  const cargos = $api.useQuery('get', '/api/rh/cargos', UM, sem)
  const setores = $api.useQuery('get', '/api/rh/setores', UM, sem)
  const beneficios = $api.useQuery('get', '/api/rh/beneficios', UM, sem)
  const usuarios = $api.useQuery('get', '/api/rh/usuarios', UM, sem)
  const gestores = $api.useQuery('get', '/api/rh/gestores', {}, sem)
  return {
    cargos: cargos.data?.total,
    setores: setores.data?.total,
    beneficios: beneficios.data?.total,
    usuarios: usuarios.data?.total,
    gestores: gestores.data?.itens.length,
  }
}
