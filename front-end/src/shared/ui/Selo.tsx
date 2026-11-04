import type { ReactNode } from 'react'
import { cn } from './estilo'

export type TomDoSelo = 'neutro' | 'sucesso' | 'info' | 'alerta' | 'erro' | 'contorno'

const TONS: Record<TomDoSelo, string> = {
  neutro: 'bg-neutro-fundo text-neutro-texto',
  sucesso: 'bg-sucesso-fundo text-sucesso-texto',
  info: 'bg-info-fundo text-info-texto',
  alerta: 'bg-alerta-fundo text-alerta-texto',
  erro: 'bg-erro-fundo text-erro-texto',
  contorno: 'border border-primaria-media bg-superficie text-primaria',
}

export function Selo({
  tom = 'neutro',
  children,
  className,
}: {
  tom?: TomDoSelo
  children: ReactNode
  className?: string
}) {
  return (
    <span
      data-tom={tom}
      className={cn(
        'inline-flex h-[26px] items-center rounded-full px-[10px] text-pequeno font-semibold whitespace-nowrap',
        TONS[tom],
        className,
      )}
    >
      {children}
    </span>
  )
}
