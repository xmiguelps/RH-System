import './shared/zod'
import './index.css'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter } from 'react-router'
import { App } from './app/App'
import { Provedores } from './app/Provedores'
import { criarRotas } from './app/rotas'

async function iniciar() {
  if (import.meta.env.DEV && import.meta.env.VITE_MOCKS !== 'nao') {
    const { worker } = await import('./shared/mocks/navegador')
    await worker.start({ onUnhandledFrame: 'bypass' })
  }
  const roteador = createBrowserRouter(criarRotas())
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <Provedores>
        <App roteador={roteador} />
      </Provedores>
    </StrictMode>,
  )
}

void iniciar()
