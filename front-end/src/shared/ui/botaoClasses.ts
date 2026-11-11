import { cva } from 'class-variance-authority'

export type VarianteDeBotao = 'primario' | 'secundario' | 'texto' | 'perigo' | 'claro'
export type TamanhoDeBotao = 'normal' | 'pequeno'

const botao = cva(
  [
    'inline-flex items-center justify-center gap-2 font-semibold whitespace-nowrap',
    'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primaria-media',
    'disabled:cursor-not-allowed disabled:opacity-50 aria-disabled:cursor-not-allowed aria-disabled:opacity-50',
  ],
  {
    variants: {
      variante: {
        primario: 'rounded-botao bg-primaria text-pagina hover:bg-primaria-ativa',
        secundario:
          'rounded-[9px] border border-primaria-media bg-superficie text-primaria hover:bg-superficie-destaque',
        texto: 'rounded-botao px-1 text-primaria-media underline-offset-2 hover:underline',
        perigo:
          'rounded-[9px] border border-erro-texto bg-superficie text-erro-texto hover:bg-erro-fundo',
        claro: 'rounded-botao bg-primaria-clara text-[#0b3f44] hover:opacity-90',
      },
      tamanho: {
        normal: 'h-11 px-5 text-corpo',
        pequeno: 'h-10 px-4 text-tabela',
      },
      larguraTotal: {
        true: 'w-full',
        false: '',
      },
    },
    compoundVariants: [
      { variante: 'texto', tamanho: 'normal', className: 'h-auto px-1' },
      { variante: 'texto', tamanho: 'pequeno', className: 'h-auto px-1' },
      { variante: 'claro', tamanho: 'normal', className: 'h-[51px]' },
    ],
    defaultVariants: { variante: 'primario', tamanho: 'normal', larguraTotal: false },
  },
)

export function classesDoBotao(opcoes: {
  variante?: VarianteDeBotao
  tamanho?: TamanhoDeBotao
  larguraTotal?: boolean
}): string {
  return botao(opcoes)
}
