import { AlertDialog } from '@base-ui/react/alert-dialog'
import { Field } from '@base-ui/react/field'
import { useState, type ReactNode } from 'react'
import { plural } from '../formato/texto'
import { AvisoDeErro } from './AvisoDeErro'
import { Botao } from './Botao'
import { Entrada } from './Entrada'

type Comum = {
  aberta: boolean
  aoCancelar: () => void
  aoConfirmar: () => void | Promise<void>
  titulo: string
  confirmando?: boolean
  erro?: unknown
  rotuloDeCancelar?: string
}

type Variantes =
  | { variante?: 'simples'; descricao: ReactNode; rotuloDeConfirmar?: string; perigo?: boolean }
  | {
      variante: 'digitar'
      descricao: ReactNode
      textoParaDigitar: string
      rotuloDeConfirmar?: string
    }
  | {
      variante: 'pendentes'
      pendentes: number | null
      assunto: string
      consequencia?: string
      rotuloDeConfirmar?: string
    }

export type JanelaDeConfirmacaoProps = Comum & Variantes

function textoDePendentes(pendentes: number | null, assunto: string, consequencia?: string) {
  const inicio =
    pendentes === null
      ? `Ainda há pessoas que não concluíram ${assunto}.`
      : `${pendentes} ${plural(pendentes, 'pessoa ainda não concluiu', 'pessoas ainda não concluíram')} ${assunto}.`
  return [inicio, consequencia, 'Quer mesmo continuar?'].filter(Boolean).join(' ')
}

function Conteudo(props: JanelaDeConfirmacaoProps) {
  const { aoCancelar, aoConfirmar, titulo, confirmando = false, erro } = props
  const rotuloDeCancelar = props.rotuloDeCancelar ?? 'Cancelar'
  const [digitado, setDigitado] = useState('')

  let descricao: ReactNode
  let rotuloDeConfirmar: string
  let perigo = false
  let liberado = true
  if (props.variante === 'pendentes') {
    descricao = textoDePendentes(props.pendentes, props.assunto, props.consequencia)
    rotuloDeConfirmar = props.rotuloDeConfirmar ?? 'Continuar mesmo assim'
  } else if (props.variante === 'digitar') {
    descricao = props.descricao
    rotuloDeConfirmar = props.rotuloDeConfirmar ?? 'Confirmar'
    perigo = true
    liberado = digitado.trim().toLowerCase() === props.textoParaDigitar.trim().toLowerCase()
  } else {
    descricao = props.descricao
    rotuloDeConfirmar = props.rotuloDeConfirmar ?? 'Confirmar'
    perigo = props.perigo ?? false
  }

  return (
    <form
      onSubmit={(evento) => {
        evento.preventDefault()
        if (liberado && !confirmando) void aoConfirmar()
      }}
      className="flex flex-col"
    >
      <div className="flex flex-col gap-4 p-7">
        <AlertDialog.Title className="text-t2 font-bold text-texto">{titulo}</AlertDialog.Title>
        <AlertDialog.Description className="text-corpo text-texto-suave">
          {descricao}
        </AlertDialog.Description>
        {props.variante === 'digitar' && (
          <Field.Root className="flex flex-col gap-2">
            <Field.Label className="text-tabela font-semibold text-texto">
              {`Digite "${props.textoParaDigitar}" para confirmar`}
            </Field.Label>
            <Entrada
              value={digitado}
              autoComplete="off"
              onChange={(evento) => setDigitado(evento.target.value)}
            />
          </Field.Root>
        )}
        {erro !== undefined && erro !== null && <AvisoDeErro erro={erro} />}
      </div>
      <div className="flex justify-end gap-3 border-t border-divisor bg-superficie px-7 py-4">
        <Botao variante="secundario" disabled={confirmando} onClick={aoCancelar}>
          {rotuloDeCancelar}
        </Botao>
        <Botao
          type="submit"
          variante={perigo ? 'perigo' : 'primario'}
          disabled={confirmando || !liberado}
        >
          {rotuloDeConfirmar}
        </Botao>
      </div>
    </form>
  )
}

export function JanelaDeConfirmacao(props: JanelaDeConfirmacaoProps) {
  return (
    <AlertDialog.Root
      open={props.aberta}
      onOpenChange={(aberto) => {
        if (!aberto && !props.confirmando) props.aoCancelar()
      }}
    >
      <AlertDialog.Portal>
        <AlertDialog.Backdrop className="fixed inset-0 z-40 bg-scrim" />
        <AlertDialog.Popup className="fixed top-1/2 left-1/2 z-50 w-[560px] max-w-[calc(100vw-48px)] -translate-x-1/2 -translate-y-1/2 overflow-hidden rounded-janela bg-pagina text-texto">
          <Conteudo {...props} />
        </AlertDialog.Popup>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  )
}
