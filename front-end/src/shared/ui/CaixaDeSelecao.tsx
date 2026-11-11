import { Checkbox } from '@base-ui/react/checkbox'
import { Check } from 'lucide-react'
import type { ReactNode } from 'react'

export function CaixaDeSelecao({
  marcada,
  aoMudar,
  rotulo,
  descricao,
  desabilitada = false,
}: {
  marcada: boolean
  aoMudar: (marcada: boolean) => void
  rotulo: ReactNode
  descricao?: string
  desabilitada?: boolean
}) {
  return (
    <label className="flex cursor-pointer items-start gap-3 has-[[data-disabled]]:cursor-not-allowed has-[[data-disabled]]:opacity-50">
      <Checkbox.Root
        checked={marcada}
        onCheckedChange={(valor) => aoMudar(valor)}
        disabled={desabilitada}
        className="mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-[4px] border border-[#8fa9a6] bg-superficie focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primaria-media data-checked:border-primaria data-checked:bg-primaria"
      >
        <Checkbox.Indicator className="text-pagina">
          <Check size={14} strokeWidth={2.5} aria-hidden="true" />
        </Checkbox.Indicator>
      </Checkbox.Root>
      <span className="flex flex-col">
        <span className="text-corpo text-texto">{rotulo}</span>
        {descricao && <span className="text-pequeno text-texto-suave">{descricao}</span>}
      </span>
    </label>
  )
}
