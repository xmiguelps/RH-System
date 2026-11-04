import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { $api, clienteHttp } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import type { Esquema } from '../../shared/api/tipos'
import { baixarExportacao } from '../../shared/arquivos/baixar'
import { validarPlanilha } from './enviarPlanilha'

type Previa = Esquema<'ImportacaoPrevia'>
type Resultado = Esquema<'ImportacaoResultado'>

/** Os estados da tela (`telas\fatia-2.md`): A/G escolher, B enviando, C/D/E prévia, F resultado. */
export type EtapaDaImportacao =
  | { tipo: 'escolher'; erro?: unknown; erroDoArquivo?: string }
  | { tipo: 'enviando'; arquivo: File }
  | { tipo: 'previa'; previa: Previa; erro?: unknown }
  | { tipo: 'resultado'; resultado: Resultado }
  /** 409 IMPORTACAO_JA_CONFIRMADA sem o resultado em mãos: só resta fechar. */
  | { tipo: 'jaConfirmada'; erro: ErroDeApi }

const VOLTAM_AO_INICIO = [
  'IMPORTACAO_COM_ERROS',
  'IMPORTACAO_EXPIRADA',
  'IMPORTACAO_LINHA_RECUSADA',
]

function erroDoCampoArquivo(erro: unknown): string | undefined {
  if (!(erro instanceof ErroDeApi) || erro.status !== 400) return undefined
  return erro.errors.arquivo?.[0]
}

export function useImportacao() {
  const queryClient = useQueryClient()
  const [etapa, setEtapa] = useState<EtapaDaImportacao>({ tipo: 'escolher' })
  const [enviarConvite, setEnviarConvite] = useState(true)
  const [formularioAdmissaoId, setFormularioAdmissaoId] = useState<number | null>(null)
  const [incluirNoOnboarding, setIncluirNoOnboarding] = useState(false)
  const [baixandoModelo, setBaixandoModelo] = useState(false)
  const [erroDoModelo, setErroDoModelo] = useState<unknown>(null)

  const envio = useMutation({ mutationFn: (arquivo: File) => validarPlanilha(arquivo) })
  const confirmacao = $api.useMutation('post', '/api/rh/importacoes/{importacaoId}/confirmar')

  function reiniciar() {
    envio.reset()
    confirmacao.reset()
    setEnviarConvite(true)
    setEtapa({ tipo: 'escolher' })
  }

  async function enviar(arquivo: File) {
    if (envio.isPending) return
    setEtapa({ tipo: 'enviando', arquivo })
    try {
      const previa = await envio.mutateAsync(arquivo)
      setEtapa({ tipo: 'previa', previa })
    } catch (erro) {
      const doCampo = erroDoCampoArquivo(erro)
      setEtapa(doCampo ? { tipo: 'escolher', erroDoArquivo: doCampo } : { tipo: 'escolher', erro })
    }
  }

  // isPending só muda depois de renderizar: o clique duplo precisa de uma trava síncrona.
  const confirmando = useRef(false)

  async function confirmar() {
    if (etapa.tipo !== 'previa' || !etapa.previa.podeConfirmar || confirmando.current) return
    confirmando.current = true
    try {
      await confirmarAgora(etapa.previa)
    } finally {
      confirmando.current = false
    }
  }

  async function confirmarAgora(previa: Extract<typeof etapa, { tipo: 'previa' }>['previa']) {
    setEtapa({ tipo: 'previa', previa })
    try {
      const resultado = await confirmacao.mutateAsync({
        params: { path: { importacaoId: previa.importacaoId } },
        // As caixas de onboarding e de formulário ficam desligadas até as fichas 6 e 5.
        body: { enviarConvite, incluirNoOnboarding, formularioAdmissaoId },
      })
      setEtapa({ tipo: 'resultado', resultado })
      await invalidarRota(queryClient, '/api/rh/funcionarios')
    } catch (erro) {
      if (erro instanceof ErroDeApi && erro.status === 409) {
        setEtapa({ tipo: 'jaConfirmada', erro })
        await invalidarRota(queryClient, '/api/rh/funcionarios')
      } else if (
        erro instanceof ErroDeApi &&
        erro.status === 422 &&
        VOLTAM_AO_INICIO.includes(erro.codigo ?? '')
      ) {
        setEtapa({ tipo: 'escolher', erro })
      } else {
        setEtapa({ tipo: 'previa', previa, erro })
      }
    }
  }

  async function baixarModelo() {
    if (baixandoModelo) return
    setBaixandoModelo(true)
    setErroDoModelo(null)
    try {
      await baixarExportacao(
        clienteHttp.GET('/api/rh/importacoes/modelo', { parseAs: 'blob' }),
        'modelo-importacao-colaboradores.xlsx',
      )
    } catch (erro) {
      setErroDoModelo(erro)
    } finally {
      setBaixandoModelo(false)
    }
  }

  return {
    etapa,
    enviar,
    confirmar,
    reiniciar,
    confirmando: confirmacao.isPending,
    enviarConvite,
    setEnviarConvite,
    formularioAdmissaoId,
    setFormularioAdmissaoId,
    incluirNoOnboarding,
    setIncluirNoOnboarding,
    baixarModelo,
    baixandoModelo,
    erroDoModelo,
  }
}

export type Importacao = ReturnType<typeof useImportacao>
