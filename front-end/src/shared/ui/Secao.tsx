import { useId, type ReactNode } from 'react'

export function Secao({
  titulo,
  acao,
  children,
}: {
  titulo: string
  acao?: ReactNode
  children: ReactNode
}) {
  const id = useId()
  return (
    <section aria-labelledby={id} className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-4">
        <h2 id={id} className="text-t2 font-bold text-texto">
          {titulo}
        </h2>
        {acao}
      </div>
      {children}
    </section>
  )
}
