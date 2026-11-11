import { describe, expect, it } from 'vitest'
import {
  FUSO_DO_PORTAL,
  dataDoInstante,
  dataPorExtenso,
  formatarData,
  formatarDataHora,
  formatarDiaMes,
  formatarDiaMesDaData,
  formatarDiaMesHora,
  formatarHora,
  formatarInstanteRelativo,
  formatarMesAno,
  hojeEmSaoPaulo,
  instanteUtcDaHoraLocal,
  nomeDoMes,
  saudacao,
} from './datas'

const AGORA = new Date('2026-10-07T12:42:00Z')

describe('datas', () => {
  it('o fuso do portal e o de Sao Paulo', () => {
    expect(FUSO_DO_PORTAL).toBe('America/Sao_Paulo')
  })

  it('formatarData nao muda o dia (Review Focus 5)', () => {
    expect(formatarData('2026-10-07')).toBe('07/10/2026')
    expect(formatarData('2026-01-01')).toBe('01/01/2026')
    expect(formatarData(null)).toBe('—')
    expect(formatarData(undefined)).toBe('—')
    expect(formatarData('')).toBe('—')
    expect(formatarData('ontem')).toBe('ontem')
  })

  it('formatarDataHora converte UTC para Sao Paulo, inclusive perto da meia-noite', () => {
    expect(formatarDataHora('2026-10-08T02:30:00Z')).toBe('07/10/2026 23:30')
    expect(formatarDataHora('2026-10-07T12:42:00Z')).toBe('07/10/2026 09:42')
    expect(formatarDataHora(null)).toBe('—')
  })

  it('formatarHora e formatarDiaMes usam Sao Paulo', () => {
    expect(formatarHora('2026-10-07T12:42:00Z')).toBe('09:42')
    expect(formatarHora('2026-10-08T02:30:00Z')).toBe('23:30')
    expect(formatarDiaMes('2026-10-08T02:30:00Z')).toBe('07/10')
    expect(formatarHora(null)).toBe('—')
    expect(formatarDiaMes(undefined)).toBe('—')
  })

  it('formatarInstanteRelativo escreve Hoje, Ontem e a data', () => {
    expect(formatarInstanteRelativo('2026-10-07T12:38:00Z', AGORA)).toBe('Hoje, 09:38')
    expect(formatarInstanteRelativo('2026-10-06T20:55:00Z', AGORA)).toBe('Ontem, 17:55')
    expect(formatarInstanteRelativo('2026-10-05T21:30:00Z', AGORA)).toBe('05/10, 18:30')
    expect(formatarInstanteRelativo('2025-10-05T21:30:00Z', AGORA)).toBe('05/10/2025, 18:30')
    expect(formatarInstanteRelativo('2026-10-08T02:30:00Z', AGORA)).toBe('Hoje, 23:30')
  })

  it('hojeEmSaoPaulo devolve o dia de calendario de Sao Paulo', () => {
    expect(hojeEmSaoPaulo(AGORA)).toBe('2026-10-07')
    expect(hojeEmSaoPaulo(new Date('2026-10-08T02:30:00Z'))).toBe('2026-10-07')
  })

  it('dataPorExtenso e saudacao usam a hora de Sao Paulo', () => {
    expect(dataPorExtenso(AGORA)).toBe('Quarta-feira, 7 de outubro')
    expect(saudacao(AGORA)).toBe('Bom dia')
    expect(saudacao(new Date('2026-10-07T20:00:00Z'))).toBe('Boa tarde')
    expect(saudacao(new Date('2026-10-08T02:00:00Z'))).toBe('Boa noite')
    expect(saudacao(new Date('2026-10-07T08:00:00Z'))).toBe('Bom dia')
    expect(saudacao(new Date('2026-10-07T07:59:00Z'))).toBe('Boa noite')
    expect(saudacao(new Date('2026-10-07T15:00:00Z'))).toBe('Boa tarde')
  })
})

describe('datas do ponto (fatia 3)', () => {
  it('formatarDiaMesDaData nao passa por Date', () => {
    expect(formatarDiaMesDaData('2026-10-05')).toBe('05/10')
    expect(formatarDiaMesDaData(null)).toBe('—')
  })

  it('formatarDiaMesHora e dataDoInstante usam Brasilia (fim de dia cai no dia anterior)', () => {
    expect(formatarDiaMesHora('2026-10-02T21:05:00Z')).toBe('02/10, 18:05')
    expect(dataDoInstante('2026-10-08T02:59:59Z')).toBe('2026-10-07')
    expect(formatarHora('2026-10-08T01:30:00Z')).toBe('22:30')
    expect(dataDoInstante('2026-10-08T01:30:00Z')).toBe('2026-10-07')
  })

  it('instanteUtcDaHoraLocal converte de -03:00 para UTC, com o dia seguinte', () => {
    expect(instanteUtcDaHoraLocal('2026-10-05', '13:05')).toBe('2026-10-05T16:05:00Z')
    expect(instanteUtcDaHoraLocal('2026-10-05', '22:30')).toBe('2026-10-06T01:30:00Z')
    expect(instanteUtcDaHoraLocal('2026-10-05', '01:10', true)).toBe('2026-10-06T04:10:00Z')
    expect(instanteUtcDaHoraLocal('2026-10-31', '00:30', true)).toBe('2026-11-01T03:30:00Z')
  })

  it('formatarMesAno e nomeDoMes', () => {
    expect(formatarMesAno(2026, 10)).toBe('Outubro de 2026')
    expect(nomeDoMes(3)).toBe('março')
  })
})
