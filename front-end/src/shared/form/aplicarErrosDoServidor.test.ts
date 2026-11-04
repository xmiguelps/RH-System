import { describe, expect, it, vi } from 'vitest'
import { ErroDeApi } from '../api/erroDeApi'
import { aplicarErrosDoServidor } from './aplicarErrosDoServidor'

const CAMPOS = ['nome', 'cpf', 'emailCorporativo'] as const

describe('aplicarErrosDoServidor', () => {
  it('400 com errors marca cada campo que o formulario tem', () => {
    const definirErro = vi.fn()
    const erro = new ErroDeApi(400, 'Confira os campos indicados.', {
      errors: { cpf: ['CPF já cadastrado.'], emailCorporativo: ['E-mail já cadastrado.'] },
    })
    const { mensagemGeral } = aplicarErrosDoServidor(erro, {
      definirErro,
      camposDoFormulario: CAMPOS,
    })
    expect(definirErro).toHaveBeenCalledTimes(2)
    expect(definirErro).toHaveBeenCalledWith('cpf', 'CPF já cadastrado.')
    expect(definirErro).toHaveBeenCalledWith('emailCorporativo', 'E-mail já cadastrado.')
    expect(mensagemGeral).toBeNull()
  })

  it('chave que o formulario nao tem vai para a mensagem geral (nao some)', () => {
    const definirErro = vi.fn()
    const erro = new ErroDeApi(400, 'Confira os campos indicados.', {
      errors: { cpf: ['CPF já cadastrado.'], nivelDeAcesso: ['Valor inválido.'] },
    })
    const { mensagemGeral } = aplicarErrosDoServidor(erro, {
      definirErro,
      camposDoFormulario: CAMPOS,
    })
    expect(definirErro).toHaveBeenCalledTimes(1)
    expect(mensagemGeral).toContain('Valor inválido.')
  })

  it('chave com inicial maiuscula (Nome) casa com o campo nome', () => {
    const definirErro = vi.fn()
    aplicarErrosDoServidor(
      new ErroDeApi(400, 'x', { errors: { Nome: ['Informe o nome.', 'Curto.'] } }),
      {
        definirErro,
        camposDoFormulario: CAMPOS,
      },
    )
    expect(definirErro).toHaveBeenCalledWith('nome', 'Informe o nome. Curto.')
  })

  it('422 sem errors usa o detail; sem detail usa a mensagem de erro', () => {
    const definirErro = vi.fn()
    expect(
      aplicarErrosDoServidor(new ErroDeApi(422, 'Regra.', { detail: 'Período bloqueado.' }), {
        definirErro,
        camposDoFormulario: CAMPOS,
      }).mensagemGeral,
    ).toBe('Período bloqueado.')
    expect(
      aplicarErrosDoServidor(new ErroDeApi(422, 'Regra do banco.'), {
        definirErro,
        camposDoFormulario: CAMPOS,
      }).mensagemGeral,
    ).toBe('Regra do banco.')
    expect(definirErro).not.toHaveBeenCalled()
  })

  it('status 0 vira a mensagem de conexao', () => {
    expect(
      aplicarErrosDoServidor(ErroDeApi.semConexao('x'), {
        definirErro: () => {},
        camposDoFormulario: CAMPOS,
      }).mensagemGeral,
    ).toBe('Não foi possível conectar. Tente novamente.')
  })
})
