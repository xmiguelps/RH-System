import { describe, expect, it } from 'vitest'
import { formatarMinutosComoHoras, formatarNumero, formatarReais } from './numeros'

describe('numeros', () => {
  it.each([
    [380, '6h20'],
    [528, '8h48'],
    [0, '0h00'],
    [60, '1h00'],
    [5, '0h05'],
  ])('formatarMinutosComoHoras(%i) === %s', (minutos, saida) => {
    expect(formatarMinutosComoHoras(minutos)).toBe(saida)
  })

  it.each([
    [380, '+6h20'],
    [-2, '−0h02'],
    [0, '0h00'],
    [-125, '−2h05'],
  ])('formatarMinutosComoHoras(%i, { sinal: true }) === %s', (minutos, saida) => {
    expect(formatarMinutosComoHoras(minutos, { sinal: true })).toBe(saida)
  })

  it.each([
    [123456, 'R$ 1.234,56'],
    [0, 'R$ 0,00'],
    [5, 'R$ 0,05'],
    [100000000, 'R$ 1.000.000,00'],
  ])('formatarReais(%i) === %s', (centavos, saida) => {
    expect(formatarReais(centavos)).toBe(saida)
  })

  it.each([
    [1284, '1.284'],
    [7, '7'],
    [1000000, '1.000.000'],
  ])('formatarNumero(%i) === %s', (n, saida) => {
    expect(formatarNumero(n)).toBe(saida)
  })
})
