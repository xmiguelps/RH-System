import { clsx, type ClassValue } from 'clsx'
import { extendTailwindMerge } from 'tailwind-merge'

// O tailwind-merge não conhece os tokens do projeto: sem isto, `text-corpo` (tamanho) e
// `text-texto` (cor) seriam tratados como o mesmo grupo e um apagaria o outro.
const mesclar = extendTailwindMerge({
  extend: {
    classGroups: {
      'font-size': [
        {
          text: [
            't1',
            'ficha',
            'numero',
            't2',
            't3',
            'corpo',
            'tabela',
            'pequeno',
            'mini',
            'relogio',
          ],
        },
      ],
      rounded: [{ rounded: ['botao', 'campo', 'cartao', 'janela', 'destaque'] }],
    },
  },
})

export function cn(...entradas: ClassValue[]): string {
  return mesclar(clsx(entradas))
}
