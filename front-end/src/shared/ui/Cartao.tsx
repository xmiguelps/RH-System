import type { ComponentPropsWithoutRef } from 'react'
import { cn } from './estilo'

export type TomDoCartao = 'branco' | 'suave' | 'destaque' | 'escuro' | 'tracejado'

const TONS: Record<TomDoCartao, string> = {
  branco: 'rounded-cartao border border-borda bg-superficie',
  suave: 'rounded-cartao bg-superficie-suave',
  destaque: 'rounded-[16px] bg-superficie-destaque',
  escuro: 'rounded-destaque bg-primaria text-pagina',
  tracejado: 'rounded-cartao border border-dashed border-tracejado bg-[#fafcfb]',
}

export function Cartao({
  tom = 'branco',
  semRecuo = false,
  className,
  ...resto
}: ComponentPropsWithoutRef<'section'> & { tom?: TomDoCartao; semRecuo?: boolean }) {
  return (
    <section
      data-tom={tom}
      className={cn(TONS[tom], !semRecuo && (tom === 'escuro' ? 'p-7' : 'p-[21px]'), className)}
      {...resto}
    />
  )
}
