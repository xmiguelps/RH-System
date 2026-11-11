import { Outlet } from 'react-router'
import { useAuth } from '../features/autenticacao/useAuth'
import { Carregando } from '../shared/ui/Carregando'

export function RaizDoApp() {
  const { carregando } = useAuth()
  if (carregando) {
    return (
      <div className="p-8">
        <Carregando />
      </div>
    )
  }
  return <Outlet />
}
