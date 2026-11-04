import { describe, expect, it } from 'vitest'
import { exemplos } from '../mocks/gerado/handlers'
import { exemploDoContrato } from './exemploDoContrato'

type Duplicado = {
  codigo: string
  cadastroExistente: { funcionarioId: number; nome: string; ativo: boolean }
}

describe('exemploDoContrato', () => {
  it('sem status devolve o primeiro exemplo 2xx, o mesmo que o handler gerado devolve', () => {
    expect(exemploDoContrato('entrar')).toEqual(exemplos.entrar.corpo)
    expect(exemploDoContrato('listarFuncionarios')).toEqual(exemplos.listarFuncionarios.corpo)
  })

  it('devolve o exemplo nomeado de um erro (o 409 cpfInativo de criarFuncionario)', () => {
    const problema = exemploDoContrato<Duplicado>('criarFuncionario', 409, 'cpfInativo')
    expect(problema.codigo).toBe('CPF_JA_CADASTRADO')
    expect(problema.cadastroExistente).toEqual({
      funcionarioId: 201,
      nome: 'Henrique Alves',
      ativo: false,
    })
  })

  it('sem nome devolve o primeiro exemplo da resposta (cpf, no 409 de criarFuncionario)', () => {
    const problema = exemploDoContrato<Duplicado>('criarFuncionario', 409)
    expect(problema.cadastroExistente.funcionarioId).toBe(12)
  })

  it('devolve uma copia: alterar o resultado nao muda o proximo', () => {
    const primeira = exemploDoContrato<{ usuario: { nome: string } }>('entrar')
    primeira.usuario.nome = 'Outra Pessoa'
    const segunda = exemploDoContrato<{ usuario: { nome: string } }>('entrar')
    expect(segunda.usuario.nome).toBe('Ana Souza')
  })

  it('operacao, status ou nome que nao existem falham dizendo o que existe', () => {
    expect(() => exemploDoContrato('operacaoInventada')).toThrow(/operacaoInventada/)
    expect(() => exemploDoContrato('criarFuncionario', 418)).toThrow(/tem: .*409/)
    expect(() => exemploDoContrato('criarFuncionario', 409, 'nada')).toThrow(
      /tem: cpf, email, cpfInativo, emailInativo/,
    )
  })
})
