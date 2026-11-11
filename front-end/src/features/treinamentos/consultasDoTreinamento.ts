import { $api } from '../../shared/api/api'
import type { Rascunho } from './regrasDoTreinamento'

/** A contagem do público (os arrays vão repetindo o parâmetro: setorIds=1&setorIds=2). */
export function useContagemDoPublico(r: Rascunho, ativa = true) {
  const publico = r.publico ?? 'TODOS'
  const pronto =
    publico === 'TODOS' ||
    (publico === 'SETORES' && r.setorIds.length > 0) ||
    (publico === 'PESSOAS' && r.pessoas.length > 0)
  return $api.useQuery(
    'get',
    '/api/rh/treinamentos/publico/contagem',
    {
      params: {
        query: {
          publico,
          ...(publico === 'SETORES' ? { setorIds: r.setorIds } : {}),
          ...(publico === 'PESSOAS' ? { funcionarioIds: r.pessoas.map((p) => p.id) } : {}),
        },
      },
    },
    { enabled: ativa && pronto },
  )
}
