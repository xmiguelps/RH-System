export function Bolinha({ valor }: { valor: number | null | undefined }) {
  if (valor === null || valor === undefined || valor <= 0) return null
  return (
    <span className="inline-flex h-5 min-w-[25px] items-center justify-center rounded-full bg-primaria-clara px-1.5 text-pequeno font-bold text-primaria">
      {valor > 99 ? '99+' : valor}
    </span>
  )
}
