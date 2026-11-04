import { Progress } from '@base-ui/react/progress'

export function Progresso({
  valor,
  maximo = 100,
  rotulo,
}: {
  valor: number
  maximo?: number
  rotulo: string
}) {
  return (
    <Progress.Root value={valor} max={maximo} className="flex flex-col gap-1">
      <Progress.Label className="sr-only">{rotulo}</Progress.Label>
      <Progress.Track className="h-2 overflow-hidden rounded-full bg-borda">
        <Progress.Indicator className="h-full rounded-full bg-primaria-media" />
      </Progress.Track>
    </Progress.Root>
  )
}
