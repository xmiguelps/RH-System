export const CHAVE_DO_REFRESH = 'rh.refresh'

// O localStorage pode lançar (navegação privada, dados do site bloqueados): nada aqui propaga o erro.
export function lerRefresh(): string | null {
  try {
    return localStorage.getItem(CHAVE_DO_REFRESH)
  } catch {
    return null
  }
}

export function gravarRefresh(refreshToken: string): void {
  try {
    localStorage.setItem(CHAVE_DO_REFRESH, refreshToken)
  } catch {
    // sem armazenamento, a sessão vale só enquanto a aba estiver aberta
  }
}

export function limparRefresh(): void {
  try {
    localStorage.removeItem(CHAVE_DO_REFRESH)
  } catch {
    // nada a limpar quando o armazenamento está bloqueado
  }
}

let tokenEmMemoria: string | null = null

export const tokenDeAcesso = {
  obter(): string | null {
    return tokenEmMemoria
  },
  definir(valor: string | null): void {
    tokenEmMemoria = valor
  },
}
