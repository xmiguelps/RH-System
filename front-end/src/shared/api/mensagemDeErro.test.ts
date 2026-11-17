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

  it('so a falta de conexao (status 0) diz que nao conectou; um erro de programa diz que algo deu errado', () => {
    expect(mensagemDeErro(new ErroDeApi(0, 'Sem conexão.'))).toBe(
      'Não foi possível conectar. Tente novamente.',
    )
    expect(mensagemDeErro(new Error('x'))).toBe('Algo deu errado. Tente novamente.')
    expect(mensagemDeErro(undefined)).toBe('Algo deu errado. Tente novamente.')
  })

  it('429 usa o Retry-After quando ele vem (segundos ate 90, depois minutos arredondados para cima)', () => {
    const com = (s: number) =>
      mensagemDeErro(new ErroDeApi(429, 'T.', { retryAfterSegundos: s }))
    expect(com(1)).toBe('Muitas tentativas. Aguarde 1 segundo e tente de novo.')
    expect(com(30)).toBe('Muitas tentativas. Aguarde 30 segundos e tente de novo.')
    expect(com(90)).toBe('Muitas tentativas. Aguarde 90 segundos e tente de novo.')
    expect(com(91)).toBe('Muitas tentativas. Aguarde 2 minutos e tente de novo.')
    expect(com(120)).toBe('Muitas tentativas. Aguarde 2 minutos e tente de novo.')
    expect(com(0)).toBe('Muitas tentativas. Aguarde um minuto e tente de novo.')
    expect(mensagemDeErro(new ErroDeApi(429, 'T.'))).toBe(
      'Muitas tentativas. Aguarde um minuto e tente de novo.',
    )
  })

  it('413 (arquivo ou envio grande demais) tem texto proprio', () => {
    expect(mensagemDeErro(new ErroDeApi(413, 'Too Large'))).toBe(
      'O arquivo ou os dados enviados são grandes demais.',
    )
    expect(mensagemDeErro(new ErroDeApi(413, 'T.', { detail: 'O vídeo passa de 100 MB.' }))).toBe(
      'O vídeo passa de 100 MB.',
    )
  })
})
