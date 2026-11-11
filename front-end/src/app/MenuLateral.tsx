import { NavLink } from 'react-router'
import { useAuth } from '../features/autenticacao/useAuth'
import { Bolinha } from '../shared/ui/Bolinha'
import { cn } from '../shared/ui/estilo'
import { LogoR } from '../shared/ui/LogoR'
import { menuVisivel } from './menu'
import { RodapeDoMenu } from './RodapeDoMenu'
import { caminhosDasRotas } from './tabelaDeRotas'
import { useContadoresDoMenu } from './useContadoresDoMenu'
import { useEmpresa } from './useEmpresa'

export function MenuLateral({ caminhosExistentes }: { caminhosExistentes?: ReadonlySet<string> }) {
  const { usuario } = useAuth()
  const empresa = useEmpresa()
  const perfil = usuario?.perfil ?? 'Funcionario'
  const contadores = useContadoresDoMenu(perfil)
  const grupos = usuario ? menuVisivel(perfil, caminhosExistentes ?? caminhosDasRotas()) : []

  return (
    <aside className="flex h-screen w-menu shrink-0 flex-col border-r border-menu-borda bg-primaria text-menu-texto">
      <div className="flex items-center gap-3 px-6 pt-6 pb-4">
        <LogoR />
        <div className="min-w-0">
          <p className="text-[18px] font-semibold">Portal de RH</p>
          {empresa && <p className="truncate text-pequeno text-menu-apoio">{empresa}</p>}
        </div>
      </div>
      <nav aria-label="Menu principal" className="flex-1 overflow-y-auto px-[14px] pb-4">
        {grupos.map((grupo) => (
          <div key={grupo.rotulo} className="mt-5">
            <p className="px-3 pb-2 text-mini text-menu-rotulo">{grupo.rotulo}</p>
            <ul className="flex flex-col gap-[2.5px]">
              {grupo.itens.map((item) => (
                <li key={item.caminho}>
                  <NavLink
                    to={item.caminho}
                    end
                    className={({ isActive }) =>
                      cn(
                        'flex h-11 items-center justify-between rounded-campo px-3 text-corpo font-medium text-menu-texto hover:bg-primaria-suave focus-visible:outline-2 focus-visible:outline-primaria-clara',
                        isActive && 'bg-primaria-ativa',
                      )
                    }
                  >
                    {item.rotulo}
                    {item.contador && <Bolinha valor={contadores[item.contador]} />}
                  </NavLink>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </nav>
      <RodapeDoMenu />
    </aside>
  )
}
