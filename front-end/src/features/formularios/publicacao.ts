import type { Esquema } from '../../shared/api/tipos'

type Formulario = Esquema<'FormularioDetalheRh'>
type Unidade = Esquema<'FormularioRecorrenciaUnidade'>

/** O estado da etapa Publicação enquanto o RH preenche. */
export type Publicacao = {
  alvo: Esquema<'FormularioAlvo'> | null
  setorIds: number[]
  pessoas: { id: number; nome: string }[]
  quando: 'AGORA' | 'AGENDAR'
  agendarPara: string
  prazo: string
  lembreteAutomatico: boolean
  repetir: boolean
  intervalo: string
  unidade: Unidade
  ate: string
}

/** A publicação já diz para quem vai? Sem isso (ou com "pessoas" sem ninguém, ou "setores" sem nenhum), o contrato recusa o corpo (400): quem sai ou volta da etapa antes de escolher não manda nada. */
export function publicacaoCompleta(v: Publicacao): boolean {
  if (v.alvo === null) return false
  if (v.alvo === 'PESSOAS') return v.pessoas.length > 0
  if (v.alvo === 'SETORES') return v.setorIds.length > 0
  return true
}

export function publicacaoInicial(f: Formulario): Publicacao {
  const p = f.publicacao
  return {
    alvo: p.alvo,
    setorIds: p.setores.map((s) => s.id),
    pessoas: p.pessoas.map((x) => ({ id: x.id, nome: x.nome })),
    quando: p.agendarPara ? 'AGENDAR' : 'AGORA',
    agendarPara: p.agendarPara ?? '',
    prazo: p.prazo ?? '',
    lembreteAutomatico: p.lembreteAutomatico,
    repetir: p.recorrencia !== null,
    intervalo: p.recorrencia ? String(p.recorrencia.intervalo) : '1',
    unidade: p.recorrencia?.unidade ?? 'MES',
    ate: p.recorrencia?.ate ?? '',
  }
}

/** O corpo de `salvarPublicacaoFormulario` e de `publicarFormulario` (o mesmo). */
export function comandoDaPublicacao(
  v: Publicacao,
  tipo: Esquema<'FormularioTipo'>,
): Esquema<'FormularioPublicacaoComando'> {
  const alvo = v.alvo ?? 'PESSOAS'
  const ehSugestao = tipo === 'OUTRO'
  const comPrazo = !ehSugestao && !!v.prazo
  return {
    alvo,
    setorIds: alvo === 'SETORES' ? v.setorIds : [],
    funcionarioIds: alvo === 'PESSOAS' ? v.pessoas.map((p) => p.id) : [],
    agendarPara: v.quando === 'AGENDAR' && v.agendarPara ? v.agendarPara : null,
    prazo: comPrazo ? v.prazo : null,
    recorrencia:
      !ehSugestao && v.repetir
        ? {
            intervalo: Number(v.intervalo) || 1,
            unidade: v.unidade,
            ate: v.ate || null,
          }
        : null,
    lembreteAutomatico: comPrazo && alvo !== 'LINK' ? v.lembreteAutomatico : false,
  }
}

/** O link público completo (com o endereço do site) a partir do `tokenDoLink`. */
export function linkPublico(token: string): string {
  return `${window.location.origin}/formulario/${token}`
}
