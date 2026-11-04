import { z } from 'zod'
import { cpfValido, somenteDigitos, telefoneValido } from '../formato/documentos'

export function campoTexto(mensagem?: string) {
  return z
    .string()
    .trim()
    .min(1, mensagem ?? 'Campo obrigatório.')
}

export function campoEmail() {
  return z.string().trim().pipe(z.email('Informe um e-mail válido.'))
}

export function campoCpf() {
  return z.string().transform(somenteDigitos).refine(cpfValido, { message: 'CPF inválido.' })
}

export function campoTelefone() {
  return z
    .string()
    .transform(somenteDigitos)
    .refine(telefoneValido, { message: 'Telefone inválido. Use DDD e número.' })
}

function dataExiste(texto: string): boolean {
  const achado = /^(\d{4})-(\d{2})-(\d{2})$/.exec(texto)
  if (!achado) return false
  const [ano, mes, dia] = [Number(achado[1]), Number(achado[2]), Number(achado[3])]
  const data = new Date(Date.UTC(ano, mes - 1, dia))
  return (
    data.getUTCFullYear() === ano && data.getUTCMonth() === mes - 1 && data.getUTCDate() === dia
  )
}

/** Data de calendário 'YYYY-MM-DD': o valor continua texto (sem Date), para nunca mudar de dia. */
export function campoData() {
  return z.string().refine(dataExiste, { message: 'Data inválida.' })
}

export function campoHora() {
  return z.string().regex(/^([01]\d|2[0-3]):[0-5]\d$/, 'Hora inválida.')
}

/** Texto vazio vira null; o resto vale o esquema. */
export function opcional<T extends z.ZodType>(esquema: T) {
  return z.union([z.literal('').transform(() => null), z.null(), esquema])
}
