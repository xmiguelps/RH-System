/** Minúsculas e sem acento, para comparar textos digitados com nomes ("logis" acha "Logística"). */
export function normalizarParaBusca(texto: string): string {
  return texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim()
}
