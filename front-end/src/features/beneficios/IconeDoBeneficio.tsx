import { Baby, BookOpen, Bus, Gift, HeartPulse, ShieldCheck, Smile, Utensils } from 'lucide-react'

const CLASSES = 'h-6 w-6 text-primaria-media'

/** O ícone não vem da API: escolhido pelo nome do catálogo (`ELENCO.md`), com um genérico para o resto. */
export function IconeDoBeneficio({ nome }: { nome: string }) {
  const n = nome.toLowerCase()
  if (n.includes('refeição'))
    return <Utensils aria-hidden="true" className={CLASSES} strokeWidth={1.75} />
  if (n.includes('saúde'))
    return <HeartPulse aria-hidden="true" className={CLASSES} strokeWidth={1.75} />
  if (n.includes('odontológico'))
    return <Smile aria-hidden="true" className={CLASSES} strokeWidth={1.75} />
  if (n.includes('transporte'))
    return <Bus aria-hidden="true" className={CLASSES} strokeWidth={1.75} />
  if (n.includes('seguro'))
    return <ShieldCheck aria-hidden="true" className={CLASSES} strokeWidth={1.75} />
  if (n.includes('creche'))
    return <Baby aria-hidden="true" className={CLASSES} strokeWidth={1.75} />
  if (n.includes('cultura'))
    return <BookOpen aria-hidden="true" className={CLASSES} strokeWidth={1.75} />
  return <Gift aria-hidden="true" className={CLASSES} strokeWidth={1.75} />
}
