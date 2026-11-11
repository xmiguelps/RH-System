import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate } from 'react-router'
import { z } from 'zod'
import { CartaoPublico } from './CartaoPublico'
import { MENSAGEM_CAMPOS_VAZIOS, mensagemDeErroDeEntrada } from './mensagemDeErroDeEntrada'
import { useAuth } from './useAuth'

const esquemaDeEntrada = z.object({
  email: z.string().trim().min(1, MENSAGEM_CAMPOS_VAZIOS),
  // Sem trim: espaço no começo ou no fim faz parte da senha.
  senha: z.string().min(1, MENSAGEM_CAMPOS_VAZIOS),
})

type DadosDeEntrada = z.infer<typeof esquemaDeEntrada>

export function PaginaEntrar() {
  const { entrar } = useAuth()
  const navegar = useNavigate()
  const [erroDoServidor, setErroDoServidor] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    getValues,
    formState: { errors, isSubmitting },
  } = useForm<DadosDeEntrada>({
    resolver: zodResolver(esquemaDeEntrada),
    defaultValues: { email: '', senha: '' },
  })

  const enviar = handleSubmit(async (dados) => {
    setErroDoServidor(null)
    try {
      await entrar(dados.email, dados.senha)
    } catch (erro) {
      setErroDoServidor(mensagemDeErroDeEntrada(erro))
    }
  })

  const mensagem =
    errors.email || errors.senha ? MENSAGEM_CAMPOS_VAZIOS : (erroDoServidor ?? undefined)

  return (
    <CartaoPublico titulo="RH-System">
      <form noValidate onSubmit={enviar} className="flex flex-col gap-4">
        <div className="flex flex-col gap-2">
          <label htmlFor="entrar-email" className="text-tabela font-semibold text-texto">
            E-mail
          </label>
          <input
            id="entrar-email"
            type="email"
            autoComplete="username"
            className="h-11 rounded-campo border border-borda-campo bg-superficie px-[13px] text-corpo text-texto focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primaria-media"
            {...register('email')}
          />
        </div>
        <div className="flex flex-col gap-2">
          <label htmlFor="entrar-senha" className="text-tabela font-semibold text-texto">
            Senha
          </label>
          <input
            id="entrar-senha"
            type="password"
            autoComplete="current-password"
            className="h-11 rounded-campo border border-borda-campo bg-superficie px-[13px] text-corpo text-texto focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primaria-media"
            {...register('senha')}
          />
        </div>
        {mensagem && (
          <p
            role="alert"
            className="rounded-campo bg-erro-fundo px-3 py-2 text-pequeno text-erro-texto"
          >
            {mensagem}
          </p>
        )}
        <button
          type="submit"
          disabled={isSubmitting}
          className="h-11 rounded-botao bg-primaria text-corpo font-semibold text-pagina hover:bg-primaria-ativa disabled:cursor-not-allowed disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primaria-media"
        >
          Entrar
        </button>
        <Link
          to="/esqueci-senha"
          onClick={(evento) => {
            evento.preventDefault()
            navegar('/esqueci-senha', { state: { email: getValues('email') } })
          }}
          className="self-center text-pequeno font-semibold text-primaria-media underline-offset-2 hover:underline"
        >
          Esqueci minha senha
        </Link>
      </form>
    </CartaoPublico>
  )
}
