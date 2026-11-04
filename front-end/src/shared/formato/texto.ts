function palavras(nome: string): string[] {
  return nome.trim().split(/\s+/).filter(Boolean)
}

export function iniciais(nome: string): string {
  const partes = palavras(nome)
  if (partes.length === 0) return ''
  const primeira = partes[0]?.charAt(0) ?? ''
  const ultima = partes.length > 1 ? (partes[partes.length - 1]?.charAt(0) ?? '') : ''
  return (primeira + ultima).toUpperCase()
}

export function primeiroNome(nome: string): string {
  return palavras(nome)[0] ?? ''
}

export function plural(quantidade: number, singular: string, plural: string): string {
  return quantidade === 1 ? singular : plural
}
