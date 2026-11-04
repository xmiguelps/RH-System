import { Link, type LinkProps } from 'react-router'
import { classesDoBotao, type TamanhoDeBotao, type VarianteDeBotao } from './botaoClasses'
import { cn } from './estilo'

export type BotaoLinkProps = LinkProps & {
  variante?: VarianteDeBotao
  tamanho?: TamanhoDeBotao
  larguraTotal?: boolean
}

export function BotaoLink({
  variante = 'primario',
  tamanho = 'normal',
  larguraTotal = false,
  className,
  ...resto
}: BotaoLinkProps) {
  return (
    <Link
      data-variante={variante}
      data-tamanho={tamanho}
      className={cn(
        classesDoBotao({ variante, tamanho, larguraTotal }),
        typeof className === 'string' ? className : undefined,
      )}
      {...resto}
    />
  )
}
