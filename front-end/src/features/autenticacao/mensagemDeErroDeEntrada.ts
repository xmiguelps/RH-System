import { ErroDeApi } from '../../shared/api/erroDeApi'

export const MENSAGEM_CAMPOS_VAZIOS = 'Preencha o e-mail e a senha.'

export function mensagemDeErroDeEntrada(erro: unknown): string {
  if (!(erro instanceof ErroDeApi)) return 'Não foi possível conectar. Tente novamente.'
  if (erro.status === 401) return 'E-mail ou senha inválidos.'
  if (erro.status === 403) return erro.detail ?? 'Seu acesso está desativado. Fale com o RH.'
  if (erro.status === 400) return MENSAGEM_CAMPOS_VAZIOS
  if (erro.status === 429) return 'Muitas tentativas. Aguarde um minuto e tente de novo.'
  if (erro.status >= 500) return 'Erro no servidor. Tente novamente em instantes.'
  return 'Não foi possível conectar. Tente novamente.'
}
