import { describe, expect, it } from 'vitest'
import { destinoDoPerfil, destinoSeguro, inicioDoPerfil, PERFIS_POR_FAMILIA } from './caminhos'

describe('caminhos', () => {
  it('inicioDoPerfil escolhe o inicio de cada perfil', () => {
    expect(inicioDoPerfil('RH')).toBe('/rh/painel')
    expect(inicioDoPerfil('Funcionario')).toBe('/eu/inicio')
    expect(inicioDoPerfil('Gestor')).toBe('/eu/inicio')
    expect(PERFIS_POR_FAMILIA).toEqual({
      rh: ['RH'],
      gestor: ['Gestor'],
      eu: ['Funcionario', 'Gestor', 'RH'],
    })
  })

  it.each([
    ['/rh/painel', '/rh/painel'],
    ['/eu/inicio?x=1', '/eu/inicio?x=1'],
    ['//exemplo.com', null],
    ['https://exemplo.com', null],
    ['/\\exemplo.com', null],
    ['javascript:alert(1)', null],
    ['/entrar', null],
    ['/entrar?voltar=%2Frh', null],
    ['/' + 'a'.repeat(600), null],
    ['', null],
    [null, null],
  ])('destinoSeguro(%s) (Review Focus 4)', (entrada, saida) => {
    expect(destinoSeguro(entrada)).toBe(saida)
  })
})

describe('destinoDoPerfil', () => {
  it.each([
    ['/rh/painel', 'RH', '/rh/painel'],
    ['/rh/painel', 'Gestor', null],
    ['/rh/colaboradores?pagina=2', 'Funcionario', null],
    ['/gestor/equipe', 'Gestor', '/gestor/equipe'],
    ['/gestor/equipe', 'RH', null],
    ['/eu/ferias', 'Funcionario', '/eu/ferias'],
    ['/formulario/abc', 'Funcionario', '/formulario/abc'],
    ['//exemplo.com', 'RH', null],
    [null, 'RH', null],
  ] as const)('%s para %s vira %s', (voltar, perfil, esperado) => {
    expect(destinoDoPerfil(voltar, perfil)).toBe(esperado)
  })
})
