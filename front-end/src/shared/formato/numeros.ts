const MENOS = '−'

export function formatarMinutosComoHoras(
  minutos: number,
  opcoes: { sinal?: boolean } = {},
): string {
  const absoluto = Math.abs(Math.trunc(minutos))
  const horas = Math.floor(absoluto / 60)
  const resto = String(absoluto % 60).padStart(2, '0')
  const valor = `${horas}h${resto}`
  if (minutos < 0) return `${MENOS}${valor}`
  if (opcoes.sinal && minutos > 0) return `+${valor}`
  return valor
}

const formatoDeReais = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const formatoDeNumero = new Intl.NumberFormat('pt-BR')

export function formatarReais(centavos: number): string {
  // O Intl separa "R$" do número com um espaço sem quebra; a tela usa o espaço comum.
  return formatoDeReais.format(centavos / 100).replace(/\s/g, ' ')
}

export function formatarNumero(n: number): string {
  return formatoDeNumero.format(n)
}
