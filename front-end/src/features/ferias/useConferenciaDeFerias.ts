import { useEffect, useRef, useState } from 'react'
import { clienteHttp } from '../../shared/api/api'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'

type Conferencia = Esquema<'FeriasConferencia'>
type Comando = Esquema<'FeriasConferenciaComando'>

export type DadosDasFerias = {
  dataInicio: string
  dataFim: string
  abonoPecuniario: boolean
  diasAbono: string
  adiantar13Salario: boolean
  texto: string
}

export const FERIAS_VAZIAS: DadosDasFerias = {
  dataInicio: '',
  dataFim: '',
  abonoPecuniario: false,
  diasAbono: '',
  adiantar13Salario: false,
  texto: '',
}

/** Só datas preenchidas e fim não anterior ao início: o resto das regras é da API. */
export function datasProntas(d: DadosDasFerias): boolean {
  return !!d.dataInicio && !!d.dataFim && d.dataFim >= d.dataInicio
}

export function comandoDaConferencia(d: DadosDasFerias): Comando {
  return {
    dataInicio: d.dataInicio,
    dataFim: d.dataFim,
    abonoPecuniario: d.abonoPecuniario,
    diasAbono: d.abonoPecuniario ? Number(d.diasAbono) || 0 : 0,
  }
}

/** Para quem confere: a pessoa (`/api/eu`) ou o RH, para alguém da ficha. */
export type Conferente = { tipo: 'eu' } | { tipo: 'rh'; funcionarioId: number }

async function conferir(quem: Conferente, corpo: Comando): Promise<Conferencia | undefined> {
  if (quem.tipo === 'eu') {
    const { data } = await clienteHttp.POST('/api/eu/ferias/conferencia', { body: corpo })
    return data
  }
  const { data } = await clienteHttp.POST('/api/rh/funcionarios/{funcionarioId}/ferias/conferencia', {
    params: { path: { funcionarioId: quem.funcionarioId } },
    body: corpo,
  })
  return data
}

/**
 * A conferência de férias: espera 300 ms depois da última mudança e descarta a resposta antiga
 * (vale a última). Não chama com datas vazias nem com o fim antes do início.
 */
export function useConferenciaDeFerias(quem: Conferente, dados: DadosDasFerias) {
  const [resultado, setResultado] = useState<{
    chave: string
    conferencia: Conferencia | null
    erro: string | null
  } | null>(null)
  const ultimaChave = useRef('')
  const pronto = datasProntas(dados)
  const chave = pronto ? JSON.stringify(comandoDaConferencia(dados)) : ''
  const funcionarioId = quem.tipo === 'rh' ? quem.funcionarioId : null

  useEffect(() => {
    ultimaChave.current = chave
    if (!chave) return
    const espera = setTimeout(() => {
      const corpo = JSON.parse(chave) as Comando
      const alvo: Conferente = funcionarioId === null ? { tipo: 'eu' } : { tipo: 'rh', funcionarioId }
      conferir(alvo, corpo)
        .then((r) => {
          if (ultimaChave.current === chave) setResultado({ chave, conferencia: r ?? null, erro: null })
        })
        .catch((e: unknown) => {
          if (ultimaChave.current === chave)
            setResultado({ chave, conferencia: null, erro: mensagemDeErro(e) })
        })
    }, 300)
    return () => clearTimeout(espera)
  }, [chave, funcionarioId])

  const atual = resultado?.chave === chave ? resultado : null
  return {
    conferencia: pronto ? (atual?.conferencia ?? null) : null,
    conferindo: pronto && !atual,
    erro: atual?.erro ?? null,
  }
}
