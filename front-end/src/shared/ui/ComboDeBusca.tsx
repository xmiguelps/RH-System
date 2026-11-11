import { Combobox } from '@base-ui/react/combobox'
import { Check, ChevronDown } from 'lucide-react'
import { CLASSES_DE_CAMPO } from './classesDeCampo'
import { cn } from './estilo'
import { normalizarParaBusca } from './normalizar'

export type OpcaoDoCombo = { valor: string; rotulo: string; apoio?: string }

type ComboDeBuscaProps = {
  opcoes: readonly OpcaoDoCombo[]
  valor: string | null
  aoMudar: (valor: string | null) => void
  rotulo: string
  placeholder?: string
  textoSemResultado?: string
  aoDigitar?: (texto: string) => void
  carregando?: boolean
}

function filtrarSemAcento(opcao: OpcaoDoCombo, consulta: string): boolean {
  return normalizarParaBusca(opcao.rotulo).includes(normalizarParaBusca(consulta))
}

export function ComboDeBusca({
  opcoes,
  valor,
  aoMudar,
  rotulo,
  placeholder,
  textoSemResultado = 'Nenhum resultado.',
  aoDigitar,
  carregando = false,
}: ComboDeBuscaProps) {
  const escolhida = opcoes.find((opcao) => opcao.valor === valor) ?? null
  return (
    <Combobox.Root
      items={[...opcoes]}
      value={escolhida}
      onValueChange={(nova) => aoMudar((nova as OpcaoDoCombo | null)?.valor ?? null)}
      itemToStringLabel={(opcao: OpcaoDoCombo) => opcao.rotulo}
      itemToStringValue={(opcao: OpcaoDoCombo) => opcao.valor}
      isItemEqualToValue={(a: OpcaoDoCombo, b: OpcaoDoCombo) => a.valor === b.valor}
      filter={aoDigitar ? null : filtrarSemAcento}
      onInputValueChange={(texto) => aoDigitar?.(texto)}
    >
      <div className="relative">
        <Combobox.Input
          aria-label={rotulo}
          placeholder={placeholder}
          className={cn(CLASSES_DE_CAMPO, 'pr-10')}
        />
        <Combobox.Trigger
          aria-label={`Abrir ${rotulo}`}
          className="absolute top-1/2 right-2 flex h-8 w-8 -translate-y-1/2 items-center justify-center text-texto-suave"
        >
          <ChevronDown size={16} strokeWidth={1.75} aria-hidden="true" />
        </Combobox.Trigger>
      </div>
      <Combobox.Portal>
        <Combobox.Positioner sideOffset={4} className="z-50">
          <Combobox.Popup className="max-h-[min(var(--available-height),320px)] w-[var(--anchor-width)] overflow-y-auto rounded-campo border border-borda bg-superficie py-1 shadow-lg">
            {carregando && (
              <p role="status" className="px-3 py-2 text-tabela text-texto-suave">
                Carregando…
              </p>
            )}
            <Combobox.Empty className="px-3 py-2 text-tabela text-texto-suave empty:hidden">
              {textoSemResultado}
            </Combobox.Empty>
            <Combobox.List>
              {(opcao: OpcaoDoCombo) => (
                <Combobox.Item
                  key={opcao.valor}
                  value={opcao}
                  className="flex cursor-default items-center gap-2 px-3 py-2 text-corpo text-texto outline-none data-highlighted:bg-superficie-suave"
                >
                  <Combobox.ItemIndicator className="w-4 text-primaria">
                    <Check size={16} strokeWidth={1.75} aria-hidden="true" />
                  </Combobox.ItemIndicator>
                  <span className="flex flex-col">
                    <span>{opcao.rotulo}</span>
                    {opcao.apoio && (
                      <span className="text-pequeno text-texto-suave">{opcao.apoio}</span>
                    )}
                  </span>
                </Combobox.Item>
              )}
            </Combobox.List>
          </Combobox.Popup>
        </Combobox.Positioner>
      </Combobox.Portal>
    </Combobox.Root>
  )
}
