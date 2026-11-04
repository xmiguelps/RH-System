import { ErroDeApi } from './erroDeApi'

const SEM_CONEXAO = 'Não foi possível conectar. Tente novamente.'

export function mensagemDeErro(erro: unknown): string {
  if (!(erro instanceof ErroDeApi) || erro.status === 0) return SEM_CONEXAO
  const { status, detail, title } = erro
  if (status === 400) return detail ?? 'Confira os dados informados.'
  if (status === 401) return 'Sua sessão expirou. Entre de novo.'
  if (status === 403) return detail ?? 'Você não tem permissão para esta ação.'
  if (status === 404) return detail ?? 'O registro não foi encontrado.'
  if (status === 409 || status === 422) return detail ?? title
  if (status === 429) return 'Muitas tentativas. Aguarde um minuto e tente de novo.'
  if (status >= 500) return 'Erro no servidor. Tente novamente em instantes.'
  return detail ?? title
}
