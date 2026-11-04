import { zodResolver } from '@hookform/resolvers/zod'
import { useRef, useState, type ReactNode } from 'react'
import {
  useForm,
  type DefaultValues,
  type FieldValues,
  type Path,
  type Resolver,
  type UseFormReturn,
} from 'react-hook-form'
import type { z } from 'zod'
import { ErroDeApi } from '../api/erroDeApi'
import { mensagemDeErro } from '../api/mensagemDeErro'
import { Aviso } from '../ui/Aviso'
import { aplicarErrosDoServidor } from './aplicarErrosDoServidor'

type Entrada<E extends z.ZodType> = z.input<E> & FieldValues
type Saida<E extends z.ZodType> = z.output<E> & FieldValues

export type FormularioComErroDoServidorProps<E extends z.ZodType> = {
  esquema: E
  valoresIniciais: z.input<E>
  aoEnviar: (valores: z.output<E>) => Promise<unknown> | unknown
  aoSucesso?: () => void
  children: (formulario: UseFormReturn<Entrada<E>, unknown, Saida<E>>) => ReactNode
  className?: string
  id?: string
}

export function FormularioComErroDoServidor<E extends z.ZodType>({
  esquema,
  valoresIniciais,
  aoEnviar,
  aoSucesso,
  children,
  className,
  id,
}: FormularioComErroDoServidorProps<E>) {
  const formulario = useForm<Entrada<E>, unknown, Saida<E>>({
    resolver: zodResolver(esquema as never) as unknown as Resolver<Entrada<E>, unknown, Saida<E>>,
    defaultValues: valoresIniciais as DefaultValues<Entrada<E>>,
  })
  const [mensagemGeral, setMensagemGeral] = useState<string | null>(null)
  // O react-hook-form não impede um segundo envio enquanto o primeiro está em andamento.
  const enviando = useRef(false)
  const campos = Object.keys(valoresIniciais as object)

  const tratarEnvio = async (valores: Saida<E>) => {
    if (enviando.current) return
    enviando.current = true
    setMensagemGeral(null)
    try {
      await aoEnviar(valores)
    } catch (erro) {
      if (erro instanceof ErroDeApi) {
        const resultado = aplicarErrosDoServidor(erro, {
          definirErro: (campo, mensagem) =>
            formulario.setError(campo as Path<Entrada<E>>, { type: 'server', message: mensagem }),
          camposDoFormulario: campos,
        })
        setMensagemGeral(resultado.mensagemGeral)
      } else {
        setMensagemGeral(mensagemDeErro(erro))
      }
      return
    } finally {
      enviando.current = false
    }
    aoSucesso?.()
  }

  return (
    <form
      id={id}
      noValidate
      onSubmit={(evento) => void formulario.handleSubmit(tratarEnvio)(evento)}
      className={className}
    >
      {mensagemGeral && (
        <div className="mb-4">
          <Aviso tom="erro">{mensagemGeral}</Aviso>
        </div>
      )}
      {children(formulario)}
    </form>
  )
}
