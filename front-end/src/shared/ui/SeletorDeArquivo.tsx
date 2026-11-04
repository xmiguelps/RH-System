import { ArrowUpFromLine, FileText } from 'lucide-react'
import { useId, useRef, useState, type DragEvent } from 'react'
import { validarArquivo } from '../arquivos/validarArquivo'
import { Botao } from './Botao'
import { cn } from './estilo'

export function SeletorDeArquivo({
  arquivo,
  aoMudar,
  tiposAceitos,
  tamanhoMaximoMb,
  textoDeAjuda,
  titulo = 'Arraste o arquivo aqui',
  erro,
}: {
  arquivo: File | null
  aoMudar: (arquivo: File | null) => void
  tiposAceitos: readonly string[]
  tamanhoMaximoMb: number
  textoDeAjuda?: string
  titulo?: string
  erro?: string | null
}) {
  const id = useId()
  const entrada = useRef<HTMLInputElement>(null)
  const [erroLocal, setErroLocal] = useState<string | null>(null)
  const [arrastando, setArrastando] = useState(false)

  const escolher = (escolhido: File | undefined) => {
    if (!escolhido) return
    const problema = validarArquivo(escolhido, { tiposAceitos, tamanhoMaximoMb })
    setErroLocal(problema)
    if (!problema) aoMudar(escolhido)
  }

  const aoSoltar = (evento: DragEvent<HTMLDivElement>) => {
    evento.preventDefault()
    setArrastando(false)
    escolher(evento.dataTransfer.files[0])
  }

  const mensagem = erro ?? erroLocal ?? null

  return (
    <div className="flex flex-col gap-2">
      <div
        onDragOver={(evento) => {
          evento.preventDefault()
          setArrastando(true)
        }}
        onDragLeave={() => setArrastando(false)}
        onDrop={aoSoltar}
        className={cn(
          'flex flex-col items-center gap-2 rounded-cartao border border-dashed border-tracejado bg-[#fafcfb] px-6 py-8 text-center',
          arrastando && 'border-primaria bg-superficie-destaque',
        )}
      >
        <ArrowUpFromLine
          size={24}
          strokeWidth={1.75}
          aria-hidden="true"
          className="text-primaria-media"
        />
        <p className="text-corpo font-semibold text-texto">{titulo}</p>
        {textoDeAjuda && <p className="text-pequeno text-texto-suave">{textoDeAjuda}</p>}
        <input
          ref={entrada}
          id={id}
          type="file"
          aria-label="Escolher arquivo"
          accept={tiposAceitos.join(',')}
          className="sr-only"
          onChange={(evento) => {
            escolher(evento.target.files?.[0])
            evento.target.value = ''
          }}
        />
        <Botao variante="secundario" tamanho="pequeno" onClick={() => entrada.current?.click()}>
          Escolher arquivo
        </Botao>
      </div>
      {arquivo && (
        <div className="flex items-center justify-between gap-3 rounded-campo border border-borda bg-superficie px-3 py-2">
          <span className="flex items-center gap-2 text-tabela text-texto">
            <FileText size={16} strokeWidth={1.75} aria-hidden="true" />
            {arquivo.name}
          </span>
          <Botao variante="texto" tamanho="pequeno" onClick={() => aoMudar(null)}>
            Remover arquivo
          </Botao>
        </div>
      )}
      {mensagem && (
        <p role="alert" className="text-pequeno text-erro-texto">
          {mensagem}
        </p>
      )}
    </div>
  )
}
