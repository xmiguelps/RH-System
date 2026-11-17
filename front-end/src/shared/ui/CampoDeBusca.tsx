import { Search } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Entrada } from './Entrada'

export function CampoDeBusca({
  valor,
  aoMudar,
  placeholder = 'Buscar',
  rotulo = 'Buscar',
  esperaMs = 300,
}: {
  valor: string
  aoMudar: (texto: string) => void
  placeholder?: string
  rotulo?: string
  esperaMs?: number
}) {
  const [texto, setTexto] = useState(valor)
  const [valorAnterior, setValorAnterior] = useState(valor)
  const espera = useRef<ReturnType<typeof setTimeout> | null>(null)

  // Quando o valor muda por fora (filtro limpo, voltar no navegador), o texto acompanha. A página guarda a busca aparada: se o valor é só o texto digitado sem os espaços das pontas, o campo fica como está (senão o espaço de "ana maria" some enquanto a pessoa escreve).
  if (valor !== valorAnterior) {
    setValorAnterior(valor)
    if (valor !== texto.trim()) setTexto(valor)
  }

  useEffect(
    () => () => {
      if (espera.current) clearTimeout(espera.current)
    },
    [],
  )

  const cancelar = () => {
    if (espera.current) clearTimeout(espera.current)
    espera.current = null
  }

  return (
    <Entrada
      type="search"
      icone={Search}
      aria-label={rotulo}
      placeholder={placeholder}
      value={texto}
      onChange={(evento) => {
        const novo = evento.target.value
        setTexto(novo)
        cancelar()
        espera.current = setTimeout(() => {
          espera.current = null
          aoMudar(novo)
        }, esperaMs)
      }}
      onKeyDown={(evento) => {
        if (evento.key !== 'Enter') return
        evento.preventDefault()
        cancelar()
        aoMudar(evento.currentTarget.value)
      }}
    />
  )
}
