import { readFileSync } from 'node:fs'
import { render } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Bolinha } from '../shared/ui/Bolinha'

// Decisão do dono (2026-11-18, `D-V-contraste`): as cores abaixo do mínimo da WCAG 2.1 AA foram escurecidas.
// Texto precisa de 4,5:1; a borda de um campo (componente de interface) precisa de 3:1.
const css = readFileSync('src/index.css', 'utf8')
const cores = new Map<string, string>()
for (const achado of css.matchAll(/--color-([a-z-]+):\s*(#[0-9a-f]{6});/g)) {
  cores.set(achado[1], achado[2])
}

function cor(nome: string): string {
  const valor = cores.get(nome)
  if (!valor) throw new Error(`token --color-${nome} não existe em index.css`)
  return valor
}

function luminancia(hex: string): number {
  const [r, g, b] = [1, 3, 5].map((inicio) => {
    const canal = parseInt(hex.slice(inicio, inicio + 2), 16) / 255
    return canal <= 0.03928 ? canal / 12.92 : ((canal + 0.055) / 1.055) ** 2.4
  })
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

function razao(a: string, b: string): number {
  const [maior, menor] = [luminancia(a), luminancia(b)].sort((x, y) => y - x)
  return (maior + 0.05) / (menor + 0.05)
}

describe('contraste das cores (WCAG 2.1 AA)', () => {
  const textosSobreFundos: [string, string][] = [
    ['primaria-media', 'pagina'],
    ['primaria-media', 'superficie'],
    ['primaria-media', 'superficie-suave'],
    ['primaria-media', 'superficie-destaque'],
    ['primaria-media', 'sucesso-fundo'],
    ['primaria-media', 'neutro-fundo'],
    ['menu-texto', 'primaria-media'],
  ]

  it.each(textosSobreFundos)('o texto %s sobre %s tem pelo menos 4,5:1', (texto, fundo) => {
    expect(razao(cor(texto), cor(fundo))).toBeGreaterThanOrEqual(4.5)
  })

  it.each(['superficie', 'pagina'])('a borda dos campos tem pelo menos 3:1 sobre %s', (fundo) => {
    expect(razao(cor('borda-campo'), cor(fundo))).toBeGreaterThanOrEqual(3)
  })

  it('o número da Bolinha do menu tem pelo menos 4,5:1 sobre o fundo dela', () => {
    const { container } = render(<Bolinha valor={4} />)
    const classes = (container.firstElementChild?.className ?? '').split(/\s+/)
    const fundo = classes.find((c) => c.startsWith('bg-') && cores.has(c.slice(3)))
    const texto = classes.find((c) => c.startsWith('text-') && cores.has(c.slice(5)))
    expect(fundo).toBeDefined()
    expect(texto).toBeDefined()
    expect(razao(cor(texto!.slice(5)), cor(fundo!.slice(3)))).toBeGreaterThanOrEqual(4.5)
  })
})
