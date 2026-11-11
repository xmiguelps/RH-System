import { exemplosPorOperacao } from '../mocks/gerado/exemplos'

function listar(chaves: string[]): string {
  return chaves.length > 0 ? chaves.join(', ') : '(nada)'
}

export function exemploDoContrato<T = unknown>(
  operationId: string,
  status?: number,
  nome?: string,
): T {
  const porStatus = exemplosPorOperacao[operationId]
  if (!porStatus) {
    throw new Error(
      `A operação "${operationId}" não tem exemplos no contrato; tem: ${listar(Object.keys(exemplosPorOperacao).slice(0, 20))}...`,
    )
  }
  const codigos = Object.keys(porStatus)
  const codigo = status === undefined ? codigos.find((c) => /^2\d\d$/.test(c)) : String(status)
  const porNome = codigo === undefined ? undefined : porStatus[codigo]
  if (!porNome) {
    throw new Error(
      `A operação "${operationId}" não tem exemplo para o status ${codigo ?? '2xx'}; tem: ${listar(codigos)}`,
    )
  }
  const nomes = Object.keys(porNome)
  const escolhido = nome ?? nomes[0]
  if (escolhido === undefined || !(escolhido in porNome)) {
    throw new Error(
      `A operação "${operationId}" (status ${codigo}) não tem o exemplo "${escolhido}"; tem: ${listar(nomes)}`,
    )
  }
  return JSON.parse(JSON.stringify(porNome[escolhido])) as T
}
