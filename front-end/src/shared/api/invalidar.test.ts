import { QueryClient } from '@tanstack/react-query'
import { describe, expect, it } from 'vitest'
import { invalidarRota } from './invalidar'

describe('invalidarRota', () => {
  it('invalidarRota invalida so as consultas cujo caminho comeca com o prefixo', async () => {
    const qc = new QueryClient()
    qc.setQueryData(['get', '/api/rh/funcionarios', { params: { query: { pagina: 1 } } }], { a: 1 })
    qc.setQueryData(['get', '/api/rh/funcionarios/{funcionarioId}', { params: {} }], { b: 1 })
    qc.setQueryData(['get', '/api/rh/cargos', {}], { c: 1 })
    await invalidarRota(qc, '/api/rh/funcionarios')
    const estado = (chave: unknown[]) => qc.getQueryState(chave)?.isInvalidated
    expect(estado(['get', '/api/rh/funcionarios', { params: { query: { pagina: 1 } } }])).toBe(true)
    expect(estado(['get', '/api/rh/funcionarios/{funcionarioId}', { params: {} }])).toBe(true)
    expect(estado(['get', '/api/rh/cargos', {}])).toBe(false)
  })
})
