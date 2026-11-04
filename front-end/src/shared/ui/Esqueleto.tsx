import { cn } from './estilo'

export function Esqueleto({ className }: { className?: string }) {
  return (
    <div
      aria-hidden="true"
      className={cn('animate-pulse rounded-campo bg-superficie-destaque', className)}
    />
  )
}
