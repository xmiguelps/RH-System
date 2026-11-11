import { Switch } from '@base-ui/react/switch'

export function Interruptor({
  ligado,
  aoMudar,
  rotulo,
  desabilitado = false,
}: {
  ligado: boolean
  aoMudar: (ligado: boolean) => void
  rotulo: string
  desabilitado?: boolean
}) {
  return (
    <label className="inline-flex cursor-pointer items-center gap-3 text-corpo text-texto">
      <Switch.Root
        checked={ligado}
        onCheckedChange={(valor) => aoMudar(valor)}
        disabled={desabilitado}
        className="relative inline-flex h-6 w-11 shrink-0 items-center rounded-full bg-borda-campo transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primaria-media data-checked:bg-primaria data-disabled:opacity-50"
      >
        <Switch.Thumb className="block h-5 w-5 translate-x-0.5 rounded-full bg-superficie transition-transform data-checked:translate-x-[22px]" />
      </Switch.Root>
      {rotulo}
    </label>
  )
}
