import { describe, expect, it } from 'vitest'
import { formatarEndereco } from './endereco'
import {
  itemDeAdvertencias,
  itemDeBancoDeHoras,
  itemDeFerias,
  itemDeProximoPagamento,
  itemDeTreinamentosPendentes,
} from './indicadores'

const NULO = {
  cep: null,
  logradouro: null,
  numero: null,
  complemento: null,
  bairro: null,
  cidade: null,
  uf: null,
}

describe('indicadores', () => {
  it('banco de horas com sinal e tom', () => {
    expect(itemDeBancoDeHoras(380)).toMatchObject({ valor: '+6h20', tom: 'neutro' })
    expect(itemDeBancoDeHoras(-225)).toMatchObject({ valor: '−3h45', tom: 'alerta' })
    expect(itemDeBancoDeHoras(0)).toMatchObject({ valor: '0h00', tom: 'neutro' })
    expect(itemDeBancoDeHoras(null).valor).toBe('—')
  })
  it('ferias, advertencias, pagamento e treinamentos', () => {
    expect(itemDeFerias(18).valor).toBe('18 dias')
    expect(itemDeFerias(1).valor).toBe('1 dia')
    expect(itemDeFerias(null).valor).toBe('—')
    expect(itemDeAdvertencias(0)).toMatchObject({ valor: '0', tom: 'neutro' })
    expect(itemDeAdvertencias(2)).toMatchObject({ valor: '2', tom: 'erro' })
    expect(itemDeProximoPagamento('2026-11-06').valor).toBe('6 de novembro')
    expect(itemDeProximoPagamento(null).valor).toBe('—')
    expect(itemDeTreinamentosPendentes(3)).toMatchObject({ valor: '3', tom: 'alerta' })
  })
})

describe('formatarEndereco', () => {
  it('monta o endereco omitindo o que e null', () => {
    expect(
      formatarEndereco({
        cep: '01310100',
        logradouro: 'Avenida Paulista',
        numero: '1578',
        complemento: 'apto 42',
        bairro: 'Bela Vista',
        cidade: 'São Paulo',
        uf: 'SP',
      }),
    ).toBe('Avenida Paulista, 1578, apto 42, Bela Vista, São Paulo/SP, CEP 01310-100')
    expect(formatarEndereco({ ...NULO, cidade: 'São Paulo' })).toBe('São Paulo')
    expect(formatarEndereco(NULO)).toBe('—')
  })
})
