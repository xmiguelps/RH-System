import { ErroDeApi } from '../api/erroDeApi'

/**
 * Erro de regra (409 ou 422) que vem sem `errors` mas pertence a um campo: devolve o mesmo erro com
 * `errors: { campo: [detail] }`, para o formulário marcar o campo em vez do aviso geral.
 */
export function erroNoCampo(
  erro: unknown,
  campoPorCodigo: Readonly<Record<string, string>>,
): unknown {
  if (!(erro instanceof ErroDeApi) || !erro.codigo) return erro
  const campo = campoPorCodigo[erro.codigo]
  if (!campo || Object.keys(erro.errors).length > 0) return erro
  return new ErroDeApi(erro.status, erro.title, {
    detail: erro.detail,
    codigo: erro.codigo,
    traceId: erro.traceId,
    errors: { [campo]: [erro.detail ?? erro.title] },
    retryAfterSegundos: erro.retryAfterSegundos,
    corpo: erro.corpo,
  })
}
