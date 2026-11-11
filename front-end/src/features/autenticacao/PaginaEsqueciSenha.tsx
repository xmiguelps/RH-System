import { useState } from 'react'
import { useLocation } from 'react-router'
import { z } from 'zod'
import { $apiPublica } from '../../shared/api/apiPublica'
import { campoEmail } from '../../shared/form/esquemas'
import { FormularioComErroDoServidor } from '../../shared/form/FormularioComErroDoServidor'
import { Aviso } from '../../shared/ui/Aviso'
import { Botao } from '../../shared/ui/Botao'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { CartaoPublico } from './CartaoPublico'
import { mensagemDeErroDeEntrada } from './mensagemDeErroDeEntrada'
import { ErroDeApi } from '../../shared/api/erroDeApi'

const esquema = z.object({ email: campoEmail().pipe(z.string().max(150)) })

export function PaginaEsqueciSenha() {
  const estado = useLocation().state as { email?: string } | null
  const [enviado, setEnviado] = useState(false)
  const pedir = $apiPublica.useMutation('post', '/api/auth/senha/esqueci')

  return (
    <CartaoPublico titulo="Esqueci minha senha">
      {enviado ? (
        <div className="flex flex-col gap-4">
          <Aviso tom="sucesso">
            Se o e-mail estiver cadastrado, enviaremos um link para definir uma nova senha.
          </Aviso>
          <BotaoLink to="/entrar" variante="texto">
            Voltar para entrar
          </BotaoLink>
        </div>
      ) : (
        <FormularioComErroDoServidor
          esquema={esquema}
          valoresIniciais={{ email: estado?.email ?? '' }}
          aoEnviar={async ({ email }) => {
            try {
              await pedir.mutateAsync({ body: { email } })
            } catch (erro) {
              // 400 vai para o campo; o resto usa os textos da tela de entrar.
              if (erro instanceof ErroDeApi && erro.status === 400) throw erro
              throw new ErroDeApi(erro instanceof ErroDeApi ? erro.status : 0, 'Erro.', {
                detail: mensagemDeErroDeEntrada(erro),
              })
            }
            setEnviado(true)
          }}
          className="flex flex-col gap-4"
        >
          {({ register, formState }) => (
            <>
              <p className="text-corpo text-texto-suave">
                Digite o e-mail do seu acesso. Se ele estiver cadastrado, enviaremos um link para
                definir uma nova senha. O link vale 1 hora.
              </p>
              <Campo rotulo="E-mail" erro={formState.errors.email?.message}>
                <Entrada {...register('email')} type="email" autoComplete="username" />
              </Campo>
              <Botao type="submit" larguraTotal carregando={pedir.isPending}>
                Enviar link
              </Botao>
              <BotaoLink to="/entrar" variante="texto" className="self-center">
                Voltar para entrar
              </BotaoLink>
            </>
          )}
        </FormularioComErroDoServidor>
      )}
    </CartaoPublico>
  )
}
