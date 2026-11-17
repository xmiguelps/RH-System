export type ChaveDoCadastro =
  'cargos' | 'setores' | 'gestores' | 'beneficios' | 'usuarios' | 'empresa' | 'pagamento'

export type ItemDoCadastro = {
  chave: ChaveDoCadastro
  titulo: string
  apoio: string
  comContagem: boolean
}

// Sem "Níveis de acesso" (decisão do dono). "Pagamento" é da fatia 7.
export const ITENS_DO_CADASTRO: readonly ItemDoCadastro[] = [
  { chave: 'cargos', titulo: 'Cargos', apoio: 'Nome e carga horária', comContagem: true },
  { chave: 'setores', titulo: 'Setores', apoio: 'Áreas e seus gestores', comContagem: true },
  { chave: 'gestores', titulo: 'Gestores', apoio: 'Quem gerencia cada setor', comContagem: true },
  {
    chave: 'beneficios',
    titulo: 'Benefícios',
    apoio: 'O que a empresa oferece',
    comContagem: true,
  },
  {
    chave: 'usuarios',
    titulo: 'Usuários do sistema',
    apoio: 'Logins de acesso ao portal',
    comContagem: true,
  },
  {
    chave: 'empresa',
    titulo: 'Empresa',
    apoio: 'Nome, horário e dia de pagamento',
    comContagem: false,
  },
  {
    chave: 'pagamento',
    titulo: 'Pagamento',
    apoio: 'Calendário e exceções por mês',
    comContagem: false,
  },
]
