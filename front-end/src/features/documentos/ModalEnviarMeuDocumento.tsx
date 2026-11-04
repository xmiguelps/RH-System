import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { invalidarContadoresDoMenu } from '../../app/useContadoresDoMenu'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { Botao } from '../../shared/ui/Botao'
import { Janela } from '../../shared/ui/Janela'
import { SeletorDeArquivo } from '../../shared/ui/SeletorDeArquivo'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { enviarMeuArquivo } from './enviarDocumento'
import { REGRAS_DE_ARQUIVO_DE_DOCUMENTO, rotuloDoTipoDeDocumento } from './rotulos'

type Documento = Esquema<'DocumentoColaborador'>

function Conteudo({ documento, aoFechar }: { documento: Documento; aoFechar: () => void }) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const [arquivo, setArquivo] = useState<File | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const enviando = useRef(false)
  const envio = useMutation({ mutationFn: (a: File) => enviarMeuArquivo(documento.id, a) })

  const atualizar = () =>
    Promise.all([
      invalidarRota(queryClient, '/api/eu/documentos'),
      invalidarContadoresDoMenu(queryClient),
    ])

  const enviar = async () => {
    if (enviando.current) return
    if (!arquivo) {
      setErro('Escolha o arquivo.')
      return
    }
    enviando.current = true
    setErro(null)
    try {
      await envio.mutateAsync(arquivo)
      await atualizar()
      aoFechar()
    } catch (falha) {
      const doCampo = falha instanceof ErroDeApi ? falha.errors.arquivo : undefined
      if (doCampo) {
        setErro(doCampo.join(' '))
        return
      }
      if (falha instanceof ErroDeApi && (falha.status === 404 || falha.status === 422)) {
        notificacao.erro(mensagemDeErro(falha))
        await atualizar()
        aoFechar()
        return
      }
      setErro(mensagemDeErro(falha))
    } finally {
      enviando.current = false
    }
  }

  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo={`Enviar ${rotuloDoTipoDeDocumento(documento.tipo)}`}
      tamanho="media"
      rodape={
        <>
          <Botao variante="secundario" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao onClick={() => void enviar()} disabled={envio.isPending}>
            Enviar documento
          </Botao>
        </>
      }
    >
      <div className="flex flex-col gap-2">
        <SeletorDeArquivo
          arquivo={arquivo}
          aoMudar={(novo) => {
            setArquivo(novo)
            setErro(null)
          }}
          tiposAceitos={REGRAS_DE_ARQUIVO_DE_DOCUMENTO.tiposAceitos}
          tamanhoMaximoMb={REGRAS_DE_ARQUIVO_DE_DOCUMENTO.tamanhoMaximoMb}
          textoDeAjuda="PDF, JPG ou PNG, até 10 MB"
          erro={erro}
        />
        {envio.isPending && (
          <p role="status" className="text-pequeno text-texto-suave">
            Enviando…
          </p>
        )}
      </div>
    </Janela>
  )
}

export function ModalEnviarMeuDocumento({
  documento,
  aberta,
  aoFechar,
}: {
  documento: Documento
  aberta: boolean
  aoFechar: () => void
}) {
  if (!aberta) return null
  return <Conteudo documento={documento} aoFechar={aoFechar} />
}
