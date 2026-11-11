import { CircleAlert, CircleCheck, Info, TriangleAlert, type LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import { cn } from './estilo'

export type TomDoAviso = 'info' | 'alerta' | 'erro' | 'sucesso'

const TONS: Record<TomDoAviso, { classes: string; icone: LucideIcon }> = {
  info: { classes: 'bg-info-fundo text-info-texto', icone: Info },
  alerta: { classes: 'bg-alerta-fundo text-alerta-forte', icone: TriangleAlert },
  erro: { classes: 'bg-erro-fundo text-erro-texto', icone: CircleAlert },
  sucesso: { classes: 'bg-sucesso-fundo text-sucesso-texto', icone: CircleCheck },
}

export function Aviso({
  tom = 'info',
  titulo,
  children,
  acao,
}: {
  tom?: TomDoAviso
  titulo?: string
  children: ReactNode
  acao?: ReactNode
}) {
  const { classes, icone: Icone } = TONS[tom]
  return (
    <div
      role={tom === 'erro' ? 'alert' : 'status'}
      data-tom={tom}
      className={cn('flex items-start gap-3 rounded-cartao px-4 py-3 text-corpo', classes)}
    >
      <Icone size={20} strokeWidth={1.75} aria-hidden="true" className="mt-0.5 shrink-0" />
      <div className="flex-1">
        {titulo && <p className="font-semibold">{titulo}</p>}
        <div>{children}</div>
      </div>
      {acao && <div className="shrink-0">{acao}</div>}
    </div>
  )
}
