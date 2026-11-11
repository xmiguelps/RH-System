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
import { Aviso } from '../../../shared/ui/Aviso'
import { Botao } from '../../../shared/ui/Botao'
import { Campo } from '../../../shared/ui/Campo'
import { ComboDeBusca } from '../../../shared/ui/ComboDeBusca'
import { Entrada } from '../../../shared/ui/Entrada'
import { Janela } from '../../../shared/ui/Janela'
import { useNotificacao } from '../../../shared/ui/useNotificacao'
import { SEM_GESTOR, useBuscaDePessoas } from '../../colaboradores/useBuscaDePessoas'
import { invalidarCadastros } from '../invalidarCadastros'

type Setor = Esquema<'SetorItem'>

const esquemaDoSetor = z.object({
  nome: z.string().trim().min(1, 'Informe o nome do setor.').max(100),
  descricao: opcional(z.string().trim().max(255)),
  gestorFuncionarioId: z.string(),
})

const CAMPO_POR_CODIGO = {
  NOME_JA_CADASTRADO: 'nome',
  GESTOR_JA_RESPONDE_POR_OUTRO_SETOR: 'gestorFuncionarioId',
  GESTOR_PRECISA_TER_PERFIL_GESTOR: 'gestorFuncionarioId',
  FUNCIONARIO_INATIVO: 'gestorFuncionarioId',
} as const

export function ModalDeSetor({
  setor,
  aberta,
  aoFechar,
}: {
  setor?: Setor
  aberta: boolean
  aoFechar: () => void
}) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const criar = $api.useMutation('post', '/api/rh/setores')
  const editar = $api.useMutation('put', '/api/rh/setores/{setorId}')
  const busca = useBuscaDePessoas(
    setor?.gestor
      ? { funcionarioId: setor.gestor.funcionarioId, nome: setor.gestor.nome }
      : undefined,
  )
  const gestorOriginal = setor?.gestor ? String(setor.gestor.funcionarioId) : SEM_GESTOR

  if (!aberta) return null

  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo={setor ? 'Editar setor' : 'Novo setor'}
      tamanho="media"
      rodape={
        <>
          <Botao variante="secundario" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao type="submit" form="form-setor" carregando={criar.isPending || editar.isPending}>
            Salvar setor
          </Botao>
        </>
      }
    >
      <FormularioComErroDoServidor
        id="form-setor"
        esquema={esquemaDoSetor}
        valoresIniciais={{
          nome: setor?.nome ?? '',
          descricao: setor?.descricao ?? '',
          gestorFuncionarioId: gestorOriginal,
        }}
        aoEnviar={async (valores) => {
          const corpo = {
            nome: valores.nome,
            descricao: valores.descricao,
            gestorFuncionarioId:
              valores.gestorFuncionarioId === SEM_GESTOR
                ? null
                : Number(valores.gestorFuncionarioId),
          }
          try {
            if (setor) {
              await editar.mutateAsync({ params: { path: { setorId: setor.id } }, body: corpo })
            } else {
              await criar.mutateAsync({ body: corpo })
            }
          } catch (erro) {
            if (erro instanceof ErroDeApi && erro.status === 404) {
              notificacao.erro(mensagemDeErro(erro))
              await invalidarCadastros(queryClient, '/api/rh/setores')
              aoFechar()
              return
            }
            throw erroNoCampo(erro, CAMPO_POR_CODIGO)
          }
          await invalidarCadastros(
            queryClient,
            '/api/rh/setores',
            '/api/rh/gestores',
            '/api/rh/funcionarios',
          )
          aoFechar()
        }}
      >
        {({ register, control, formState, watch }) => {
          const gestor = watch('gestorFuncionarioId')
          return (
            <div className="flex flex-col gap-4">
              <Campo rotulo="Nome do setor" erro={formState.errors.nome?.message}>
                <Entrada {...register('nome')} />
              </Campo>
              <Campo rotulo="Descrição" erro={formState.errors.descricao?.message}>
                <AreaDeTexto {...register('descricao')} limite={255} />
              </Campo>
              <Controller
                control={control}
                name="gestorFuncionarioId"
                render={({ field, fieldState }) => (
                  <Campo rotulo="Gestor" erro={fieldState.error?.message}>
                    <ComboDeBusca
                      rotulo="Gestor"
                      placeholder="Busque pelo nome"
                      opcoes={busca.opcoes}
                      valor={field.value || null}
                      aoMudar={(valor) => field.onChange(valor ?? '')}
                      aoDigitar={busca.aoDigitar}
                      carregando={busca.carregando}
                    />
                  </Campo>
                )}
              />
              <p className="text-pequeno text-texto-suave">
                A pessoa escolhida passa a ter o nível Gestor. Quem deixa de ser gestor deste setor
                volta a Colaborador e continua no setor.
              </p>
              {setor?.gestor && gestor !== gestorOriginal && (
                <Aviso tom="alerta">
                  Os pedidos de ponto e de férias que esperavam pelo gestor atual passam para o RH.
                </Aviso>
              )}
            </div>
          )
        }}
      </FormularioComErroDoServidor>
    </Janela>
  )
}
