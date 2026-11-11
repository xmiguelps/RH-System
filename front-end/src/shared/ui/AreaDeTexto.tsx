import { Field } from '@base-ui/react/field'
import { useState, type ChangeEvent, type ComponentPropsWithRef } from 'react'
import { cn } from './estilo'

export function AreaDeTexto({
  limite,
  onChange,
  className,
  ...resto
}: ComponentPropsWithRef<'textarea'> & { limite?: number }) {
  const inicial = String(resto.value ?? resto.defaultValue ?? '').length
  const [tamanho, setTamanho] = useState(inicial)
  const tamanhoAtual = resto.value !== undefined ? String(resto.value).length : tamanho
  const aoMudar = (evento: ChangeEvent<HTMLTextAreaElement>) => {
    setTamanho(evento.target.value.length)
    onChange?.(evento)
  }
  return (
    <div className="flex flex-col gap-1">
      <Field.Control
        render={
          <textarea
            {...resto}
            maxLength={limite ?? resto.maxLength}
            onChange={aoMudar}
            className={cn(
              'min-h-[96px] w-full rounded-campo border border-borda-campo bg-superficie px-[13px] py-[10px] text-corpo text-texto',
              'placeholder:text-texto-fraco focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primaria-media',
              'aria-invalid:border-erro-texto data-invalid:border-erro-texto',
              className,
            )}
          />
        }
      />
      {limite !== undefined && (
        <p className="self-end text-mini text-texto-suave" aria-live="polite">
          {tamanhoAtual}/{limite}
        </p>
      )}
    </div>
  )
}
