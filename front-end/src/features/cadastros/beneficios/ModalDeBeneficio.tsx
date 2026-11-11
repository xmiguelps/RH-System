import { useQueryClient } from '@tanstack/react-query'
import { Controller } from 'react-hook-form'
import { z } from 'zod'
import { $api } from '../../../shared/api/api'
import { ErroDeApi } from '../../../shared/api/erroDeApi'
import { mensagemDeErro } from '../../../shared/api/mensagemDeErro'
import type { Esquema } from '../../../shared/api/tipos'
import { erroNoCampo } from '../../../shared/form/erroNoCampo'
import { opcional } from '../../../shared/form/esquemas'
import { FormularioComErroDoServidor } from '../../../shared/form/FormularioComErroDoServidor'
import { AreaDeTexto } from '../../../shared/ui/AreaDeTexto'
import { Botao } from '../../../shared/ui/Botao'
import { CaixaDeSelecao } from '../../../shared/ui/CaixaDeSelecao'
import { Campo } from '../../../shared/ui/Campo'
import { Entrada } from '../../../shared/ui/Entrada'
import { Janela } from '../../../shared/ui/Janela'
import { useNotificacao } from '../../../shared/ui/useNotificacao'
import { invalidarCadastros } from '../invalidarCadastros'

type Beneficio = Esquema<'BeneficioCatalogoItem'>

const esquema = z.object({
  nome: z.string().trim().min(1, 'Informe o nome do benefício.').max(100),
  descricao: opcional(z.string().trim().max(255)),
  opcional: z.boolean(),
  aceitaDependentes: z.boolean(),
  aceitaTrajeto: z.boolean(),
})

const MARCAS = [
  {
    nome: 'opcional',
    rotulo: 'Opcional',
    apoio:
      "A própria pessoa pode pedir este benefício (ele aparece em 'Disponíveis para você'). Desmarcado, só o RH o concede.",
  },
  {
    nome: 'aceitaDependentes',
    rotulo: 'Aceita dependentes',
    apoio: 'O pedido deixa incluir dependentes, como no plano de saúde.',
  },
  {
    nome: 'aceitaTrajeto',
    rotulo: 'Pede o trajeto',
    apoio: 'O pedido pede o trajeto, como no vale-transporte.',
  },
] as const

export function ModalDeBeneficio({
  beneficio,
  aberta,
  aoFechar,
}: {
  beneficio?: Beneficio
  aberta: boolean
  aoFechar: () => void
}) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const criar = $api.useMutation('post', '/api/rh/beneficios')
  const editar = $api.useMutation('put', '/api/rh/beneficios/{beneficioId}')
  if (!aberta) return null

  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo={beneficio ? 'Editar benefício' : 'Novo benefício'}
      tamanho="media"
      rodape={
        <>
          <Botao variante="secundario" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao
            type="submit"
            form="form-beneficio"
            carregando={criar.isPending || editar.isPending}
          >
            Salvar benefício
          </Botao>
        </>
      }
    >
      <FormularioComErroDoServidor
        id="form-beneficio"
        esquema={esquema}
        valoresIniciais={{
          nome: beneficio?.nome ?? '',
          descricao: beneficio?.descricao ?? '',
          opcional: beneficio?.opcional ?? false,
          aceitaDependentes: beneficio?.aceitaDependentes ?? false,
          aceitaTrajeto: beneficio?.aceitaTrajeto ?? false,
        }}
        aoEnviar={async (valores) => {
          try {
            if (beneficio) {
              await editar.mutateAsync({
                params: { path: { beneficioId: beneficio.id } },
                body: valores,
              })
            } else {
              await criar.mutateAsync({ body: valores })
            }
          } catch (erro) {
            if (erro instanceof ErroDeApi && erro.status === 404) {
              notificacao.erro(mensagemDeErro(erro))
              await invalidarCadastros(queryClient, '/api/rh/beneficios')
              aoFechar()
              return
            }
            throw erroNoCampo(erro, { NOME_JA_CADASTRADO: 'nome' })
          }
          await invalidarCadastros(queryClient, '/api/rh/beneficios')
          aoFechar()
        }}
      >
        {({ register, control, formState }) => (
          <div className="flex flex-col gap-4">
            <Campo rotulo="Nome do benefício" erro={formState.errors.nome?.message}>
              <Entrada {...register('nome')} />
            </Campo>
            <Campo rotulo="Descrição" erro={formState.errors.descricao?.message}>
              <AreaDeTexto {...register('descricao')} limite={255} />
            </Campo>
            {MARCAS.map((marca) => (
              <Controller
                key={marca.nome}
                control={control}
                name={marca.nome}
                render={({ field }) => (
                  <CaixaDeSelecao
                    rotulo={marca.rotulo}
                    descricao={marca.apoio}
                    marcada={field.value}
                    aoMudar={field.onChange}
                  />
                )}
              />
            ))}
          </div>
        )}
      </FormularioComErroDoServidor>
    </Janela>
  )
}
