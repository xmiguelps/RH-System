import { Check } from 'lucide-react'
import { cn } from './estilo'

export function Etapas({
  etapas,
  atual,
}: {
  etapas: readonly { chave: string; rotulo: string }[]
  atual: string
}) {
  const indiceAtual = etapas.findIndex((etapa) => etapa.chave === atual)
  return (
    <ol aria-label="Etapas" className="flex items-center gap-3">
      {etapas.map((etapa, indice) => {
        const concluida = indice < indiceAtual
        const ehAtual = indice === indiceAtual
        return (
          <li
            key={etapa.chave}
            aria-current={ehAtual ? 'step' : undefined}
            className="flex items-center gap-3"
          >
            {indice > 0 && <span aria-hidden="true" className="h-0.5 w-10 bg-borda-campo" />}
            <span
              aria-hidden="true"
              className={cn(
                'flex h-[22px] w-[22px] items-center justify-center rounded-full',
                concluida && 'bg-primaria text-pagina',
                ehAtual && 'border-2 border-primaria',
                !concluida && !ehAtual && 'border-2 border-tracejado',
              )}
            >
              {concluida && <Check size={14} strokeWidth={2.5} />}
              {ehAtual && <span className="h-2 w-2 rounded-full bg-primaria" />}
            </span>
            <span
              className={cn(
                'text-tabela',
                ehAtual ? 'font-semibold text-primaria' : 'text-texto-suave',
              )}
            >
              {etapa.rotulo}
              {concluida && <span className="sr-only"> (concluída)</span>}
            </span>
          </li>
        )
      })}
    </ol>
  )
}
