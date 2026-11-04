import { describe, expect, it } from 'vitest'
import { formatarDiaEMes } from './datas'
import { formatarCep, mascararCep } from './documentos'

describe('formatadores da ficha', () => {
  it('formatarDiaEMes nao usa Date: dia 18 de junho, 6 de novembro, vazio e traco', () => {
    expect(formatarDiaEMes('1990-06-18')).toBe('18 de junho')
    expect(formatarDiaEMes('2026-11-06')).toBe('6 de novembro')
    expect(formatarDiaEMes('2026-01-01')).toBe('1 de janeiro')
    expect(formatarDiaEMes(null)).toBe('—')
  })

  it('mascararCep e formatarCep', () => {
    expect(mascararCep('01310100')).toBe('01310-100')
    expect(mascararCep('0131')).toBe('0131')
    expect(mascararCep('013101009999')).toBe('01310-100')
    expect(formatarCep('01310100')).toBe('01310-100')
    expect(formatarCep(null)).toBe('—')
  })
})
