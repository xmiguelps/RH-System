import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { AreaDeTexto } from '../../shared/ui/AreaDeTexto'
import { Aviso } from '../../shared/ui/Aviso'
import { Botao } from '../../shared/ui/Botao'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { Janela } from '../../shared/ui/Janela'
import { OpcoesEmCartao } from '../../shared/ui/OpcoesEmCartao'
import { Selecao } from '../../shared/ui/Selecao'
import { SeletorDeArquivo } from '../../shared/ui/SeletorDeArquivo'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { adicionarDocumento } from './enviarDocumento'
import { OPCOES_DE_TIPO_DE_DOCUMENTO, REGRAS_DE_ARQUIVO_DE_DOCUMENTO } from './rotulos'

type Modo = Esquema<'DocumentoColaboradorModo'>
type Tipo = Esquema<'DocumentoColaboradorTipo'>
type Erros = { tipo?: string; arquivo?: string; descricao?: string; dataValidade?: string }

function Conteudo({
  funcionarioId,
  primeiroNome,
  aoFechar,
}: {
  funcionarioId: number
  primeiroNome: string
  aoFechar: () => void
}) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const [modo, setModo] = useState<Modo>('ANEXAR_AGORA')
  const [tipo, setTipo] = useState<Tipo | null>(null)
  const [dataValidade, setDataValidade] = useState('')
  const [descricao, setDescricao] = useState('')
  const [arquivo, setArquivo] = useState<File | null>(null)
  const [erros, setErros] = useState<Erros>({})
  const [erroGeral, setErroGeral] = useState<string | null>(null)
  const enviando = useRef(false)
  const envio = useMutation({
    mutationFn: (campos: Parameters<typeof adicionarDocumento>[1]) =>
      adicionarDocumento(funcionarioId, campos),
  })

  const salvar = async () => {
    if (enviando.current) return
    const novos: Erros = {}
    if (!tipo) novos.tipo = 'Escolha o tipo.'
    if (modo === 'ANEXAR_AGORA' && !arquivo) novos.arquivo = 'Escolha o arquivo.'
    setErros(novos)
    setErroGeral(null)
    if (!tipo || Object.keys(novos).length > 0) return
    enviando.current = true
    try {
      await envio.mutateAsync({
        modo,
        tipo,
        descricao: descricao.trim() || undefined,
        dataValidade: dataValidade || undefined,
        arquivo: modo === 'ANEXAR_AGORA' ? (arquivo ?? undefined) : undefined,
      })
      await invalidarRota(queryClient, '/api/rh/funcionarios')
      aoFechar()
    } catch (erro) {
      if (erro instanceof ErroDeApi && erro.status === 404) {
        notificacao.erro(mensagemDeErro(erro))
        aoFechar()
        await invalidarRota(queryClient, '/api/rh/funcionarios')
        return
      }
      if (erro instanceof ErroDeApi && Object.keys(erro.errors).length > 0) {
        const doServidor: Erros = {}
        for (const campo of ['tipo', 'arquivo', 'descricao', 'dataValidade'] as const) {
          const mensagens = erro.errors[campo]
          if (mensagens) doServidor[campo] = mensagens.join(' ')
        }
        setErros(doServidor)
        if (Object.keys(doServidor).length === 0) setErroGeral(mensagemDeErro(erro))
      } else {
        setErroGeral(mensagemDeErro(erro))
      }
    } finally {
      enviando.current = false
    }
  }

  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo={`Adicionar documento de ${primeiroNome}`}
      tamanho="media"
      rodape={
        <>
          <Botao variante="secundario" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao onClick={() => void salvar()} disabled={envio.isPending}>
            Salvar documento
          </Botao>
        </>
      }
    >
      <div className="flex flex-col gap-5">
        {erroGeral && <Aviso tom="erro">{erroGeral}</Aviso>}
        <OpcoesEmCartao
          rotulo="Como o documento vai chegar"
          opcoes={[
            { valor: 'ANEXAR_AGORA', titulo: 'Anexar agora', descricao: 'Você já tem o arquivo' },
            {
              valor: 'PEDIR_PARA_ENVIAR',
              titulo: 'Pedir para a pessoa enviar',
              descricao: 'Ela envia pelo portal',
            },
          ]}
          valor={modo}
          aoMudar={setModo}
        />
        {modo === 'PEDIR_PARA_ENVIAR' && (
          <p className="text-pequeno text-texto-suave">Ela vê o pedido em Meus documentos.</p>
        )}
        <div className="grid grid-cols-2 gap-4">
          <Campo rotulo="Tipo de documento" erro={erros.tipo}>
            <Selecao
              rotulo="Tipo de documento"
              placeholder="Escolha o tipo"
              opcoes={OPCOES_DE_TIPO_DE_DOCUMENTO}
              valor={tipo}
              aoMudar={setTipo}
              invalido={!!erros.tipo}
            />
          </Campo>
          <Campo rotulo="Validade (opcional)" erro={erros.dataValidade}>
            <Entrada
              type="date"
              lang="pt-BR"
              value={dataValidade}
              onChange={(evento) => setDataValidade(evento.target.value)}
            />
          </Campo>
        </div>
        <Campo
          rotulo="Descrição (opcional)"
          dica={tipo === 'OUTRO' ? 'Diga qual é o documento' : undefined}
          erro={erros.descricao}
        >
          <AreaDeTexto
            limite={255}
            value={descricao}
            placeholder="Ex.: comprovante de residência atualizado"
            onChange={(evento) => setDescricao(evento.target.value)}
          />
        </Campo>
        {modo === 'ANEXAR_AGORA' && (
          <div className="flex flex-col gap-2">
            <SeletorDeArquivo
              arquivo={arquivo}
              aoMudar={(novo) => {
                setArquivo(novo)
                setErros((atuais) => ({ ...atuais, arquivo: undefined }))
              }}
              tiposAceitos={REGRAS_DE_ARQUIVO_DE_DOCUMENTO.tiposAceitos}
              tamanhoMaximoMb={REGRAS_DE_ARQUIVO_DE_DOCUMENTO.tamanhoMaximoMb}
              textoDeAjuda="PDF, JPG ou PNG, até 10 MB"
              erro={erros.arquivo}
            />
            {envio.isPending && (
              <p role="status" className="text-pequeno text-texto-suave">
                Enviando…
              </p>
            )}
          </div>
        )}
        <p className="text-pequeno text-texto-suave">
          {`Só o RH e ${primeiroNome} conseguem ver os documentos da ficha.`}
        </p>
      </div>
    </Janela>
  )
}

export function ModalAdicionarDocumento({
  funcionarioId,
  primeiroNome,
  aberta,
  aoFechar,
}: {
  funcionarioId: number
  primeiroNome: string
  aberta: boolean
  aoFechar: () => void
}) {
  if (!aberta) return null
  return <Conteudo funcionarioId={funcionarioId} primeiroNome={primeiroNome} aoFechar={aoFechar} />
}
