import type { Esquema } from '../../shared/api/tipos'

type Pergunta = Esquema<'PerguntaFormulario'>
type Item = Esquema<'RespostaFormularioItem'>

/** A resposta enquanto a pessoa preenche (só na memória da tela; nunca em localStorage). */
export type RespostaLocal = {
  texto?: string
  alternativaId?: number
  valorEscala?: number
  arquivo?: File | null
}

export function estaRespondida(pergunta: Pergunta, r: RespostaLocal | undefined): boolean {
  if (!r) return false
  switch (pergunta.tipo) {
    case 'RESPOSTA_CURTA':
    case 'RESPOSTA_LONGA':
      return !!r.texto?.trim()
    case 'MULTIPLA_ESCOLHA':
      return r.alternativaId !== undefined
    case 'ESCALA':
      return r.valorEscala !== undefined
    case 'ARQUIVO':
      return r.arquivo instanceof File
  }
}

/**
 * Os itens do envio: cada resposta usa só o campo do tipo da pergunta; o arquivo vai pela
 * posição (`arquivoIndice`) na lista `arquivos`.
 */
export function montarEnvio(
  perguntas: readonly Pergunta[],
  respostas: Readonly<Record<number, RespostaLocal>>,
): { respostas: Item[]; arquivos: File[] } {
  const itens: Item[] = []
  const arquivos: File[] = []
  for (const p of perguntas) {
    const r = respostas[p.id]
    if (!estaRespondida(p, r) || !r) continue
    if (p.tipo === 'RESPOSTA_CURTA' || p.tipo === 'RESPOSTA_LONGA')
      itens.push({ perguntaId: p.id, texto: r.texto!.trim() })
    else if (p.tipo === 'MULTIPLA_ESCOLHA')
      itens.push({ perguntaId: p.id, alternativaIds: [r.alternativaId!] })
    else if (p.tipo === 'ESCALA') itens.push({ perguntaId: p.id, valorEscala: r.valorEscala! })
    else {
      itens.push({ perguntaId: p.id, arquivoIndice: arquivos.length })
      arquivos.push(r.arquivo as File)
    }
  }
  return { respostas: itens, arquivos }
}
