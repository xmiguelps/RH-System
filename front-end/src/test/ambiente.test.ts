import { describe, expect, it } from 'vitest'

describe('ambiente de teste', () => {
  it('jsdom ganhou os polyfills que o Base UI e o userEvent usam', () => {
    const elemento = document.createElement('div')
    expect(elemento.hasPointerCapture(1)).toBe(false)
    expect(() => elemento.scrollIntoView()).not.toThrow()
    expect(() => elemento.setPointerCapture(1)).not.toThrow()
    expect(() => elemento.releasePointerCapture(1)).not.toThrow()
  })

  it('requisicao sem handler falha (onUnhandledFrame: error)', async () => {
    await expect(fetch('https://api.exemplo.test/api/rota-que-nao-existe')).rejects.toThrow()
  })
})
