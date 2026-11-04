import type { Esquema } from '../../shared/api/tipos'
import { formatarDiaMesDaData } from '../../shared/formato/datas'
import { formatarMinutosComoHoras } from '../../shared/formato/numeros'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { cn } from '../../shared/ui/estilo'
import { Selo } from '../../shared/ui/Selo'
import { etiquetaDoDia, rotuloDoDiaDaSemana, textoDoPeriodo } from './rotulosDoPonto'

type Periodo = Esquema<'PontoPeriodo'>

const VAZIO = '—'

export function ChipsDosPeriodos({
  periodos,
  ehHoje,
  semNada = 'Nenhum registro',
}: {
  periodos: readonly Periodo[]
  ehHoje: boolean
  semNada?: string
}) {
  if (periodos.length === 0) return <span className="text-tabela text-texto-suave">{semNada}</span>
  return (
    <span className="flex flex-wrap gap-1.5">
      {periodos.map((periodo, i) => {
        const { texto, tom } = textoDoPeriodo(periodo, ehHoje)
        return (
          <Selo key={periodo.entrada?.id ?? periodo.saida?.id ?? i} tom={tom}>
            {texto}
          </Selo>
        )
      })}
    </span>
  )
}

export function CelulaDoDia({
  dia,
}: {
  dia: Pick<Esquema<'PontoEspelhoDia'>, 'data' | 'diaDaSemana'>
}) {
  return (
    <span className="flex flex-col">
      <span className="font-semibold text-texto">{formatarDiaMesDaData(dia.data)}</span>
      <span className="text-pequeno text-texto-suave">{rotuloDoDiaDaSemana(dia.diaDaSemana)}</span>
    </span>
  )
}

/** Saldo com sinal (U+2212 no negativo) e cor; `null` é travessão. */
export function Saldo({ minutos, className }: { minutos: number | null; className?: string }) {
  if (minutos === null) return <span className={cn('text-texto-suave', className)}>{VAZIO}</span>
  return (
    <span
      data-tom={minutos < 0 ? 'alerta' : minutos > 0 ? 'sucesso' : 'neutro'}
      className={cn(
        'tabular-nums',
        minutos < 0 && 'text-alerta-texto',
        minutos > 0 && 'text-primaria-media',
        className,
      )}
    >
      {formatarMinutosComoHoras(minutos, { sinal: true })}
    </span>
  )
}

export function Horas({ minutos }: { minutos: number | null }) {
  return minutos === null ? (
    <span className="text-texto-suave">{VAZIO}</span>
  ) : (
    <span className="font-bold tabular-nums">{formatarMinutosComoHoras(minutos)}</span>
  )
}

export function EtiquetaDoDia({
  dia,
  onde = 'pessoa',
}: {
  dia: Pick<Esquema<'PontoEspelhoDia'>, 'situacao' | 'atrasoMinutos' | 'nomeFeriado'>
  onde?: 'pessoa' | 'rh'
}) {
  const { texto, tom } = etiquetaDoDia(dia, onde)
  return <Selo tom={tom}>{texto}</Selo>
}

export type NumeroDoPonto = {
  rotulo: string
  valor: string
  tom?: 'neutro' | 'alerta' | 'sucesso'
  apoio?: string
}

/** A fileira de números do topo (vêm prontos da API; nada é somado aqui). */
export function NumerosDoPonto({
  rotulo,
  itens,
  carregando = false,
}: {
  rotulo: string
  itens: readonly NumeroDoPonto[]
  carregando?: boolean
}) {
  return (
    <section
      aria-label={rotulo}
      className={cn(
        'grid gap-4',
        itens.length === 4 ? 'grid-cols-4' : itens.length === 3 ? 'grid-cols-3' : 'grid-cols-2',
      )}
    >
      {itens.map((item) => (
        <div
          key={item.rotulo}
          className="flex flex-col gap-1 rounded-cartao border border-borda bg-superficie p-5"
        >
          <span className="text-tabela text-texto-suave">{item.rotulo}</span>
          {carregando ? (
            <Esqueleto className="h-8 w-20" />
          ) : (
            <span
              data-tom={item.tom ?? 'neutro'}
              className={cn(
                'text-numero font-bold tabular-nums text-texto',
                item.tom === 'alerta' && 'text-alerta-texto',
                item.tom === 'sucesso' && 'text-primaria-media',
              )}
            >
              {item.valor}
            </span>
          )}
          {!carregando && item.apoio && (
            <span className="text-pequeno text-texto-suave">{item.apoio}</span>
          )}
        </div>
      ))}
    </section>
  )
}
