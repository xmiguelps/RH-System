/**
 * Um `window.turnstile` falso para os testes (o jsdom não carrega o script do Cloudflare). Cada `render` guarda as opções que o componente passou; o teste
 * chama `resolver(token)` para simular a pessoa passando na verificação, `expirar()` para o token vencer e `falhar()` para o widget dar erro.
 */
export type OpcoesDoTurnstile = {
  sitekey: string
  callback: (token: string) => void
  'expired-callback'?: () => void
  'error-callback'?: () => void
}

export type TurnstileFalso = {
  renders: { conteiner: HTMLElement; opcoes: OpcoesDoTurnstile; id: string }[]
  removidos: string[]
  resolver: (token: string, indice?: number) => void
  expirar: (indice?: number) => void
  falhar: (indice?: number) => void
  desinstalar: () => void
}

export function instalarTurnstileFalso(opcoes: { automatico?: string } = {}): TurnstileFalso {
  const renders: TurnstileFalso['renders'] = []
  const removidos: string[] = []
  window.turnstile = {
    render: (conteiner, o) => {
      const id = `widget-${renders.length + 1}`
      renders.push({ conteiner, opcoes: o, id })
      if (opcoes.automatico)
        queueMicrotask(() => o.callback(`${opcoes.automatico}-${renders.length}`))
      return id
    },
    remove: (id) => void removidos.push(id),
    reset: () => undefined,
  }
  const ultimo = (indice?: number) => renders[indice ?? renders.length - 1]!
  return {
    renders,
    removidos,
    resolver: (token, indice) => ultimo(indice).opcoes.callback(token),
    expirar: (indice) => ultimo(indice).opcoes['expired-callback']?.(),
    falhar: (indice) => ultimo(indice).opcoes['error-callback']?.(),
    desinstalar: () => {
      delete window.turnstile
    },
  }
}
