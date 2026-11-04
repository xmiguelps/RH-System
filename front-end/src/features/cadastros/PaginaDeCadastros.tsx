import { Navigate, NavLink, useOutlet } from 'react-router'
import { formatarNumero } from '../../shared/formato/numeros'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { cn } from '../../shared/ui/estilo'
import { ITENS_DO_CADASTRO } from './itensDoCadastro'
import { useContagensDosCadastros } from './useContagensDosCadastros'

export function PaginaDeCadastros() {
  const filha = useOutlet()
  const contagens = useContagensDosCadastros()
  if (!filha) return <Navigate to="cargos" replace />
  return (
    <>
      <CabecalhoDePagina
        titulo="Cadastros"
        subtitulo="Tabelas de apoio que o RH mantém. Elas viram as opções que aparecem no resto do sistema."
      />
      <div className="grid grid-cols-[260px_1fr] gap-6">
        <nav aria-label="Cadastros">
          <ul className="flex flex-col gap-1">
            {ITENS_DO_CADASTRO.map((item) => {
              const numero = contagens[item.chave]
              return (
                <li key={item.chave}>
                  <NavLink
                    to={item.chave}
                    className={({ isActive }) =>
                      cn(
                        'flex items-center justify-between gap-3 rounded-campo px-4 py-3 hover:bg-superficie-suave',
                        isActive && 'bg-superficie-destaque',
                      )
                    }
                  >
                    <span className="flex flex-col">
                      <span className="text-corpo font-semibold text-texto">{item.titulo}</span>
                      <span className="text-pequeno text-texto-suave">{item.apoio}</span>
                    </span>
                    {item.comContagem && numero !== undefined && (
                      <span className="text-tabela font-semibold text-texto-suave">
                        {formatarNumero(numero)}
                      </span>
                    )}
                  </NavLink>
                </li>
              )
            })}
          </ul>
        </nav>
        <div className="min-w-0">{filha}</div>
      </div>
    </>
  )
}
