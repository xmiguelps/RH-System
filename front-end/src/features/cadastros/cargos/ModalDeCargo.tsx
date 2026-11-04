import { useQueryClient } from '@tanstack/react-query'
import { z } from 'zod'
import { $api } from '../../../shared/api/api'
import { ErroDeApi } from '../../../shared/api/erroDeApi'
import { invalidarRota } from '../../../shared/api/invalidar'
import { mensagemDeErro } from '../../../shared/api/mensagemDeErro'
import type { Esquema } from '../../../shared/api/tipos'
import { erroNoCampo } from '../../../shared/form/erroNoCampo'
import { opcional } from '../../../shared/form/esquemas'
import { FormularioComErroDoServidor } from '../../../shared/form/FormularioComErroDoServidor'
import { AreaDeTexto } from '../../../shared/ui/AreaDeTexto'
import { Botao } from '../../../shared/ui/Botao'
import { Campo } from '../../../shared/ui/Campo'
import { Entrada } from '../../../shared/ui/Entrada'
import { Janela } from '../../../shared/ui/Janela'
import { useNotificacao } from '../../../shared/ui/useNotificacao'

type Cargo = Esquema<'CargoItem'>

const esquemaDoCargo = z.object({
  nome: z.string().trim().min(1, 'Informe o nome do cargo.').max(100),
  cargaSemanalHoras: z
    .string()
    .trim()
    .min(1, 'Informe a carga horária.')
    .refine((texto) => /^\d+([.,]\d{1,2})?$/.test(texto), {
      message: 'Use um número com até duas casas decimais.',
    })
    .transform((texto) => Number(texto.replace(',', '.')))
    .refine((horas) => horas > 0 && horas <= 168, {
      message: 'A carga semanal precisa ser maior que zero e no máximo 168 horas.',
    }),
  descricao: opcional(z.string().trim().max(255)),
})

export function ModalDeCargo({
  cargo,
  aberta,
  aoFechar,
}: {
  cargo?: Cargo
  aberta: boolean
  aoFechar: () => void
}) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const criar = $api.useMutation('post', '/api/rh/cargos')
  const editar = $api.useMutation('put', '/api/rh/cargos/{cargoId}')
  const editando = !!cargo

  if (!aberta) return null

  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo={editando ? 'Editar cargo' : 'Novo cargo'}
      tamanho="media"
      rodape={
        <>
          <Botao variante="secundario" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao type="submit" form="form-cargo" carregando={criar.isPending || editar.isPending}>
            Salvar cargo
          </Botao>
        </>
      }
    >
      <FormularioComErroDoServidor
        id="form-cargo"
        esquema={esquemaDoCargo}
        valoresIniciais={{
          nome: cargo?.nome ?? '',
          cargaSemanalHoras: cargo ? String(cargo.cargaSemanalHoras) : '',
          descricao: cargo?.descricao ?? '',
        }}
        aoEnviar={async (valores) => {
          const corpo = {
            nome: valores.nome,
            descricao: valores.descricao,
            cargaSemanalHoras: valores.cargaSemanalHoras,
          }
          try {
            if (cargo) {
              await editar.mutateAsync({ params: { path: { cargoId: cargo.id } }, body: corpo })
            } else {
              await criar.mutateAsync({ body: corpo })
            }
          } catch (erro) {
            if (erro instanceof ErroDeApi && erro.status === 404) {
              notificacao.erro(mensagemDeErro(erro))
              await invalidarRota(queryClient, '/api/rh/cargos')
              aoFechar()
              return
            }
            throw erroNoCampo(erro, { NOME_JA_CADASTRADO: 'nome' })
          }
          await invalidarRota(queryClient, '/api/rh/cargos')
          aoFechar()
        }}
      >
        {({ register, formState }) => (
          <div className="flex flex-col gap-4">
            <Campo rotulo="Nome do cargo" erro={formState.errors.nome?.message}>
              <Entrada {...register('nome')} placeholder="Ex.: analista de compras" />
            </Campo>
            <Campo
              rotulo="Carga horária semanal"
              erro={formState.errors.cargaSemanalHoras?.message}
            >
              <div className="flex items-center gap-3">
                <Entrada
                  {...register('cargaSemanalHoras')}
                  type="number"
                  step="any"
                  placeholder="44"
                  className="w-32"
                />
                <span className="text-corpo text-texto-suave">horas</span>
              </div>
            </Campo>
            <Campo rotulo="Descrição" erro={formState.errors.descricao?.message}>
              <AreaDeTexto
                {...register('descricao')}
                limite={255}
                placeholder="O que a pessoa faz no dia a dia"
              />
            </Campo>
            <p className="text-pequeno text-texto-suave">
              A carga horária vale para todas as pessoas do cargo.
            </p>
            {editando && (
              <p className="text-pequeno text-texto-suave">
                A nova carga vale para os dias de ponto criados depois; os que já foram gravados não
                mudam.
              </p>
            )}
          </div>
        )}
      </FormularioComErroDoServidor>
    </Janela>
  )
}
