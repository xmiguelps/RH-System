import type { ReactNode } from 'react'

export function CabecalhoDePagina({
  titulo,
  subtitulo,
  acoes,
}: {
  titulo: string
  subtitulo?: ReactNode
  acoes?: ReactNode
}) {
  return (
    <header className="mb-6 flex items-start justify-between gap-6">
      <div>
        <h1 className="text-t1 font-bold text-texto">{titulo}</h1>
        {subtitulo && <div className="mt-1 text-corpo text-texto-suave">{subtitulo}</div>}
      </div>
      {acoes && <div className="flex shrink-0 items-center gap-3">{acoes}</div>}
    </header>
  )
}
