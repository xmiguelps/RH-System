import { Dialog } from '@base-ui/react/dialog'
import { X } from 'lucide-react'
import type { ReactNode } from 'react'
import { cn } from './estilo'

export type JanelaProps = {
  aberta: boolean
  aoFechar: () => void
  titulo: string
  descricao?: string
  posicao?: 'centro' | 'direita'
  tamanho?: 'media' | 'larga' | 'extra'
  largura?: 'estreita' | 'media' | 'larga'
  rodape?: ReactNode
  fecharAoClicarFora?: boolean
  children: ReactNode
}

const TAMANHOS = { media: 'w-[640px]', larga: 'w-[1040px]', extra: 'w-[1180px]' } as const
const LARGURAS = { estreita: 'w-[580px]', media: 'w-[600px]', larga: 'w-[864px]' } as const

export function Janela({
  aberta,
  aoFechar,
  titulo,
  descricao,
  posicao = 'centro',
  tamanho = 'media',
  largura = 'media',
  rodape,
  fecharAoClicarFora = true,
  children,
}: JanelaProps) {
  const lateral = posicao === 'direita'
  return (
    <Dialog.Root
      open={aberta}
      onOpenChange={(aberto) => {
        if (!aberto) aoFechar()
      }}
      disablePointerDismissal={!fecharAoClicarFora}
    >
      <Dialog.Portal>
        <Dialog.Backdrop data-testid="janela-fundo" className="fixed inset-0 z-40 bg-scrim" />
        <Dialog.Popup
          data-posicao={posicao}
          className={cn(
            'fixed z-50 flex max-w-[calc(100vw-48px)] flex-col overflow-hidden bg-pagina text-texto focus-visible:outline-none',
            lateral
              ? cn('top-0 right-0 h-screen', LARGURAS[largura])
              : cn(
                  'top-1/2 left-1/2 max-h-[calc(100vh-48px)] -translate-x-1/2 -translate-y-1/2 rounded-janela',
                  TAMANHOS[tamanho],
                ),
          )}
        >
          <div
            className={cn(
              'flex shrink-0 items-center justify-between gap-4 border-b border-divisor bg-superficie px-7',
              lateral ? 'h-20' : 'h-[76px]',
            )}
          >
            <Dialog.Title className="text-t2 font-bold text-texto">{titulo}</Dialog.Title>
            <Dialog.Close
              aria-label="Fechar"
              className="flex h-11 w-11 shrink-0 items-center justify-center rounded-campo border border-borda-campo bg-superficie text-texto focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primaria-media"
            >
              <X size={16} strokeWidth={1.75} aria-hidden="true" />
            </Dialog.Close>
          </div>
          <div className="min-h-0 flex-1 overflow-y-auto p-7">
            {descricao && (
              <Dialog.Description className="mb-4 text-corpo text-texto-suave">
                {descricao}
              </Dialog.Description>
            )}
            {children}
          </div>
          {rodape && (
            <div
              className={cn(
                'flex shrink-0 items-center justify-end gap-3 border-t border-divisor bg-superficie px-7',
                lateral ? 'h-[78px]' : 'py-4',
              )}
            >
              {rodape}
            </div>
          )}
        </Dialog.Popup>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
