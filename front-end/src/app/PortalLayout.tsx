import { useEffect, useRef } from 'react'
import { Outlet } from 'react-router'
import { MenuLateral } from './MenuLateral'

const TITULO_PADRAO = 'RH-System'

/** O título da aba é o h1 da tela que está aberta (as telas carregam aos poucos, então acompanha o que muda dentro do conteúdo). */
function useTituloDaAba(conteudo: { current: HTMLElement | null }) {
  useEffect(() => {
    const elemento = conteudo.current
    if (!elemento) return
    const atualizar = () => {
      const titulo = elemento.querySelector('h1')?.textContent?.trim()
      document.title = titulo ? `${titulo} · ${TITULO_PADRAO}` : TITULO_PADRAO
    }
    atualizar()
    const observador = new MutationObserver(atualizar)
    observador.observe(elemento, { childList: true, subtree: true, characterData: true })
    return () => {
      observador.disconnect()
      document.title = TITULO_PADRAO
    }
  }, [conteudo])
}

export function PortalLayout() {
  const conteudo = useRef<HTMLElement>(null)
  useTituloDaAba(conteudo)
  return (
    <div className="flex min-h-screen min-w-[1280px] bg-pagina">
      <div className="sticky top-0 h-screen">
        <MenuLateral />
      </div>
      <main ref={conteudo} className="min-w-0 flex-1 px-12 pt-8 pb-12">
        <Outlet />
      </main>
    </div>
  )
}
