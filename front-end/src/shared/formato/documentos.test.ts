import { describe, expect, it } from 'vitest'
import {
  cpfValido,
  formatarCpf,
  formatarTelefone,
  mascararCpf,
  mascararTelefone,
  somenteDigitos,
  telefoneValido,
} from './documentos'

describe('documentos', () => {
  it.each([
    ['846.392.710-69', '84639271069'],
    ['(11) 98765-4410', '11987654410'],
    ['', ''],
  ])('somenteDigitos(%s) === %s', (entrada, saida) => {
    expect(somenteDigitos(entrada)).toBe(saida)
  })

  it.each([
    ['84639271069', '846.392.710-69'],
    ['8463', '846.3'],
    ['846', '846'],
    ['846392', '846.392'],
    ['8463927', '846.392.7'],
    ['846392710', '846.392.710'],
    ['8463927106', '846.392.710-6'],
    ['', ''],
  ])('mascararCpf(%s) === %s', (entrada, saida) => {
    expect(mascararCpf(entrada)).toBe(saida)
  })

  it.each([
    ['11987654410', '(11) 98765-4410'],
    ['1187654410', '(11) 8765-4410'],
    ['119', '(11) 9'],
    ['1', '(1'],
    ['11', '(11'],
    ['', ''],
  ])('mascararTelefone(%s) === %s', (entrada, saida) => {
    expect(mascararTelefone(entrada)).toBe(saida)
  })

  it('mascararCpf e mascararTelefone ignoram o que nao e digito e cortam o excesso', () => {
    expect(mascararCpf('846.392.710-69999')).toBe('846.392.710-69')
    expect(mascararTelefone('(11) 98765-44109')).toBe('(11) 98765-4410')
  })

  it.each([
    ['84639271069', '846.392.710-69'],
    [null, '—'],
    [undefined, '—'],
    ['', '—'],
  ])('formatarCpf(%s) === %s', (entrada, saida) => {
    expect(formatarCpf(entrada)).toBe(saida)
  })

  it.each([
    ['11987654410', '(11) 98765-4410'],
    ['(11) 98765-4410', '(11) 98765-4410'],
    [null, '—'],
    ['', '—'],
  ])('formatarTelefone(%s) === %s', (entrada, saida) => {
    expect(formatarTelefone(entrada)).toBe(saida)
  })

  it('cpfValido aceita os cpfs do contrato e recusa repetido, curto e com digito errado', () => {
    for (const cpf of ['84639271069', '12348237130', '29384756083']) {
      expect(cpfValido(cpf), cpf).toBe(true)
    }
    for (const cpf of ['11111111111', '1234567890', '12345678901']) {
      expect(cpfValido(cpf), cpf).toBe(false)
    }
  })

  it.each([
    ['11987654410', true],
    ['1187654410', true],
    ['118765441', false],
    ['119876544100', false],
  ])('telefoneValido(%s) === %s', (entrada, saida) => {
    expect(telefoneValido(entrada)).toBe(saida)
  })
})
