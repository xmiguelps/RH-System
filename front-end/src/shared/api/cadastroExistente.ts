import { ErroDeApi } from './erroDeApi'
import type { Esquema } from './tipos'

export type CadastroExistente = NonNullable<
  Esquema<'ColaboradorDuplicadoProblema'>['cadastroExistente']
>

/** O cadastro que já usa o CPF ou o e-mail (só no 409 de criarFuncionario). */
export function lerCadastroExistente(erro: unknown): CadastroExistente | null {
  if (!(erro instanceof ErroDeApi) || erro.status !== 409) return null
  const cadastro = erro.corpo?.cadastroExistente
  if (typeof cadastro !== 'object' || cadastro === null) return null
  const c = cadastro as Record<string, unknown>
  if (typeof c.funcionarioId !== 'number' || typeof c.nome !== 'string') return null
  if (typeof c.ativo !== 'boolean') return null
  return { funcionarioId: c.funcionarioId, nome: c.nome, ativo: c.ativo }
}
