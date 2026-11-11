import type { Esquema } from '../../shared/api/tipos'

export type ColunaDaImportacao = Esquema<'ImportacaoColuna'>
type ChaveDosValores = keyof Esquema<'ImportacaoLinhaValores'>

/**
 * A tabela das 11 colunas do modelo (`telas\fatia-2.md`, estado C). `Record` de propósito: se o contrato
 * mudar `ImportacaoColuna` (x-provisorio), o tipo gerado deixa de compilar aqui, e só aqui.
 */
export const COLUNAS_DO_MODELO: Record<
  ColunaDaImportacao,
  { titulo: string; tituloCurto: string; chave: ChaveDosValores }
> = {
  NOME: { titulo: 'Nome completo', tituloCurto: 'Nome', chave: 'nome' },
  CPF: { titulo: 'CPF', tituloCurto: 'CPF', chave: 'cpf' },
  DATA_NASCIMENTO: {
    titulo: 'Data de nascimento',
    tituloCurto: 'Nascimento',
    chave: 'dataNascimento',
  },
  TELEFONE: { titulo: 'Telefone', tituloCurto: 'Telefone', chave: 'telefone' },
  EMAIL_CORPORATIVO: {
    titulo: 'E-mail corporativo',
    tituloCurto: 'E-mail corporativo',
    chave: 'emailCorporativo',
  },
  DATA_ADMISSAO: { titulo: 'Data de admissão', tituloCurto: 'Admissão', chave: 'dataAdmissao' },
  TIPO_CONTRATO: { titulo: 'Tipo de contrato', tituloCurto: 'Contrato', chave: 'tipoContrato' },
  CARGO: { titulo: 'Cargo', tituloCurto: 'Cargo', chave: 'cargo' },
  SETOR: { titulo: 'Setor', tituloCurto: 'Setor', chave: 'setor' },
  NIVEL_DE_ACESSO: {
    titulo: 'Nível de acesso',
    tituloCurto: 'Nível de acesso',
    chave: 'nivelDeAcesso',
  },
  BENEFICIOS: { titulo: 'Benefícios', tituloCurto: 'Benefícios', chave: 'beneficios' },
}

/** Na ordem do modelo. */
export const ORDEM_DAS_COLUNAS: readonly ColunaDaImportacao[] = [
  'NOME',
  'CPF',
  'DATA_NASCIMENTO',
  'TELEFONE',
  'EMAIL_CORPORATIVO',
  'DATA_ADMISSAO',
  'TIPO_CONTRATO',
  'CARGO',
  'SETOR',
  'NIVEL_DE_ACESSO',
  'BENEFICIOS',
]

export const TIPO_XLSX = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
export const LIMITE_DA_PLANILHA_MB = 5
export const LINHAS_POR_PAGINA_DA_PREVIA = 50
