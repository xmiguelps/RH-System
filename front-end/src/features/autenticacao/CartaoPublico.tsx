import type { ReactNode } from 'react'
import { LogoR } from '../../shared/ui/LogoR'

/** O cartão estreito das páginas públicas (até 400 px; funciona de 360 px em diante). */
export function CartaoPublico({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <main className="flex min-h-screen items-center justify-center bg-pagina px-4 py-10">
      <div className="w-full max-w-[400px] rounded-cartao border border-borda bg-superficie p-7">
        <div className="mb-6 flex items-center gap-3">
          <LogoR tom="escuro" />
          <h1 className="text-t2 font-bold text-texto">{titulo}</h1>
        </div>
        {children}
      </div>
    </main>
  )
}
