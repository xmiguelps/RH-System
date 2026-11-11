import { LoaderCircle, type LucideIcon } from 'lucide-react'
import type { ComponentPropsWithRef } from 'react'
import { classesDoBotao, type TamanhoDeBotao, type VarianteDeBotao } from './botaoClasses'
import { cn } from './estilo'

export type { TamanhoDeBotao, VarianteDeBotao } from './botaoClasses'

export type BotaoProps = ComponentPropsWithRef<'button'> & {
  variante?: VarianteDeBotao
  tamanho?: TamanhoDeBotao
  carregando?: boolean
  icone?: LucideIcon
  larguraTotal?: boolean
}

export function Botao({
  variante = 'primario',
  tamanho = 'normal',
  carregando = false,
  icone: Icone,
  larguraTotal = false,
  type = 'button',
  disabled,
  className,
  children,
  ...resto
}: BotaoProps) {
  return (
    <button
      type={type}
      disabled={disabled || carregando}
      aria-busy={carregando || undefined}
      data-variante={variante}
      data-tamanho={tamanho}
      className={cn(classesDoBotao({ variante, tamanho, larguraTotal }), className)}
      {...resto}
    >
      {carregando ? (
        <LoaderCircle size={16} strokeWidth={1.75} aria-hidden="true" className="animate-spin" />
      ) : (
        Icone && <Icone size={16} strokeWidth={1.75} aria-hidden="true" />
      )}
      {children}
    </button>
  )
}
