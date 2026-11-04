import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

export function EstadoVazio({
  titulo,
  descricao,
  icone: Icone,
  acao,
}: {
  titulo: string
  descricao?: string
  icone?: LucideIcon
  acao?: ReactNode
}) {
  return (
    <div className="flex flex-col items-center gap-2 px-6 py-10 text-center">
      {Icone && (
        <Icone size={24} strokeWidth={1.75} aria-hidden="true" className="text-texto-suave" />
      )}
      <p className="text-corpo font-semibold text-texto">{titulo}</p>
      {descricao && <p className="text-tabela text-texto-suave">{descricao}</p>}
      {acao && <div className="mt-2">{acao}</div>}
    </div>
  )
}
