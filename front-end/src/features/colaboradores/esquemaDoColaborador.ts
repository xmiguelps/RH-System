import { z } from 'zod'
import type { Esquema } from '../../shared/api/tipos'
import { campoCpf, campoData, campoTelefone, opcional } from '../../shared/form/esquemas'
import { hojeEmSaoPaulo } from '../../shared/formato/datas'
import { somenteDigitos } from '../../shared/formato/documentos'

const email = (maximo: number) =>
  z.string().trim().max(maximo).pipe(z.email('Informe um e-mail válido.'))
const texto = (maximo: number) => z.string().trim().max(maximo)

/** Um só esquema para o cadastro (tela 02) e a edição (tela 03); os campos da edição são opcionais. */
export const esquemaDoColaborador = z.object({
  nome: z.string().trim().min(1, 'Informe o nome.').max(150),
  cpf: campoCpf(),
  dataNascimento: campoData().refine((data) => data <= hojeEmSaoPaulo(), {
    message: 'A data de nascimento não pode ser no futuro.',
  }),
  telefone: campoTelefone(),
  emailCorporativo: email(150),
  dataAdmissao: campoData(),
  tipoContrato: z.enum(['CLT', 'ESTAGIO']),
  cargoId: z.string().min(1, 'Escolha o cargo.').transform(Number),
  setorId: z.string().min(1, 'Escolha o setor.').transform(Number),
  perfil: z.enum(['Funcionario', 'Gestor', 'RH']),
  beneficioIds: z
    .array(z.number())
    .refine((ids) => new Set(ids).size === ids.length, { message: 'Benefício repetido.' }),
  enviarConvite: z.boolean(),
  emailPessoal: opcional(email(150)),
  rg: opcional(texto(20)),
  rgOrgaoEmissor: opcional(texto(20)),
  endereco: z.object({
    cep: opcional(
      z
        .string()
        .transform(somenteDigitos)
        .refine((cep) => cep.length === 8, { message: 'CEP inválido.' }),
    ),
    logradouro: opcional(texto(150)),
    numero: opcional(texto(10)),
    complemento: opcional(texto(100)),
    bairro: opcional(texto(100)),
    cidade: opcional(texto(100)),
    uf: opcional(z.string().regex(/^[A-Z]{2}$/, 'UF inválida.')),
  }),
})

export type EntradaDoColaborador = z.input<typeof esquemaDoColaborador>
export type SaidaDoColaborador = z.output<typeof esquemaDoColaborador>

// "CLT" e "Colaborador" por padrão (suponho: o PNG 02 mostra os dois).
export const valoresIniciaisDoCadastro: EntradaDoColaborador = {
  nome: '',
  cpf: '',
  dataNascimento: '',
  telefone: '',
  emailCorporativo: '',
  dataAdmissao: '',
  tipoContrato: 'CLT',
  cargoId: '',
  setorId: '',
  perfil: 'Funcionario',
  beneficioIds: [],
  enviarConvite: true,
  emailPessoal: '',
  rg: '',
  rgOrgaoEmissor: '',
  endereco: {
    cep: '',
    logradouro: '',
    numero: '',
    complemento: '',
    bairro: '',
    cidade: '',
    uf: '',
  },
}

/** Só as chaves de FuncionarioCriarComando; as caixas das fichas 5 e 6 ficam desligadas aqui. */
export function montarCorpoDeCadastro(
  v: SaidaDoColaborador,
  formularioAdmissaoId: number | null = null,
  incluirNoOnboarding = false,
): Esquema<'FuncionarioCriarComando'> {
  return {
    nome: v.nome,
    cpf: v.cpf,
    dataNascimento: v.dataNascimento,
    telefone: v.telefone,
    emailCorporativo: v.emailCorporativo,
    dataAdmissao: v.dataAdmissao,
    tipoContrato: v.tipoContrato,
    cargoId: v.cargoId,
    setorId: v.setorId,
    perfil: v.perfil,
    beneficioIds: v.beneficioIds,
    enviarConvite: v.enviarConvite,
    incluirNoOnboarding,
    formularioAdmissaoId,
  }
}

export function montarCorpoDeEdicao(v: SaidaDoColaborador): Esquema<'FuncionarioEditarComando'> {
  return {
    nome: v.nome,
    cpf: v.cpf,
    dataNascimento: v.dataNascimento,
    telefone: v.telefone,
    emailCorporativo: v.emailCorporativo,
    emailPessoal: v.emailPessoal,
    rg: v.rg,
    rgOrgaoEmissor: v.rgOrgaoEmissor,
    endereco: { ...v.endereco },
    dataAdmissao: v.dataAdmissao,
    tipoContrato: v.tipoContrato,
    cargoId: v.cargoId,
    setorId: v.setorId,
    perfil: v.perfil,
    beneficioIds: v.beneficioIds,
  }
}
