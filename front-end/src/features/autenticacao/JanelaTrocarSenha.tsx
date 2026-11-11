import { $api } from '../../shared/api/api'
import { erroNoCampo } from '../../shared/form/erroNoCampo'
import { FormularioComErroDoServidor } from '../../shared/form/FormularioComErroDoServidor'
import { Botao } from '../../shared/ui/Botao'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { Janela } from '../../shared/ui/Janela'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { esquemaDeTrocaDeSenha } from './esquemasDeSenha'

export function JanelaTrocarSenha({ aberta, aoFechar }: { aberta: boolean; aoFechar: () => void }) {
  const notificacao = useNotificacao()
  const trocar = $api.useMutation('post', '/api/eu/senha')
  if (!aberta) return null
  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo="Trocar senha"
      tamanho="media"
      rodape={
        <>
          <Botao variante="secundario" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao type="submit" form="form-trocar-senha" carregando={trocar.isPending}>
            Trocar senha
          </Botao>
        </>
      }
    >
      <FormularioComErroDoServidor
        id="form-trocar-senha"
        esquema={esquemaDeTrocaDeSenha}
        valoresIniciais={{ senhaAtual: '', novaSenha: '', confirmacao: '' }}
        className="flex flex-col gap-4"
        aoEnviar={async ({ senhaAtual, novaSenha }) => {
          try {
            await trocar.mutateAsync({ body: { senhaAtual, novaSenha } })
          } catch (erro) {
            throw erroNoCampo(erro, { SENHA_ATUAL_INCORRETA: 'senhaAtual' })
          }
          notificacao.sucesso('Senha trocada. As suas outras sessões foram encerradas.')
          aoFechar()
        }}
      >
        {({ register, formState }) => (
          <>
            <Campo rotulo="Senha atual" erro={formState.errors.senhaAtual?.message}>
              <Entrada
                {...register('senhaAtual')}
                type="password"
                autoComplete="current-password"
              />
            </Campo>
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
          </>
        )}
      </FormularioComErroDoServidor>
    </Janela>
  )
}
