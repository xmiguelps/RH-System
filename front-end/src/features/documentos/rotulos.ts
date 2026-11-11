import type { Esquema } from '../../shared/api/tipos'
import type { OpcaoDeSelecao } from '../../shared/ui/Selecao'
import type { TomDoSelo } from '../../shared/ui/Selo'

type Tipo = Esquema<'DocumentoColaboradorTipo'>

const ROTULOS: Record<Tipo, string> = {
  IDENTIDADE: 'Documento de identidade (RG ou CNH)',
  CPF: 'CPF',
  CARTEIRA_DE_TRABALHO: 'Carteira de trabalho digital',
  COMPROVANTE_DE_RESIDENCIA: 'Comprovante de residência',
  CONTRATO_ASSINADO: 'Contrato assinado',
  EXAME_ADMISSIONAL: 'Exame admissional (ASO)',
  OUTRO: 'Outro',
}

/** Valor que o front não conhece (a lista é provisória) aparece como veio. */
export function rotuloDoTipoDeDocumento(tipo: Tipo | string): string {
  return ROTULOS[tipo as Tipo] ?? tipo
}

export const OPCOES_DE_TIPO_DE_DOCUMENTO: readonly OpcaoDeSelecao<Tipo>[] = (
  Object.keys(ROTULOS) as Tipo[]
).map((valor) => ({ valor, rotulo: ROTULOS[valor] }))

export const REGRAS_DE_ARQUIVO_DE_DOCUMENTO = {
  tiposAceitos: ['application/pdf', 'image/jpeg', 'image/png'],
  tamanhoMaximoMb: 10,
} as const

export function etiquetaDoDocumento(situacao: Esquema<'DocumentoColaboradorSituacao'>): {
  texto: string
  tom: TomDoSelo
} {
  return situacao === 'ENVIADO'
    ? { texto: 'Enviado', tom: 'sucesso' }
    : { texto: 'Pendente', tom: 'alerta' }
}
