import { Toast } from '@base-ui/react/toast'
import { X } from 'lucide-react'
import type { ReactNode } from 'react'
import { cn } from './estilo'

const TONS: Record<string, string> = {
  sucesso: 'border-l-4 border-l-primaria',
  erro: 'border-l-4 border-l-erro-texto',
  informacao: 'border-l-4 border-l-primaria-media',
}

function ListaDeNotificacoes() {
  const { toasts } = Toast.useToastManager()
  return toasts.map((aviso) => (
    <Toast.Root
      key={aviso.id}
      toast={aviso}
      data-tom={aviso.type ?? 'informacao'}
      className={cn(
        'flex w-[360px] items-start gap-3 rounded-cartao border border-borda bg-superficie px-4 py-3 shadow-lg',
        TONS[aviso.type ?? 'informacao'],
      )}
    >
      <Toast.Content className="flex flex-1 items-start gap-3">
        <Toast.Title className="flex-1 text-corpo text-texto" />
        <Toast.Close
          aria-label="Fechar aviso"
          className="flex h-6 w-6 items-center justify-center text-texto-suave"
        >
          <X size={16} strokeWidth={1.75} aria-hidden="true" />
        </Toast.Close>
      </Toast.Content>
    </Toast.Root>
  ))
}

export function NotificacoesProvider({ children }: { children: ReactNode }) {
  return (
    <Toast.Provider timeout={5000}>
      {children}
      <Toast.Portal>
        <Toast.Viewport
          aria-label="Notificações"
          className="fixed right-6 bottom-6 z-[60] flex flex-col gap-3"
        >
          <ListaDeNotificacoes />
        </Toast.Viewport>
      </Toast.Portal>
    </Toast.Provider>
  )
}
