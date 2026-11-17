import { describe, expect, it } from 'vitest'
import { barraDoPeriodo, mesesDasSemanas } from './calendarioDeFerias'

// A janela do exemplo do contrato: 13 segundas-feiras, de 05/10/2026 a 28/12/2026 (a janela vai até 03/01/2027).
const SEMANAS = [
  '2026-10-05',
  '2026-10-12',
  '2026-10-19',
  '2026-10-26',
  '2026-11-02',
  '2026-11-09',
  '2026-11-16',
  '2026-11-23',
  '2026-11-30',
  '2026-12-07',
  '2026-12-14',
  '2026-12-21',
  '2026-12-28',
]
const FIM = '2027-01-03'
const barra = (inicio: string, fim: string) => barraDoPeriodo(SEMANAS, FIM, inicio, fim)

describe('barraDoPeriodo', () => {
  it('periodo dentro da janela: comeca na semana do primeiro dia e termina na do ultimo, com "dd/mm a dd/mm"', () => {
    expect(barra('2026-11-03', '2026-11-17')).toEqual({
      coluna: 5,
      largura: 3,
      texto: '03/11 a 17/11',
    })
  })

  it('um dia so ocupa uma coluna', () => {
    expect(barra('2026-10-07', '2026-10-07')).toEqual({
      coluna: 1,
      largura: 1,
      texto: '07/10 a 07/10',
    })
  })

  it('comeca antes da janela: coluna 1 e "Até dd/mm" (o dia do fim)', () => {
    expect(barra('2026-09-28', '2026-10-17')).toEqual({ coluna: 1, largura: 2, texto: 'Até 17/10' })
  })

  it('passa do fim da janela: vai ate a ultima coluna e "A partir de dd/mm"', () => {
    expect(barra('2026-12-23', '2027-01-15')).toEqual({
      coluna: 12,
      largura: 2,
      texto: 'A partir de 23/12',
    })
  })

  it('cobre a janela toda: as 13 colunas e um texto que nao inventa data', () => {
    expect(barra('2026-09-01', '2027-02-01')).toEqual({
      coluna: 1,
      largura: 13,
      texto: 'Durante todo o período',
    })
  })

  it('termina no domingo e comeca na segunda: nao vaza para a coluna ao lado', () => {
    expect(barra('2026-11-09', '2026-11-15')).toEqual({
      coluna: 6,
      largura: 1,
      texto: '09/11 a 15/11',
    })
  })

  it('fora da janela (acabou antes ou comeca depois) nao tem barra', () => {
    expect(barra('2026-09-01', '2026-10-04')).toBeNull()
    expect(barra('2027-01-04', '2027-01-20')).toBeNull()
  })

  it('o ultimo dia da janela ainda conta (domingo 03/01 cai na ultima coluna)', () => {
    expect(barra('2027-01-03', '2027-01-03')).toEqual({
      coluna: 13,
      largura: 1,
      texto: '03/01 a 03/01',
    })
  })
})

describe('mesesDasSemanas', () => {
  it('agrupa as semanas pelo mes da segunda-feira, na virada de mes', () => {
    expect(mesesDasSemanas(SEMANAS)).toEqual([
      { mes: 'Outubro', colunas: 4 },
      { mes: 'Novembro', colunas: 5 },
      { mes: 'Dezembro', colunas: 4 },
    ])
  })
})
