/** Tipo pela extensão, só para o arquivo que chega sem `type` (o navegador no Windows nem sempre conhece a extensão). */
const TIPO_PELA_EXTENSAO: Readonly<Record<string, string>> = {
  pdf: 'application/pdf',
  jpg: 'image/jpeg',
  jpeg: 'image/jpeg',
  png: 'image/png',
  xlsx: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  mp4: 'video/mp4',
  webm: 'video/webm',
}

function tipoDoArquivo(arquivo: File): string {
  if (arquivo.type) return arquivo.type
  const ponto = arquivo.name.lastIndexOf('.')
  if (ponto < 0) return ''
  return TIPO_PELA_EXTENSAO[arquivo.name.slice(ponto + 1).toLowerCase()] ?? ''
}

export function validarArquivo(
  arquivo: File,
  regras: { tiposAceitos: readonly string[]; tamanhoMaximoMb: number },
): string | null {
  if (!regras.tiposAceitos.includes(tipoDoArquivo(arquivo)))
    return 'Esse tipo de arquivo não é aceito.'
  if (arquivo.size === 0) return 'O arquivo está vazio.'
  if (arquivo.size > regras.tamanhoMaximoMb * 1024 * 1024) {
    return `O arquivo passa de ${regras.tamanhoMaximoMb} MB.`
  }
  return null
}
