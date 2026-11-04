import { Tabs } from '@base-ui/react/tabs'
import type { ReactNode } from 'react'
import { cn } from './estilo'

export type ItemDeAba<V extends string = string> = {
  valor: V
  rotulo: string
  contagem?: number | null
  desabilitada?: boolean
}

export function Abas<V extends string>({
  itens,
  valor,
  aoMudar,
  rotulo,
  variante = 'pilula',
  renderizarPainel,
}: {
  itens: readonly ItemDeAba<V>[]
  valor: V
  aoMudar: (valor: V) => void
  rotulo: string
  variante?: 'pilula' | 'sublinhada'
  renderizarPainel?: (valor: V) => ReactNode
}) {
  const pilula = variante === 'pilula'
  return (
    <Tabs.Root value={valor} onValueChange={(novo) => aoMudar(novo as V)}>
      <Tabs.List
        activateOnFocus
        aria-label={rotulo}
        data-variante={variante}
        className={cn(
          'flex',
          pilula
            ? 'inline-flex gap-1 rounded-[16px] bg-superficie-destaque p-1'
            : 'gap-6 border-b border-divisor',
        )}
      >
        {itens.map((item) => (
          <Tabs.Tab
            key={item.valor}
            value={item.valor}
            disabled={item.desabilitada}
            className={cn(
              'text-corpo whitespace-nowrap focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primaria-media data-disabled:opacity-50',
              pilula
                ? 'h-11 rounded-[12px] px-4 font-medium text-texto-suave data-active:bg-superficie data-active:font-semibold data-active:text-texto'
                : '-mb-px border-b-[3px] border-transparent pb-3 text-texto-suave data-active:border-primaria data-active:font-semibold data-active:text-primaria',
            )}
          >
            {item.rotulo}
            {item.contagem !== undefined && item.contagem !== null && ` ${item.contagem}`}
          </Tabs.Tab>
        ))}
      </Tabs.List>
      {renderizarPainel && (
        <Tabs.Panel value={valor} className="pt-6 focus-visible:outline-none">
          {renderizarPainel(valor)}
        </Tabs.Panel>
      )}
    </Tabs.Root>
  )
}
