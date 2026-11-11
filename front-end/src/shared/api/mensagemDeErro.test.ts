import { describe, expect, it } from 'vitest'
import { ErroDeApi } from './erroDeApi'
import { mensagemDeErro } from './mensagemDeErro'

describe('mensagemDeErro', () => {
  it.each([
    [0, 'Não foi possível conectar. Tente novamente.'],
    [401, 'Sua sessão expirou. Entre de novo.'],
    [429, 'Muitas tentativas. Aguarde um minuto e tente de novo.'],
    [500, 'Erro no servidor. Tente novamente em instantes.'],
    [503, 'Erro no servidor. Tente novamente em instantes.'],
  ])('status %i', (status, mensagem) => {
    expect(mensagemDeErro(new ErroDeApi(status, 'Titulo.', { detail: 'Detalhe.' }))).toBe(mensagem)
  })

  it('400, 403, 404, 409 e 422 mostram o detail quando vem e um texto padrao quando nao', () => {
    expect(mensagemDeErro(new ErroDeApi(400, 'T.', { detail: 'D400.' }))).toBe('D400.')
    expect(mensagemDeErro(new ErroDeApi(400, 'T.'))).toBe('Confira os dados informados.')
    expect(mensagemDeErro(new ErroDeApi(403, 'T.', { detail: 'D403.' }))).toBe('D403.')
    expect(mensagemDeErro(new ErroDeApi(403, 'T.'))).toBe('Você não tem permissão para esta ação.')
    expect(mensagemDeErro(new ErroDeApi(404, 'T.', { detail: 'D404.' }))).toBe('D404.')
    expect(mensagemDeErro(new ErroDeApi(404, 'T.'))).toBe('O registro não foi encontrado.')
    expect(mensagemDeErro(new ErroDeApi(409, 'Conflito.', { detail: 'D409.' }))).toBe('D409.')
    expect(mensagemDeErro(new ErroDeApi(409, 'Conflito.'))).toBe('Conflito.')
    expect(mensagemDeErro(new ErroDeApi(422, 'Regra.', { detail: 'D422.' }))).toBe('D422.')
    expect(mensagemDeErro(new ErroDeApi(422, 'Regra.'))).toBe('Regra.')
  })

  it('erro que nao e ErroDeApi vira a mensagem de conexao', () => {
    expect(mensagemDeErro(new Error('x'))).toBe('Não foi possível conectar. Tente novamente.')
    expect(mensagemDeErro(undefined)).toBe('Não foi possível conectar. Tente novamente.')
  })
})
