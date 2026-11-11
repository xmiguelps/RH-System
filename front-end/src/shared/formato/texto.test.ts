import { describe, expect, it } from 'vitest'
import { iniciais, plural, primeiroNome } from './texto'

describe('texto', () => {
  it.each([
    ['Ana Souza', 'AS'],
    ['João Pedro Alves', 'JA'],
    ['Madonna', 'M'],
    ['  ', ''],
    ['bruno lima', 'BL'],
    ['  Carla   Rocha  ', 'CR'],
  ])('iniciais(%s) === %s', (nome, saida) => {
    expect(iniciais(nome)).toBe(saida)
  })

  it.each([
    ['Ana Souza', 'Ana'],
    ['  Bruno Lima', 'Bruno'],
    ['Madonna', 'Madonna'],
    ['', ''],
  ])('primeiroNome(%s) === %s', (nome, saida) => {
    expect(primeiroNome(nome)).toBe(saida)
  })

  it.each([
    [1, 'pessoa'],
    [0, 'pessoas'],
    [2, 'pessoas'],
  ])('plural(%i) escolhe a forma certa', (quantidade, saida) => {
    expect(plural(quantidade, 'pessoa', 'pessoas')).toBe(saida)
  })
})
