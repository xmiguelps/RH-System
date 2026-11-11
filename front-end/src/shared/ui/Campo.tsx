import { Field } from '@base-ui/react/field'
import type { ReactNode } from 'react'
import { cn } from './estilo'

export type CampoProps = {
  rotulo: string
  dica?: string
  erro?: string | null
  children: ReactNode
  className?: string
}

export function Campo({ rotulo, dica, erro, children, className }: CampoProps) {
  return (
    <Field.Root invalid={!!erro} className={cn('flex flex-col gap-2', className)}>
      <Field.Label className="text-tabela font-semibold text-texto">{rotulo}</Field.Label>
      {children}
      {dica && (
        <Field.Description className="text-pequeno text-texto-suave">{dica}</Field.Description>
      )}
      {erro && (
        <Field.Error match className="text-pequeno text-erro-texto">
          {erro}
        </Field.Error>
      )}
    </Field.Root>
  )
}
