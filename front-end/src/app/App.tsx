import type { createBrowserRouter } from 'react-router'
import { RouterProvider } from 'react-router/dom'

export function App({ roteador }: { roteador: ReturnType<typeof createBrowserRouter> }) {
  return <RouterProvider router={roteador} />
}
