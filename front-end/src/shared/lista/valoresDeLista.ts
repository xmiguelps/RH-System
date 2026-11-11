/** Valor da URL só se for um dos válidos (valor desconhecido vale o padrão da tela). */
export function valorDeEnum<V extends string>(
  valor: string | undefined,
  validos: readonly V[],
): V | undefined {
  return valor !== undefined && (validos as readonly string[]).includes(valor)
    ? (valor as V)
    : undefined
}

/** Só inteiro maior ou igual a 1. */
export function inteiroDaUrl(valor: string | undefined): number | undefined {
  if (!valor || !/^\d+$/.test(valor)) return undefined
  const numero = Number(valor)
  return numero >= 1 ? numero : undefined
}
