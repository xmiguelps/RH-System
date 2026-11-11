import { describe, expect, it } from 'vitest'
import { esperarRequisicaoConforme } from './contrato'

describe('esperarRequisicaoConforme', () => {
  it('aceita o corpo de entrar com as chaves do contrato', () => {
    expect(() =>
      esperarRequisicaoConforme('entrar', { corpo: { email: 'a@b.c', senha: 'x' } }),
    ).not.toThrow()
  })

  it('recusa chave extra ao lado das certas no corpo', () => {
    expect(() =>
      esperarRequisicaoConforme('entrar', { corpo: { email: 'a@b.c', senha: 'x', senhaa: 'y' } }),
    ).toThrow(/senhaa/)
  })

  it('recusa corpo sem chave obrigatoria', () => {
    expect(() => esperarRequisicaoConforme('entrar', { corpo: { email: 'a@b.c' } })).toThrow(
      /senha/,
    )
  })

  it('confere os parametros de consulta de uma lista paginada', () => {
    expect(() =>
      esperarRequisicaoConforme('listarFuncionarios', {
        consulta: new URLSearchParams({ pagina: '1', tamanhoPagina: '20', busca: 'ana' }),
      }),
    ).not.toThrow()
    expect(() =>
      esperarRequisicaoConforme('listarFuncionarios', {
        consulta: new URLSearchParams({ pagina: '1', tamanhoPagin: '20' }),
      }),
    ).toThrow(/tamanhoPagin/)
  })

  it('operacao que nao existe no contrato falha com mensagem clara', () => {
    expect(() => esperarRequisicaoConforme('operacaoInventada', {})).toThrow(/operacaoInventada/)
  })
})
