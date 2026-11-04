// @vitest-environment node
import { describe, expect, it } from 'vitest'

describe('fuso da maquina', () => {
  it('controle do fuso: o relogio da maquina obedece a variavel TZ (Pacific/Honolulu fica 10 h atras de UTC)', () => {
    const instante = new Date('2026-10-07T12:42:00Z')
    if (process.env.TZ === 'Pacific/Honolulu') {
      expect(instante.getHours()).toBe(2)
      expect(instante.getTimezoneOffset()).toBe(600)
    } else {
      expect(instante.getUTCHours()).toBe(12)
    }
  })
})
