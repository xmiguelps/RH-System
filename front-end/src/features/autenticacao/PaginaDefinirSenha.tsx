import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { $apiPublica } from '../../shared/api/apiPublica'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import { FormularioComErroDoServidor } from '../../shared/form/FormularioComErroDoServidor'
import { formatarDataHora } from '../../shared/formato/datas'
import { Aviso } from '../../shared/ui/Aviso'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { CartaoPublico } from './CartaoPublico'
import { esquemaDeNovaSenha } from './esquemasDeSenha'
import { useValidacaoDoLink } from './useValidacaoDoLink'

const LINK_INVALIDO = 'Este link não vale mais. Peça um novo ao RH ou use "Esqueci minha senha".'

function ehLinkInvalido(erro: unknown): boolean {
  return (
    erro instanceof ErroDeApi &&
    (erro.codigo === 'LINK_INVALIDO_OU_VENCIDO' || (erro.status === 400 && !!erro.errors.token))
  )
}

function LinkInvalido({ detalhe }: { detalhe: string }) {
  return (
    <CartaoPublico titulo="Link inválido">
      <div className="flex flex-col gap-4">
        <Aviso tom="erro">{detalhe}</Aviso>
        <BotaoLink to="/esqueci-senha" larguraTotal>
          Esqueci minha senha
        </BotaoLink>
        <BotaoLink to="/entrar" variante="texto" className="self-center">
          Voltar para entrar
        </BotaoLink>
      </div>
    </CartaoPublico>
  )
}

export function PaginaDefinirSenha() {
  const [parametros] = useSearchParams()
  // O token só fica no endereço e no corpo da chamada: nunca em estado guardado nem em log.
  const token = parametros.get('token') || null
  const validacao = useValidacaoDoLink(token)
  const definir = $apiPublica.useMutation('post', '/api/auth/senha/definir')
  const [invalidoAoSalvar, setInvalidoAoSalvar] = useState<string | null>(null)
  const [concluido, setConcluido] = useState(false)

  if (!token) return <LinkInvalido detalhe={LINK_INVALIDO} />
  if (invalidoAoSalvar) return <LinkInvalido detalhe={invalidoAoSalvar} />
  if (validacao.isLoading) {
    return (
      <CartaoPublico titulo="Definir senha">
        <p role="status" className="text-corpo text-texto-suave">
          Conferindo o link…
        </p>
      </CartaoPublico>
    )
  }
  if (validacao.error) {
    if (ehLinkInvalido(validacao.error)) {
      const erro = validacao.error as ErroDeApi
      return <LinkInvalido detalhe={erro.detail ?? LINK_INVALIDO} />
    }
    return (
      <CartaoPublico titulo="Definir senha">
        <AvisoDeErro erro={validacao.error} aoTentarDeNovo={() => void validacao.refetch()} />
      </CartaoPublico>
    )
  }
  const link = validacao.data
  if (!link) return null
  const convite = link.tipo === 'CONVITE'

  if (concluido) {
    return (
      <CartaoPublico titulo={convite ? 'Senha definida' : 'Senha alterada'}>
        <div className="flex flex-col gap-4">
          <Aviso tom="sucesso">
            {convite
              ? 'Senha definida. Agora você já pode entrar.'
              : 'Senha alterada. Entre com a nova senha.'}
          </Aviso>
          <BotaoLink to="/entrar" larguraTotal>
            Ir para entrar
          </BotaoLink>
        </div>
      </CartaoPublico>
    )
  }

  return (
    <CartaoPublico titulo={convite ? 'Bem-vindo, defina a sua senha' : 'Defina uma nova senha'}>
      <FormularioComErroDoServidor
        esquema={esquemaDeNovaSenha}
        valoresIniciais={{ novaSenha: '', confirmacao: '' }}
        className="flex flex-col gap-4"
        aoEnviar={async ({ novaSenha }) => {
          try {
            await definir.mutateAsync({ body: { token, novaSenha } })
          } catch (erro) {
            if (ehLinkInvalido(erro)) {
              setInvalidoAoSalvar((erro as ErroDeApi).detail ?? LINK_INVALIDO)
              return
            }
            if (erro instanceof ErroDeApi && erro.status === 429) {
              throw new ErroDeApi(429, erro.title, { detail: mensagemDeErro(erro) })
            }
            throw erro
          }
          setConcluido(true)
        }}
      >
        {({ register, formState }) => (
          <>
            <p className="text-corpo text-texto-suave">
              {convite
                ? `Olá, ${link.nome}. Esta será a senha do acesso ${link.emailCorporativo}.`
                : `Acesso ${link.emailCorporativo}.`}
            </p>
            <Campo
              rotulo="Nova senha"
              dica="De 8 a 128 caracteres."
              erro={formState.errors.novaSenha?.message}
            >
              <Entrada {...register('novaSenha')} type="password" autoComplete="new-password" />
            </Campo>
            <Campo rotulo="Repita a nova senha" erro={formState.errors.confirmacao?.message}>
              <Entrada {...register('confirmacao')} type="password" autoComplete="new-password" />
            </Campo>
            <Botao type="submit" larguraTotal carregando={definir.isPending}>
              Salvar senha
            </Botao>
            <p className="text-pequeno text-texto-suave">
              {`O link vale até ${formatarDataHora(link.expiraEmUtc)}.`}
            </p>
          </>
        )}
      </FormularioComErroDoServidor>
    </CartaoPublico>
  )
}
