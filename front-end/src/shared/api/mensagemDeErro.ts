import { ErroDeApi } from './erroDeApi'

const SEM_CONEXAO = 'Não foi possível conectar. Tente novamente.'

/** "Aguarde ..." do 429: usa o `Retry-After` quando ele vem (segundos até 90, depois minutos para cima). */
function esperaDoLimite(segundos: number | null): string {
  if (!segundos || segundos < 1) return 'um minuto'
  if (segundos <= 90) return segundos === 1 ? '1 segundo' : `${segundos} segundos`
  return `${Math.ceil(segundos / 60)} minutos`
}

export function mensagemDeErro(erro: unknown): string {
  // Só a falta de conexão diz "não conectou": um erro de programa (TypeError etc.) é outra coisa.
  if (!(erro instanceof ErroDeApi)) return 'Algo deu errado. Tente novamente.'
  if (erro.status === 0) return SEM_CONEXAO
  const { status, detail, title } = erro
  if (status === 400) return detail ?? 'Confira os dados informados.'
  if (status === 401) return 'Sua sessão expirou. Entre de novo.'
  if (status === 403) return detail ?? 'Você não tem permissão para esta ação.'
  if (status === 404) return detail ?? 'O registro não foi encontrado.'
  if (status === 409 || status === 422) return detail ?? title
  if (status === 413) return detail ?? 'O arquivo ou os dados enviados são grandes demais.'
  if (status === 429) {
    return `Muitas tentativas. Aguarde ${esperaDoLimite(erro.retryAfterSegundos)} e tente de novo.`
  }
  if (status >= 500) return 'Erro no servidor. Tente novamente em instantes.'
  return detail ?? title
}
