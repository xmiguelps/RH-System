export function validarArquivo(
  arquivo: File,
  regras: { tiposAceitos: readonly string[]; tamanhoMaximoMb: number },
): string | null {
  if (!regras.tiposAceitos.includes(arquivo.type)) return 'Esse tipo de arquivo não é aceito.'
  if (arquivo.size === 0) return 'O arquivo está vazio.'
  if (arquivo.size > regras.tamanhoMaximoMb * 1024 * 1024) {
    return `O arquivo passa de ${regras.tamanhoMaximoMb} MB.`
  }
  return null
}
