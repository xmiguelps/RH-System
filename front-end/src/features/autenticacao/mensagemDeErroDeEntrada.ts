import { ErroDeApi } from '../../shared/api/erroDeApi'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'

export const MENSAGEM_CAMPOS_VAZIOS = 'Preencha o e-mail e a senha.'

/** Na entrada, 401 é "e-mail ou senha" (não "sessão expirou"); o resto (conexão, limite com espera, servidor) é o texto comum. */
export function mensagemDeErroDeEntrada(erro: unknown): string {
  if (erro instanceof ErroDeApi) {
    if (erro.status === 401) return 'E-mail ou senha inválidos.'
    if (erro.status === 403) return erro.detail ?? 'Seu acesso está desativado. Fale com o RH.'
    if (erro.status === 400) return MENSAGEM_CAMPOS_VAZIOS
  }
  return mensagemDeErro(erro)
}
