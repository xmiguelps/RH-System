import { describe, expect, it } from 'vitest'
import { destinoDaPendencia, TITULO_DA_PENDENCIA } from './pendencias'

describe('destinoDaPendencia', () => {
  it.each([
    ['AJUSTE_DE_PONTO', '/rh/ponto-e-feriados?pedido=300'],
    ['FERIAS', '/rh/pedidos-de-ferias?pedido=300'],
    ['ATESTADO', '/rh/ponto-e-feriados/atestados'],
    ['ADMISSAO_COM_DOCUMENTOS_PENDENTES', '/rh/colaboradores/12?aba=documentos'],
    ['BENEFICIOS', '/rh/pedidos-de-beneficios'],
  ] as const)('%s leva a %s', (tipo, destino) => {
    expect(destinoDaPendencia({ tipo, referenciaId: 300, funcionarioId: 12 })).toBe(destino)
  })
})

describe('TITULO_DA_PENDENCIA', () => {
  it('os cinco tipos do contrato tem o titulo da tela 00', () => {
    expect(TITULO_DA_PENDENCIA).toEqual({
      AJUSTE_DE_PONTO: 'Pedidos de ajuste de ponto',
      FERIAS: 'Pedidos de férias',
      ATESTADO: 'Atestados para validar',
      ADMISSAO_COM_DOCUMENTOS_PENDENTES: 'Admissão com documentos pendentes',
      BENEFICIOS: 'Pedidos de benefícios',
    })
  })
})
