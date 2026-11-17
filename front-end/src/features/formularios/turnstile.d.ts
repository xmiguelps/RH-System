export {}

/** O que a página do Cloudflare Turnstile (`api.js?render=explicit`) põe em `window` (só o que o app usa). */
declare global {
  interface Window {
    turnstile?: {
      render: (
        conteiner: HTMLElement,
        opcoes: {
          sitekey: string
          callback: (token: string) => void
          'expired-callback'?: () => void
          'error-callback'?: () => void
        },
      ) => string
      remove: (id: string) => void
      reset: (id?: string) => void
    }
  }
}
