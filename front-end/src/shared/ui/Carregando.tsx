import { LoaderCircle } from 'lucide-react'

export function Carregando({ texto = 'Carregando…' }: { texto?: string }) {
  return (
    <p role="status" className="flex items-center gap-2 py-6 text-corpo text-texto-suave">
      <LoaderCircle size={16} strokeWidth={1.75} aria-hidden="true" className="animate-spin" />
      {texto}
    </p>
  )
}
