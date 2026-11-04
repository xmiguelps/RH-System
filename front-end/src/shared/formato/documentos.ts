const VAZIO = '—'

export function somenteDigitos(valor: string): string {
  return valor.replace(/\D/g, '')
}

export function mascararCpf(valor: string): string {
  const d = somenteDigitos(valor).slice(0, 11)
  if (d.length <= 3) return d
  if (d.length <= 6) return `${d.slice(0, 3)}.${d.slice(3)}`
  if (d.length <= 9) return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6)}`
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`
}

export function mascararTelefone(valor: string): string {
  const d = somenteDigitos(valor).slice(0, 11)
  if (d.length === 0) return ''
  if (d.length <= 2) return `(${d}`
  const ddd = d.slice(0, 2)
  const numero = d.slice(2)
  // Com 11 dígitos o número tem 5 antes do hífen (celular); com 10 ou menos, 4.
  const antes = d.length === 11 ? 5 : 4
  if (numero.length <= antes) return `(${ddd}) ${numero}`
  return `(${ddd}) ${numero.slice(0, antes)}-${numero.slice(antes)}`
}

export function mascararCep(valor: string): string {
  const d = somenteDigitos(valor).slice(0, 8)
  return d.length <= 5 ? d : `${d.slice(0, 5)}-${d.slice(5)}`
}

export function formatarCep(cep: string | null | undefined): string {
  if (!cep) return VAZIO
  return mascararCep(cep)
}

export function formatarCpf(cpf: string | null | undefined): string {
  if (!cpf) return VAZIO
  return mascararCpf(cpf)
}

export function formatarTelefone(telefone: string | null | undefined): string {
  if (!telefone) return VAZIO
  return mascararTelefone(telefone)
}

function digitoVerificador(digitos: string, pesoInicial: number): number {
  let soma = 0
  for (let i = 0; i < digitos.length; i += 1) soma += Number(digitos[i]) * (pesoInicial - i)
  const resto = (soma * 10) % 11
  return resto === 10 ? 0 : resto
}

export function cpfValido(digitos: string): boolean {
  if (!/^\d{11}$/.test(digitos)) return false
  if (/^(\d)\1{10}$/.test(digitos)) return false
  const primeiro = digitoVerificador(digitos.slice(0, 9), 10)
  const segundo = digitoVerificador(digitos.slice(0, 10), 11)
  return primeiro === Number(digitos[9]) && segundo === Number(digitos[10])
}

export function telefoneValido(digitos: string): boolean {
  return /^\d{10,11}$/.test(digitos)
}
