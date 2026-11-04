import { Botao } from '../../shared/ui/Botao'
import { cn } from '../../shared/ui/estilo'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import type { ItemDeIndicador } from './indicadores'

const TONS = { neutro: 'text-texto', alerta: 'text-alerta-texto', erro: 'text-erro-texto' } as const

/** Os quatro números da ficha; carregam e falham sozinhos, sem derrubar o resto. */
export function QuadroDeIndicadores({
  rotulo,
  itens,
  carregando = false,
  erro,
  aoTentarDeNovo,
}: {
  rotulo: string
  itens: readonly ItemDeIndicador[]
  carregando?: boolean
  erro?: unknown
  aoTentarDeNovo?: () => void
}) {
  const temErro = erro !== undefined && erro !== null
  return (
    <section aria-label={rotulo} className="rounded-cartao bg-superficie-suave p-[21px]">
      <div className="grid grid-cols-2 gap-4">
        {itens.map((item) => (
          <div key={item.rotulo} className="flex flex-col gap-1">
            <span className="text-tabela text-texto-suave">{item.rotulo}</span>
            {carregando ? (
              <Esqueleto className="h-8 w-24" />
            ) : (
              <span
                data-tom={temErro ? 'neutro' : item.tom}
                className={cn(
                  'text-numero font-bold tabular-nums',
                  TONS[temErro ? 'neutro' : item.tom],
                )}
              >
                {temErro ? '—' : item.valor}
              </span>
            )}
          </div>
        ))}
      </div>
      {temErro && aoTentarDeNovo && (
        <div className="mt-4">
          <Botao variante="secundario" tamanho="pequeno" onClick={aoTentarDeNovo}>
            Tentar de novo
          </Botao>
        </div>
      )}
    </section>
  )
}
