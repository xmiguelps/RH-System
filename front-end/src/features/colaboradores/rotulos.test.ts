import { describe, expect, it } from 'vitest'
import { etiquetaDaSituacao, rotuloDoNivelDeAcesso, rotuloDoTipoDeContrato } from './rotulos'

describe('rotulos de colaboradores', () => {
  it('rotulos de tipo de contrato e de nivel de acesso', () => {
    expect(rotuloDoNivelDeAcesso('Funcionario')).toBe('Colaborador')
    expect(rotuloDoNivelDeAcesso('Gestor')).toBe('Gestor')
    expect(rotuloDoNivelDeAcesso('RH')).toBe('RH')
    expect(rotuloDoNivelDeAcesso(null)).toBe('—')
    expect(rotuloDoTipoDeContrato('CLT')).toBe('CLT')
    expect(rotuloDoTipoDeContrato('ESTAGIO')).toBe('Estágio')
    expect(rotuloDoTipoDeContrato('NOVO')).toBe('NOVO')
    expect(rotuloDoTipoDeContrato(null)).toBe('—')
  })

  it.each([
    ['INATIVO', null, 'Inativo', 'neutro'],
    ['EM_ADMISSAO', null, 'Em admissão', 'contorno'],
    ['ATIVO', 'FERIAS', 'Férias', 'info'],
    ['ATIVO', 'ATESTADO', 'Atestado', 'alerta'],
    ['ATIVO', 'SEM_REGISTRO', 'Ativo', 'sucesso'],
    ['ATIVO', 'TRABALHANDO', 'Ativo', 'sucesso'],
    ['ATIVO', null, 'Ativo', 'sucesso'],
  ] as const)('etiquetaDaSituacao(%s, %s)', (situacao, hoje, texto, tom) => {
    expect(etiquetaDaSituacao(situacao, hoje)).toEqual({ texto, tom })
  })
})
