import { describe, expect, it } from 'vitest'
import { sessaoDeTeste } from '../test/sessao'
import { linhaDeApoio } from './linhaDeApoio'

const usuario = (perfil: 'RH' | 'Gestor' | 'Funcionario', extra = {}) =>
  sessaoDeTeste(perfil, extra).usuario

describe('linhaDeApoio', () => {
  it('RH tem o texto fixo', () => {
    expect(linhaDeApoio(usuario('RH'))).toBe('Perfil RH, acesso total')
  })
  it('Gestor com e sem setor', () => {
    expect(linhaDeApoio(usuario('Gestor'))).toBe('Perfil gestor, setor Comercial')
    expect(linhaDeApoio(usuario('Gestor', { setor: null }))).toBe('Perfil gestor')
  })
  it('Funcionario com cargo e setor, so com cargo, e com os dois nulos', () => {
    expect(linhaDeApoio(usuario('Funcionario'))).toBe('Analista pleno, Financeiro')
    expect(linhaDeApoio(usuario('Funcionario', { cargo: null }))).toBe('Financeiro')
    expect(linhaDeApoio(usuario('Funcionario', { setor: null }))).toBe('Analista pleno')
    expect(linhaDeApoio(usuario('Funcionario', { cargo: null, setor: null }))).toBe('')
  })
})
