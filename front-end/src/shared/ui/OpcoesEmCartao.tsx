import { Radio } from '@base-ui/react/radio'
import { RadioGroup } from '@base-ui/react/radio-group'
import { useId } from 'react'
import { cn } from './estilo'

export type OpcaoEmCartao<V extends string = string> = {
  valor: V
  titulo: string
  descricao?: string
  desabilitada?: boolean
}

const COLUNAS = { 2: 'grid-cols-2', 3: 'grid-cols-3', 4: 'grid-cols-4' } as const

export function OpcoesEmCartao<V extends string>({
  opcoes,
  valor,
  aoMudar,
  rotulo,
  colunas = 2,
}: {
  opcoes: readonly OpcaoEmCartao<V>[]
  valor: V | null
  aoMudar: (valor: V) => void
  rotulo: string
  colunas?: 2 | 3 | 4
}) {
  const idDoRotulo = useId()
  return (
    <div className="flex flex-col gap-2">
      <span id={idDoRotulo} className="text-tabela font-semibold text-texto">
        {rotulo}
      </span>
      <RadioGroup
        aria-labelledby={idDoRotulo}
        value={valor}
        onValueChange={(novo) => aoMudar(novo as V)}
        className={cn('grid gap-3', COLUNAS[colunas])}
      >
        {opcoes.map((opcao) => (
          <label
            key={opcao.valor}
            className="flex cursor-pointer items-start gap-3 rounded-cartao border border-borda bg-superficie p-4 has-[[data-checked]]:border-2 has-[[data-checked]]:border-primaria has-[[data-disabled]]:cursor-not-allowed has-[[data-disabled]]:opacity-50"
          >
            <Radio.Root
              value={opcao.valor}
              disabled={opcao.desabilitada}
              className="mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-full border border-[#8fa9a6] bg-superficie focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primaria-media data-checked:border-primaria data-checked:bg-primaria"
            >
              <Radio.Indicator className="block h-2 w-2 rounded-full bg-superficie" />
            </Radio.Root>
            <span className="flex flex-col gap-0.5">
              <span className="text-corpo font-semibold text-texto">{opcao.titulo}</span>
              {opcao.descricao && (
                <span className="text-pequeno text-texto-suave">{opcao.descricao}</span>
              )}
            </span>
          </label>
        ))}
      </RadioGroup>
    </div>
  )
}
