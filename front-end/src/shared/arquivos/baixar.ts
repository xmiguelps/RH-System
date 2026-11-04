function clicarELimpar(link: HTMLAnchorElement): void {
  document.body.appendChild(link)
  link.click()
  link.remove()
}

/** Arquivo guardado (documento, holerite, anexo): a API devolve um LinkTemporario; ele vem como anexo. */
export function abrirLinkTemporario(link: { url: string }): void {
  let endereco: URL
  try {
    endereco = new URL(link.url, window.location.href)
  } catch {
    throw new Error('Link de arquivo inválido.')
  }
  if (endereco.protocol !== 'https:' && endereco.protocol !== 'http:') {
    throw new Error('Link de arquivo inválido.')
  }
  const ancora = document.createElement('a')
  ancora.href = endereco.href
  ancora.rel = 'noopener noreferrer'
  ancora.target = '_blank'
  clicarELimpar(ancora)
}

export function baixarBlob(blob: Blob, nomeDoArquivo: string): void {
  const url = URL.createObjectURL(blob)
  const ancora = document.createElement('a')
  ancora.href = url
  ancora.download = nomeDoArquivo
  clicarELimpar(ancora)
  URL.revokeObjectURL(url)
}

export function nomeDoArquivoDaResposta(response: Response, padrao: string): string {
  const cabecalho = response.headers.get('content-disposition') ?? ''
  const codificado = /filename\*\s*=\s*(?:UTF-8|utf-8)''([^;]+)/.exec(cabecalho)
  if (codificado?.[1]) {
    try {
      return decodeURIComponent(codificado[1].trim())
    } catch {
      // cai no filename simples
    }
  }
  const simples = /filename\s*=\s*"?([^";]+)"?/.exec(cabecalho)
  return simples?.[1]?.trim() || padrao
}

/** Exportação (CSV, xlsx): a API devolve os bytes. Um erro da API chega como ErroDeApi e nada baixa. */
export async function baixarExportacao(
  chamada: Promise<{ data?: Blob; response: Response }>,
  nomePadrao: string,
): Promise<void> {
  const { data, response } = await chamada
  if (!data) return
  baixarBlob(data, nomeDoArquivoDaResposta(response, nomePadrao))
}
