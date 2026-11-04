import { useQueryClient } from '@tanstack/react-query'
import { Controller, useFieldArray, type UseFormReturn } from 'react-hook-form'
import { z } from 'zod'
import { $api } from '../../shared/api/api'
import { invalidarRota } from '../../shared/api/invalidar'
import type { Esquema } from '../../shared/api/tipos'
import { campoHora } from '../../shared/form/esquemas'
import { FormularioComErroDoServidor } from '../../shared/form/FormularioComErroDoServidor'
import {
  dataDoInstante,
  formatarDiaMesDaData,
  formatarHora,
  instanteUtcDaHoraLocal,
} from '../../shared/formato/datas'
import { Botao } from '../../shared/ui/Botao'
import { Campo } from '../../shared/ui/Campo'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'
import { Entrada } from '../../shared/ui/Entrada'
import { Janela } from '../../shared/ui/Janela'
import { Selecao } from '../../shared/ui/Selecao'
import { useNotificacao } from '../../shared/ui/useNotificacao'

type Dia = Esquema<'PontoEspelhoDia'>

const TIPOS = [
  { valor: 'ENTRADA', rotulo: 'Entrada' },
  { valor: 'SAIDA', rotulo: 'Saída' },
] as const

const esquema = z.object({
  marcacoes: z
    .array(
      z.object({
        id: z.number().nullable(),
        tipo: z.enum(['ENTRADA', 'SAIDA']),
        hora: campoHora(),
        diaSeguinte: z.boolean(),
      }),
    )
    .max(20, 'No máximo 20 batidas por dia.'),
})

type Valores = z.input<typeof esquema>

function valoresDoDia(dia: Dia): Valores {
  const marcacoes = dia.periodos
    .flatMap((p) => [p.entrada, p.saida])
    .filter((m): m is NonNullable<typeof m> => m !== null)
    .sort((a, b) => a.ordem - b.ordem)
  return {
    marcacoes: marcacoes.map((m) => ({
      id: m.id,
      tipo: m.tipo,
      hora: formatarHora(m.instanteUtc),
      diaSeguinte: dataDoInstante(m.instanteUtc) !== dia.data,
    })),
  }
}

function Linhas({
  formulario,
}: {
  formulario: UseFormReturn<Valores, unknown, z.output<typeof esquema>>
}) {
  const { control, register, formState } = formulario
  const lista = useFieldArray({ control, name: 'marcacoes' })
  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="mb-2 text-tabela font-semibold text-texto">Batidas do dia</legend>
      {lista.fields.length === 0 && (
        <p className="text-corpo text-texto-suave">Nenhuma batida neste dia.</p>
      )}
      {lista.fields.map((campo, i) => (
        <div key={campo.id} className="flex flex-wrap items-end gap-3">
          <div className="w-40">
            <Controller
              control={control}
              name={`marcacoes.${i}.tipo`}
              render={({ field }) => (
                <Campo rotulo={`Tipo da batida ${i + 1}`}>
                  <Selecao
                    rotulo={`Tipo da batida ${i + 1}`}
                    opcoes={TIPOS}
                    valor={field.value}
                    aoMudar={(v) => field.onChange(v ?? 'ENTRADA')}
                  />
                </Campo>
              )}
            />
          </div>
          <div className="w-32">
            <Campo
              rotulo={`Hora da batida ${i + 1}`}
              erro={formState.errors.marcacoes?.[i]?.hora?.message}
            >
              <Entrada type="time" {...register(`marcacoes.${i}.hora`)} />
            </Campo>
          </div>
          <Controller
            control={control}
            name={`marcacoes.${i}.diaSeguinte`}
            render={({ field }) => (
              <CaixaDeSelecao
                rotulo={`No dia seguinte (${i + 1})`}
                marcada={field.value}
                aoMudar={field.onChange}
              />
            )}
          />
          <Botao
            variante="texto"
            tamanho="pequeno"
            onClick={() => lista.remove(i)}
            aria-label={`Remover a batida ${i + 1}`}
          >
            Remover
          </Botao>
        </div>
      ))}
      <div>
        <Botao
          variante="secundario"
          tamanho="pequeno"
          disabled={lista.fields.length >= 20}
          onClick={() => lista.append({ id: null, tipo: 'ENTRADA', hora: '', diaSeguinte: false })}
        >
          Incluir batida
        </Botao>
      </div>
    </fieldset>
  )
}

/** "Editar" do dia (tela 12; sem print). Vai a lista inteira: batida que não vai é apagada pela API. */
export function ModalCorrigirBatidas({
  funcionario,
  dia,
  aoFechar,
}: {
  funcionario: { id: number; nome: string }
  dia: Dia
  aoFechar: () => void
}) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const corrigir = $api.useMutation('put', '/api/rh/funcionarios/{funcionarioId}/ponto/dias/{dia}')
  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo={`Corrigir ponto de ${formatarDiaMesDaData(dia.data)}`}
      descricao={`Para ${funcionario.nome}`}
      tamanho="larga"
      rodape={
        <>
          <Botao variante="secundario" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao type="submit" form="form-corrigir-batidas" carregando={corrigir.isPending}>
            Salvar correção
          </Botao>
        </>
      }
    >
      <FormularioComErroDoServidor
        id="form-corrigir-batidas"
        esquema={esquema}
        valoresIniciais={valoresDoDia(dia)}
        aoEnviar={async (valores) => {
          await corrigir.mutateAsync({
            params: { path: { funcionarioId: funcionario.id, dia: dia.data } },
            body: {
              marcacoes: valores.marcacoes.map((m) => ({
                ...(m.id === null ? {} : { id: m.id }),
                tipo: m.tipo,
                instanteUtc: instanteUtcDaHoraLocal(dia.data, m.hora, m.diaSeguinte),
              })),
            },
          })
          notificacao.sucesso('Ponto corrigido.')
          await invalidarRota(queryClient, `/api/rh/funcionarios/{funcionarioId}/ponto`)
          aoFechar()
        }}
      >
        {(formulario) => (
          <div className="flex flex-col gap-5">
            <p className="text-corpo text-texto-suave">
              A correção vale na hora e não passa pela segunda etapa. A pessoa não recebe e-mail. A
              batida corrigida fica marcada como corrigida pelo RH.
            </p>
            <Linhas formulario={formulario} />
          </div>
        )}
      </FormularioComErroDoServidor>
    </Janela>
  )
}
