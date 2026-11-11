import { Outlet } from 'react-router'
import { MenuLateral } from './MenuLateral'

export function PortalLayout() {
  return (
    <div className="flex min-h-screen min-w-[1280px] bg-pagina">
      <div className="sticky top-0 h-screen">
        <MenuLateral />
      </div>
      <main className="min-w-0 flex-1 px-12 pt-8 pb-12">
        <Outlet />
      </main>
    </div>
  )
}
