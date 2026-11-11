import { useCallback, useState } from 'react'
import { mensagemDeErro } from '../api/mensagemDeErro'
import type { Esquema } from '../api/tipos'
import { useNotificacao } from '../ui/useNotificacao'
import { abrirLinkTemporario } from './baixar'

/** Arquivo guardado (documento, holerite, anexo): pede o link temporário e o abre como anexo. */
export function useBaixarPorLink(): {
  baixar(chamada: () => Promise<{ data?: Esquema<'LinkTemporario'> }>): Promise<void>
  baixando: boolean
} {
  const notificacao = useNotificacao()
  const [baixando, setBaixando] = useState(false)
  const baixar = useCallback(
    async (chamada: () => Promise<{ data?: Esquema<'LinkTemporario'> }>) => {
      setBaixando(true)
      try {
        const { data } = await chamada()
        if (data) abrirLinkTemporario(data)
      } catch (erro) {
        notificacao.erro(mensagemDeErro(erro))
      } finally {
        setBaixando(false)
      }
    },
    [notificacao],
  )
  return { baixar, baixando }
}
