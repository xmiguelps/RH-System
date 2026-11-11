import { $api } from '../../shared/api/api'
import type { Esquema } from '../../shared/api/tipos'

type Formulario = Esquema<'FormularioDetalheRh'>

/** O que se pode mudar na etapa 1, pela situação (contrato `editarFormulario`). */
export function travasDosDetalhes(f: Formulario | undefined): {
  tudo: boolean
  tipoEAnonimo: boolean
  dica: string | null
} {
  if (!f || f.situacao === 'RASCUNHO' || f.situacao === 'AGENDADO')
    return { tudo: false, tipoEAnonimo: false, dica: null }
  if (f.situacao === 'ENCERRADO')
    return { tudo: true, tipoEAnonimo: true, dica: 'Formulário encerrado não muda.' }
  if (f.anonimo)
    return {
      tudo: false,
      tipoEAnonimo: true,
      dica: 'Depois de publicado, o formulário não muda entre anônimo e identificado.',
    }
  // Publicado com nome: o anonimato nunca muda; com respostas, só título e descrição.
  return {
    tudo: false,
    tipoEAnonimo: true,
    dica: f.temRespostas
      ? 'Este formulário já tem respostas: só o título e a descrição mudam.'
      : 'Depois de publicado, o formulário não muda entre anônimo e identificado.',
  }
}

/** Em anônimo publicado e em encerrado, as perguntas são só leitura (nunca diz se há resposta). */
export function perguntasCongeladas(f: Formulario): string | null {
  if (f.situacao === 'ENCERRADO') return 'Formulário encerrado não muda.'
  if (f.anonimo && f.situacao === 'PUBLICADO')
    return 'As perguntas de um formulário anônimo publicado não mudam.'
  return null
}

/** A conferência da etapa 3 (também lida para ligar o "Próximo"). */
export function useVerificacao(formularioId: number, ativa = true) {
  return $api.useQuery(
    'get',
    '/api/rh/formularios/{formularioId}/verificacao',
    { params: { path: { formularioId } } },
    { enabled: ativa },
  )
}
