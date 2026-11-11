import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Controller, useWatch, type UseFormReturn } from 'react-hook-form'
import { z } from 'zod'
import { invalidarRota } from '../../shared/api/invalidar'
import { campoData, campoHora } from '../../shared/form/esquemas'
import { FormularioComErroDoServidor } from '../../shared/form/FormularioComErroDoServidor'
import { Botao } from '../../shared/ui/Botao'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { Janela } from '../../shared/ui/Janela'
import { OpcoesEmCartao } from '../../shared/ui/OpcoesEmCartao'
import { SeletorDeArquivo } from '../../shared/ui/SeletorDeArquivo'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { enviarAtestado, type CamposDoAtestado } from './enviarAtestado'

const TIPOS_DO_ATESTADO = ['application/pdf', 'image/jpeg', 'image/png'] as const

const esquema = z
  .object({
    modo: z.enum(['DIAS', 'HORAS']),
    dataInicio: z.string(),
    dataFim: z.string(),
    data: z.string(),
    horaInicio: z.string(),
    horaFim: z.string(),
    arquivo: z.custom<File | null>().refine((a) => a instanceof File, {
      message: 'Escolha o arquivo do atestado.',
    }),
  })
  .superRefine((v, ctx) => {
    if (v.modo === 'DIAS') {
      if (!campoData().safeParse(v.dataInicio).success)
        ctx.addIssue({ code: 'custom', path: ['dataInicio'], message: 'Informe a data de início.' })
      if (!campoData().safeParse(v.dataFim).success)
        ctx.addIssue({ code: 'custom', path: ['dataFim'], message: 'Informe a data de fim.' })
      else if (v.dataFim < v.dataInicio)
        ctx.addIssue({
          code: 'custom',
          path: ['dataFim'],
          message: 'O fim não pode ser antes do início.',
        })
      return
    }
    if (!campoData().safeParse(v.data).success)
      ctx.addIssue({ code: 'custom', path: ['data'], message: 'Informe a data.' })
    if (!campoHora().safeParse(v.horaInicio).success)
      ctx.addIssue({ code: 'custom', path: ['horaInicio'], message: 'Informe a hora de início.' })
    if (!campoHora().safeParse(v.horaFim).success)
      ctx.addIssue({ code: 'custom', path: ['horaFim'], message: 'Informe a hora de fim.' })
    else if (v.horaFim <= v.horaInicio)
      ctx.addIssue({
        code: 'custom',
        path: ['horaFim'],
        message: 'A hora de fim precisa ser depois da de início.',
      })
  })

type Valores = z.input<typeof esquema>

const INICIAIS: Valores = {
  modo: 'DIAS',
  dataInicio: '',
  dataFim: '',
  data: '',
  horaInicio: '',
  horaFim: '',
  arquivo: null,
}

function camposParaEnviar(v: z.output<typeof esquema>): CamposDoAtestado {
  const arquivo = v.arquivo as File
  if (v.modo === 'DIAS') return { dataInicio: v.dataInicio, dataFim: v.dataFim, arquivo }
  return {
    dataInicio: v.data,
    dataFim: v.data,
    horaInicio: v.horaInicio,
    horaFim: v.horaFim,
    arquivo,
  }
}

function CamposDoPeriodo({
  formulario,
}: {
  formulario: UseFormReturn<Valores, unknown, z.output<typeof esquema>>
}) {
  const { control, register, formState } = formulario
  const modo = useWatch({ control, name: 'modo' })
  const e = formState.errors
  return (
    <>
      <Controller
        control={control}
        name="modo"
        render={({ field }) => (
          <OpcoesEmCartao
            rotulo="Período do atestado"
            valor={field.value}
            aoMudar={field.onChange}
            opcoes={[
              { valor: 'DIAS', titulo: 'Dia(s) inteiro(s)' },
              {
                valor: 'HORAS',
                titulo: 'Algumas horas',
                descricao: 'Só esse período é compensado',
              },
            ]}
          />
        )}
      />
      {modo === 'DIAS' ? (
        <div className="grid grid-cols-2 gap-4">
          <Campo rotulo="De" erro={e.dataInicio?.message}>
            <Entrada type="date" lang="pt-BR" {...register('dataInicio')} />
          </Campo>
          <Campo rotulo="Até" erro={e.dataFim?.message}>
            <Entrada type="date" lang="pt-BR" {...register('dataFim')} />
          </Campo>
        </div>
      ) : (
        <div className="grid grid-cols-3 gap-4">
          <Campo rotulo="Data" erro={e.data?.message}>
            <Entrada type="date" lang="pt-BR" {...register('data')} />
          </Campo>
          <Campo rotulo="Das" erro={e.horaInicio?.message}>
            <Entrada type="time" {...register('horaInicio')} />
          </Campo>
          <Campo rotulo="Até a hora" erro={e.horaFim?.message}>
            <Entrada type="time" {...register('horaFim')} />
          </Campo>
        </div>
      )}
    </>
  )
}

/** "Enviar atestado" (sem print). Sem campo de diagnóstico; o arquivo vai inteiro para a API. */
export function ModalEnviarAtestado({ aoFechar }: { aoFechar: () => void }) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const envio = useMutation({ mutationFn: (campos: CamposDoAtestado) => enviarAtestado(campos) })

  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo="Enviar atestado"
      rodape={
        <>
          <Botao variante="secundario" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao type="submit" form="form-atestado" carregando={envio.isPending}>
            Enviar atestado
          </Botao>
        </>
      }
    >
      <FormularioComErroDoServidor
        id="form-atestado"
        esquema={esquema}
        valoresIniciais={INICIAIS}
        aoEnviar={async (valores) => {
          await envio.mutateAsync(camposParaEnviar(valores))
          notificacao.sucesso('Atestado enviado. O RH vai validar.')
          await invalidarRota(queryClient, '/api/eu/ponto')
          aoFechar()
        }}
      >
        {(formulario) => (
          <div className="flex flex-col gap-5">
            <CamposDoPeriodo formulario={formulario} />
            <Controller
              control={formulario.control}
              name="arquivo"
              render={({ field, fieldState }) => (
                <SeletorDeArquivo
                  arquivo={field.value ?? null}
                  aoMudar={field.onChange}
                  tiposAceitos={TIPOS_DO_ATESTADO}
                  tamanhoMaximoMb={5}
                  textoDeAjuda="PDF, JPG ou PNG, até 5 MB"
                  erro={fieldState.error?.message}
                />
              )}
            />
            <p className="text-pequeno text-texto-suave">
              O RH e o seu gestor veem o arquivo. O sistema não guarda diagnóstico.
            </p>
          </div>
        )}
      </FormularioComErroDoServidor>
    </Janela>
  )
}
