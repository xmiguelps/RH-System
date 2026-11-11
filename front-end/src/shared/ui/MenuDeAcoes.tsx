import { Menu } from '@base-ui/react/menu'
import { Ellipsis } from 'lucide-react'
import { cn } from './estilo'

export type AcaoDoMenu = {
  chave: string
  rotulo: string
  aoEscolher: () => void
  perigo?: boolean
  desabilitada?: boolean
  /** Explica por que o item está desabilitado (vira a descrição acessível do item). */
  dica?: string
}

export function MenuDeAcoes({ acoes, rotulo }: { acoes: readonly AcaoDoMenu[]; rotulo: string }) {
  return (
    <Menu.Root>
      <Menu.Trigger
        aria-label={rotulo}
        className="flex h-11 w-11 items-center justify-center rounded-campo border border-borda-campo bg-superficie text-texto focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primaria-media"
      >
        <Ellipsis size={16} strokeWidth={1.75} aria-hidden="true" />
      </Menu.Trigger>
      <Menu.Portal>
        <Menu.Positioner sideOffset={4} align="end" className="z-50">
          <Menu.Popup className="min-w-[180px] rounded-campo border border-borda bg-superficie py-1 shadow-lg">
            {acoes.map((acao) => (
              <Menu.Item
                key={acao.chave}
                disabled={acao.desabilitada}
                title={acao.dica}
                onClick={() => {
                  if (!acao.desabilitada) acao.aoEscolher()
                }}
                className={cn(
                  'flex cursor-default px-3 py-2 text-corpo outline-none data-disabled:opacity-50 data-highlighted:bg-superficie-suave',
                  acao.perigo ? 'text-erro-texto' : 'text-texto',
                )}
              >
                {acao.rotulo}
              </Menu.Item>
            ))}
          </Menu.Popup>
        </Menu.Positioner>
      </Menu.Portal>
    </Menu.Root>
  )
}
