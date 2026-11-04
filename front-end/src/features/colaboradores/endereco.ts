import type { Esquema } from '../../shared/api/tipos'
import { formatarCep } from '../../shared/formato/documentos'

/** "{logradouro}, {numero}, {complemento}, {bairro}, {cidade}/{uf}, CEP {cep}", omitindo o que é null. */
export function formatarEndereco(e: Esquema<'FuncionarioEndereco'>): string {
  const cidade = [e.cidade, e.uf].filter(Boolean).join('/')
  const partes = [
    e.logradouro,
    e.numero,
    e.complemento,
    e.bairro,
    cidade || null,
    e.cep ? `CEP ${formatarCep(e.cep)}` : null,
  ].filter(Boolean)
  return partes.length > 0 ? partes.join(', ') : '—'
}
