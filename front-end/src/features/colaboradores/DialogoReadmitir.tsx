import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { Controller } from 'react-hook-form'
import { z } from 'zod'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import type { Esquema } from '../../shared/api/tipos'
import { erroNoCampo } from '../../shared/form/erroNoCampo'
import { campoData } from '../../shared/form/esquemas'
import { FormularioComErroDoServidor } from '../../shared/form/FormularioComErroDoServidor'
import { formatarData } from '../../shared/formato/datas'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'
import { CaixaDoFormularioDeAdmissao } from '../formularios/CaixaDoFormularioDeAdmissao'
import { CaixaDoOnboarding } from '../treinamentos/CaixaDoOnboarding'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { Janela } from '../../shared/ui/Janela'
import { Selecao } from '../../shared/ui/Selecao'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { OPCOES_DE_TIPO_DE_CONTRATO } from './rotulos'
import { useOpcoesDeApoio } from './useOpcoesDeApoio'

type Ficha = Esquema<'FuncionarioFichaRh'>

function criarEsquema(minimo: string | null) {
  return z.object({
    dataAdmissao: campoData().refine((data) => !minimo || data >= minimo, {
      message: minimo
        ? `A data de admissão não pode ser anterior a ${formatarData(minimo)}, o último dia do vínculo anterior.`
        : 'Data inválida.',
    }),
    tipoContrato: z.enum(['CLT', 'ESTAGIO']),
    cargoId: z.string().min(1, 'Escolha o cargo.').transform(Number),
    setorId: z.string().min(1, 'Escolha o setor.').transform(Number),
    enviarConvite: z.boolean(),
  })
}

export function DialogoReadmitir({
  ficha,
  aberto,
  aoFechar,
  aoReadmitir,
}: {
  ficha: Ficha
  aberto: boolean
  aoFechar: () => void
  aoReadmitir: (readmitida: Ficha) => void
}) {
  const opcoes = useOpcoesDeApoio()
  const queryClient = useQueryClient()
  const [formularioAdmissaoId, setFormularioAdmissaoId] = useState<number | null>(null)
  const [incluirNoOnboarding, setIncluirNoOnboarding] = useState(false)
  const notificacao = useNotificacao()
  const readmitir = $api.useMutation('post', '/api/rh/funcionarios/{funcionarioId}/readmissao')
  const minimo = ficha.desligamento?.ultimoDiaDeTrabalho ?? null
  const podeConvidar = ficha.acesso.situacao !== 'SENHA_DEFINIDA'

  return (
    <Janela
      aberta={aberto}
      aoFechar={aoFechar}
      titulo={`Readmitir ${ficha.nome}`}
      tamanho="media"
      rodape={
        <>
          <Botao variante="secundario" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao type="submit" form="form-readmitir" carregando={readmitir.isPending}>
            Readmitir
          </Botao>
        </>
      }
    >
      <div className="flex flex-col gap-5">
        <p className="text-corpo text-texto-suave">
          A pessoa volta com o mesmo cadastro, o mesmo CPF e a mesma matrícula, e o histórico
          continua na ficha. Os benefícios e as férias futuras cancelados na saída{' '}
          <strong>não voltam</strong>: dê os benefícios de novo em Editar dados. O acesso ao portal
          precisa ser liberado de novo.
        </p>
        {!!opcoes.erro && <AvisoDeErro erro={opcoes.erro} aoTentarDeNovo={opcoes.tentarDeNovo} />}
        <FormularioComErroDoServidor
          id="form-readmitir"
          esquema={criarEsquema(minimo)}
          valoresIniciais={{
            dataAdmissao: '',
            tipoContrato: ficha.tipoContrato ?? 'CLT',
            cargoId: ficha.cargo ? String(ficha.cargo.id) : '',
            setorId: ficha.setor ? String(ficha.setor.id) : '',
            enviarConvite: false,
          }}
          aoEnviar={async (valores) => {
            let readmitida: Ficha
            try {
              readmitida = await readmitir.mutateAsync({
                params: { path: { funcionarioId: ficha.id } },
                body: {
                  dataAdmissao: valores.dataAdmissao,
                  cargoId: valores.cargoId,
                  setorId: valores.setorId,
                  tipoContrato: valores.tipoContrato,
                  incluirNoOnboarding,
                  formularioAdmissaoId,
                  enviarConvite: podeConvidar ? valores.enviarConvite : false,
                },
              })
            } catch (erro) {
              if (erro instanceof ErroDeApi && erro.status === 409) {
                await invalidarRota(queryClient, '/api/rh/funcionarios')
              }
              throw erroNoCampo(erro, { GESTOR_DO_SETOR_INATIVO: 'setorId' })
            }
            await invalidarRota(queryClient, '/api/rh/funcionarios')
            notificacao.sucesso(`${ficha.nome} foi readmitido.`)
            aoReadmitir(readmitida)
          }}
        >
          {({ register, control, formState }) => (
            <div className="flex flex-col gap-4">
              <Campo rotulo="Data de admissão" erro={formState.errors.dataAdmissao?.message}>
                <Entrada
                  {...register('dataAdmissao')}
                  type="date"
                  lang="pt-BR"
                  min={minimo ?? undefined}
                />
              </Campo>
              <Controller
                control={control}
                name="tipoContrato"
                render={({ field }) => (
                  <Campo rotulo="Tipo de contrato">
                    <Selecao
                      rotulo="Tipo de contrato"
                      opcoes={OPCOES_DE_TIPO_DE_CONTRATO}
                      valor={field.value}
                      aoMudar={(v) => field.onChange(v ?? 'CLT')}
                    />
                  </Campo>
                )}
              />
              <Controller
                control={control}
                name="cargoId"
                render={({ field, fieldState }) => (
                  <Campo rotulo="Cargo" erro={fieldState.error?.message}>
                    <Selecao
                      rotulo="Cargo"
                      placeholder="Escolha o cargo"
                      opcoes={opcoes.cargos}
                      valor={field.value || null}
                      aoMudar={(v) => field.onChange(v ?? '')}
                      carregando={opcoes.carregando}
                    />
                  </Campo>
                )}
              />
              <Controller
                control={control}
                name="setorId"
                render={({ field, fieldState }) => (
                  <Campo rotulo="Setor" erro={fieldState.error?.message}>
                    <Selecao
                      rotulo="Setor"
                      placeholder="Escolha o setor"
                      opcoes={opcoes.setores}
                      valor={field.value || null}
                      aoMudar={(v) => field.onChange(v ?? '')}
                      carregando={opcoes.carregando}
                    />
                  </Campo>
                )}
              />
              <div className="flex flex-col gap-3 rounded-cartao bg-superficie-suave p-4">
                <CaixaDoOnboarding marcada={incluirNoOnboarding} aoMudar={setIncluirNoOnboarding} />
                <CaixaDoFormularioDeAdmissao
                  valor={formularioAdmissaoId}
                  aoMudar={setFormularioAdmissaoId}
                />
                {podeConvidar && (
                  <Controller
                    control={control}
                    name="enviarConvite"
                    render={({ field }) => (
                      <CaixaDeSelecao
                        rotulo="Enviar convite de acesso ao portal"
                        marcada={field.value}
                        aoMudar={field.onChange}
                      />
                    )}
                  />
                )}
              </div>
            </div>
          )}
        </FormularioComErroDoServidor>
      </div>
    </Janela>
  )
}
