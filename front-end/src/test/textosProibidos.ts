const ROTULOS =
  'h1, h2, h3, legend, label, button, th, [role="tab"], [role="columnheader"], [role="status"], [role="alert"]'

/**
 * Só o texto dos rótulos da tela (títulos, legendas, botões, cabeçalhos de coluna, abas, avisos) e,
 * nos diálogos (que abrem em portal, fora do contêiner), também os parágrafos. Células e dados dos
 * exemplos do contrato ficam de fora: senão um nome de pessoa daria falso positivo.
 */
export function rotulosDaTela(conteiner: HTMLElement): string {
  const partes: string[] = []
  for (const el of conteiner.querySelectorAll(ROTULOS)) partes.push(el.textContent ?? '')
  for (const dialogo of conteiner.ownerDocument.querySelectorAll('[role="dialog"]')) {
    for (const el of dialogo.querySelectorAll(`${ROTULOS}, p`)) partes.push(el.textContent ?? '')
  }
  return partes.join('\n')
}

function escapar(texto: string): string {
  return texto.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

/** Palavra inteira, sem caixa; o `\b` do JavaScript falha em palavra que começa com acento. */
export function acharTextoProibido(rotulos: string, texto: string): boolean {
  return new RegExp(`(?<![\\p{L}\\p{N}])${escapar(texto)}(?![\\p{L}\\p{N}])`, 'iu').test(rotulos)
}
