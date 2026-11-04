import { cn } from './estilo'

// Sem arquivo de imagem: a política do site barra imagem data: e o Vite embutiria um SVG pequeno.
export function LogoR({ tom = 'claro' }: { tom?: 'claro' | 'escuro' }) {
  return (
    <span
      aria-hidden="true"
      data-tom={tom}
      className={cn(
        'inline-flex h-[34px] w-[38px] shrink-0 items-center justify-center rounded-[9px] text-t3 font-bold',
        tom === 'claro' ? 'bg-primaria-clara text-primaria' : 'bg-primaria text-primaria-clara',
      )}
    >
      R
    </span>
  )
}
