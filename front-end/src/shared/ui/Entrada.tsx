import { Input } from '@base-ui/react/input'
import type { LucideIcon } from 'lucide-react'
import type { ChangeEvent, ComponentPropsWithRef } from 'react'
import { CLASSES_DE_CAMPO } from './classesDeCampo'
import { cn } from './estilo'

export type EntradaProps = Omit<ComponentPropsWithRef<'input'>, 'size'> & {
  icone?: LucideIcon
  mascara?: (valor: string) => string
}

export function Entrada({ icone: Icone, mascara, onChange, className, ...resto }: EntradaProps) {
  const aoMudar = (evento: ChangeEvent<HTMLInputElement>) => {
    if (mascara) evento.target.value = mascara(evento.target.value)
    onChange?.(evento)
  }
  const campo = (
    <Input
      {...resto}
      onChange={aoMudar}
      className={cn(CLASSES_DE_CAMPO, Icone && 'pl-10', className)}
    />
  )
  if (!Icone) return campo
  return (
    <div className="relative">
      <Icone
        size={16}
        strokeWidth={1.75}
        aria-hidden="true"
        className="pointer-events-none absolute top-1/2 left-[13px] -translate-y-1/2 text-texto-suave"
      />
      {campo}
    </div>
  )
}
