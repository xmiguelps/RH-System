import { useEffect, useRef, useState } from 'react'
import { Aviso } from '../../shared/ui/Aviso'
import { Botao } from '../../shared/ui/Botao'

const ENDERECO_DO_SCRIPT = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit'

let carregamento: Promise<void> | null = null

/** Carrega o script do Cloudflare Turnstile uma vez (e de novo depois de uma falha). A política do site libera só esse endereço em `script-src`. */
function carregarScript(): Promise<void> {
  if (window.turnstile) return Promise.resolve()
  if (carregamento) return carregamento
  const promessa = new Promise<void>((resolver, rejeitar) => {
    const script = document.createElement('script')
    script.src = ENDERECO_DO_SCRIPT
    script.async = true
    script.defer = true
    script.addEventListener('load', () => {
      if (window.turnstile) resolver()
      else {
        script.remove()
        rejeitar(new Error('O Turnstile não ficou disponível.'))
      }
    })
    script.addEventListener('error', () => {
      script.remove()
      rejeitar(new Error('Não foi possível carregar o Turnstile.'))
    })
    document.head.appendChild(script)
  })
  carregamento = promessa
  const liberar = () => {
    if (carregamento === promessa) carregamento = null
  }
  promessa.then(liberar, liberar)
  return promessa
}

/**
 * O widget do Cloudflare Turnstile (a verificação "não sou um robô" da candidatura pública). Avisa por `aoMudar` o token que ele gera (`null` quando vence ou falha). Cada token vale **uma** tentativa e uns 5 minutos: quem usa este componente o remonta (troca a `key`) depois de qualquer erro de envio, e
 * a desmontagem remove o widget.
 */
export function CaptchaTurnstile({
  chaveDoSite,
  aoMudar,
}: {
  chaveDoSite: string
  aoMudar: (token: string | null) => void
}) {
  const conteiner = useRef<HTMLDivElement>(null)
  const aoMudarAtual = useRef(aoMudar)
  const [tentativa, setTentativa] = useState(0)
  const [falhou, setFalhou] = useState(false)

  useEffect(() => {
    aoMudarAtual.current = aoMudar
  })

  useEffect(() => {
    let cancelado = false
    let widget: string | null = null
    const avisar = (token: string | null) => aoMudarAtual.current(token)
    carregarScript().then(
      () => {
        if (cancelado || !conteiner.current || !window.turnstile) return
        widget = window.turnstile.render(conteiner.current, {
          sitekey: chaveDoSite,
          callback: (token) => avisar(token),
          'expired-callback': () => avisar(null),
          'error-callback': () => {
            avisar(null)
            setFalhou(true)
          },
        })
      },
      () => {
        if (cancelado) return
        avisar(null)
        setFalhou(true)
      },
    )
    return () => {
      cancelado = true
      if (widget !== null) window.turnstile?.remove(widget)
    }
  }, [chaveDoSite, tentativa])

  if (falhou)
    return (
      <div className="flex flex-col items-start gap-2">
        <Aviso tom="erro">Não foi possível carregar a verificação.</Aviso>
        <Botao
          variante="secundario"
          onClick={() => {
            setFalhou(false)
            setTentativa((n) => n + 1)
          }}
        >
          Tentar de novo
        </Botao>
      </div>
    )
  return <div ref={conteiner} role="group" aria-label="Verificação de segurança" />
}
