/** 44 → '44 h'; 37.5 → '37,5 h'. */
export function formatarCargaSemanal(horas: number): string {
  return `${new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(horas)} h`
}
