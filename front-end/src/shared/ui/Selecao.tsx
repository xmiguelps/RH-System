import { Select } from '@base-ui/react/select'
import { Check, ChevronDown } from 'lucide-react'
import { cn } from './estilo'

export type OpcaoDeSelecao<V extends string = string> = {
  valor: V
  rotulo: string
  desabilitada?: boolean
}

type SelecaoProps<V extends string> = {
  opcoes: readonly OpcaoDeSelecao<V>[]
  valor: V | null
  aoMudar: (valor: V | null) => void
  rotulo: string
  placeholder?: string
  desabilitado?: boolean
  carregando?: boolean
  invalido?: boolean
  aoPerderFoco?: () => void
}

export function Selecao<V extends string>({
  opcoes,
  valor,
  aoMudar,
  rotulo,
  placeholder = 'Selecione',
  desabilitado = false,
  carregando = false,
  invalido = false,
  aoPerderFoco,
}: SelecaoProps<V>) {
  const itens = opcoes.map((opcao) => ({ value: opcao.valor, label: opcao.rotulo }))
  return (
    <Select.Root
      items={itens}
      value={valor}
      onValueChange={(novo) => aoMudar((novo as V | null) ?? null)}
      disabled={desabilitado || carregando}
    >
      <Select.Trigger
        aria-label={rotulo}
        aria-invalid={invalido || undefined}
        onBlur={aoPerderFoco}
        className={cn(
          'flex h-11 w-full items-center justify-between gap-3 rounded-campo border border-borda-campo bg-superficie px-[13px] text-left text-corpo text-texto',
          'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primaria-media',
          'aria-invalid:border-erro-texto data-invalid:border-erro-texto data-disabled:cursor-not-allowed data-disabled:opacity-50',
        )}
      >
        {carregando ? (
          <span className="text-texto-suave">Carregando…</span>
        ) : (
          <Select.Value
            placeholder={placeholder}
            className="truncate data-placeholder:text-texto-fraco"
          />
        )}
        <Select.Icon className="text-texto-suave">
          <ChevronDown size={16} strokeWidth={1.75} aria-hidden="true" />
        </Select.Icon>
      </Select.Trigger>
      <Select.Portal>
        <Select.Positioner sideOffset={4} alignItemWithTrigger={false} className="z-50">
          <Select.Popup className="max-h-[var(--available-height)] min-w-[var(--anchor-width)] overflow-y-auto rounded-campo border border-borda bg-superficie py-1 shadow-lg">
            <Select.List>
              {opcoes.map((opcao) => (
                <Select.Item
                  key={opcao.valor}
                  value={opcao.valor}
                  disabled={opcao.desabilitada}
                  className="flex cursor-default items-center gap-2 px-3 py-2 text-corpo text-texto outline-none data-disabled:opacity-50 data-highlighted:bg-superficie-suave"
                >
                  <Select.ItemIndicator className="w-4 text-primaria">
                    <Check size={16} strokeWidth={1.75} aria-hidden="true" />
                  </Select.ItemIndicator>
                  <Select.ItemText>{opcao.rotulo}</Select.ItemText>
                </Select.Item>
              ))}
            </Select.List>
          </Select.Popup>
        </Select.Positioner>
      </Select.Portal>
    </Select.Root>
  )
}
