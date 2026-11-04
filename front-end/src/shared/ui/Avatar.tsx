import { iniciais } from '../formato/texto'
import { cn } from './estilo'

export type TamanhoDoAvatar = 'lista' | 'menu' | 'ficha'

const TAMANHOS: Record<TamanhoDoAvatar, string> = {
  lista: 'h-9 w-9 bg-avatar-fundo text-pequeno text-primaria',
  menu: 'h-[42px] w-[42px] bg-primaria-media text-corpo text-menu-texto',
  ficha: 'h-[76px] w-[76px] bg-primaria text-t3 text-pagina',
}

export function Avatar({ nome, tamanho = 'lista' }: { nome: string; tamanho?: TamanhoDoAvatar }) {
  return (
    <span
      aria-hidden="true"
      data-tamanho={tamanho}
      className={cn(
        'inline-flex shrink-0 items-center justify-center rounded-full font-bold',
        TAMANHOS[tamanho],
      )}
    >
      {iniciais(nome)}
    </span>
  )
}
