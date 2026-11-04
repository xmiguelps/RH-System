import { useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'
import {
  Controller,
  useFieldArray,
  useWatch,
  type Control,
  type UseFormReturn,
} from 'react-hook-form'
import { z } from 'zod'
import { invalidarContadoresDoMenu } from '../../app/useContadoresDoMenu'
import { $api } from '../../shared/api/api'
import { invalidarRota } from '../../shared/api/invalidar'
import { erroNoCampo } from '../../shared/form/erroNoCampo'
import { campoData, campoHora } from '../../shared/form/esquemas'
import { FormularioComErroDoServidor } from '../../shared/form/FormularioComErroDoServidor'
import {
  formatarHora,
  hojeEmSaoPaulo,
  instanteUtcDaHoraLocal,
  somarDias,
} from '../../shared/formato/datas'
import { AreaDeTexto } from '../../shared/ui/AreaDeTexto'
import { Botao } from '../../shared/ui/Botao'
import { Campo } from '../../shared/ui/Campo'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'
import { Entrada } from '../../shared/ui/Entrada'
import { Janela } from '../../shared/ui/Janela'
import { Selecao } from '../../shared/ui/Selecao'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { ChipsDosPeriodos } from './PecasDoPonto'

const TIPOS = [
  { valor: 'ENTRADA', rotulo: 'Entrada' },
  { valor: 'SAIDA', rotulo: 'Saída' },
] as const

const CAMPO_POR_CODIGO = {
  PONTO_PEDIDO_JA_EM_ANALISE: 'data',
  PONTO_PEDIDO_DIA_NAO_PASSOU: 'data',
  PONTO_INSTANTE_NO_FUTURO: 'itens',
  PONTO_INSTANTE_FORA_DO_DIA: 'itens',
  PONTO_SEQUENCIA_INVALIDA: 'itens',
} as const

const NOVA = 'nova'

function criarEsquema(ontem: string) {
  return z.object({
    data: campoData().refine((d) => d <= ontem, {
      message: 'Escolha um dia que já passou (até ontem).',
    }),
    itens: z
      .array(
        z.object({
          tipo: z.enum(['ENTRADA', 'SAIDA']),
          hora: campoHora(),
          diaSeguinte: z.boolean(),
          /** 'nova' ou o id de uma batida atual do dia (vira `marcacaoId`). */
          marcacao: z.string(),
        }),
      )
      .min(1, 'Inclua pelo menos uma batida.')
      .max(10, 'No máximo 10 batidas por pedido.'),
    justificativa: z
      .string()
      .trim()
      .min(5, 'Escreva de 5 a 500 caracteres.')
      .max(500, 'Escreva de 5 a 500 caracteres.'),
  })
}

type Esquema = ReturnType<typeof criarEsquema>
type Valores = z.input<Esquema>
type Formulario = UseFormReturn<Valores, unknown, z.output<Esquema>>

const BATIDA_VAZIA: Valores['itens'][number] = {
  tipo: 'ENTRADA',
  hora: '',
  diaSeguinte: false,
  marcacao: NOVA,
}

const DATA_VALIDA = /^\d{4}-\d{2}-\d{2}$/

/** O dia escolhido, no espelho do mês dele: o registro atual e as batidas que dá para corrigir. */
function useDiaEscolhido(control: Control<Valores>) {
  const data = useWatch({ control, name: 'data' })
  const valida = DATA_VALIDA.test(data ?? '')
  const espelho = $api.useQuery(
    'get',
    '/api/eu/ponto/espelho',
    { params: { query: { ano: Number(data?.slice(0, 4)), mes: Number(data?.slice(5, 7)) } } },
    { enabled: valida },
  )
  const dia = valida ? espelho.data?.dias.find((d) => d.data === data) : undefined
  const marcacoes = (dia?.periodos ?? [])
    .flatMap((p) => [p.entrada, p.saida])
    .filter((m): m is NonNullable<typeof m> => m !== null)
  return { valida, carregando: espelho.isLoading, dia, marcacoes }
}

function RegistroAtual({ control }: { control: Control<Valores> }) {
  const { valida, carregando, dia } = useDiaEscolhido(control)
  if (!valida) return null
  return (
    <div className="flex flex-col gap-2">
      <span className="text-tabela font-semibold text-texto">Registro atual do dia</span>
      {carregando ? (
        <span className="text-pequeno text-texto-suave">Carregando…</span>
      ) : (
        <ChipsDosPeriodos
          periodos={dia?.periodos ?? []}
          ehHoje={false}
          semNada="Nenhum registro neste dia."
        />
      )}
    </div>
  )
}

function Batidas({ formulario }: { formulario: Formulario }) {
  const { control, register, formState, setValue } = formulario
  const lista = useFieldArray({ control, name: 'itens' })
  const { marcacoes } = useDiaEscolhido(control)
  const opcoesDeBatida = [
    { valor: NOVA, rotulo: 'Nova batida' },
    ...marcacoes.map((m) => ({
      valor: String(m.id),
      rotulo: `${m.rotulo} ${formatarHora(m.instanteUtc)}`,
    })),
  ]
  const erroDaLista = formState.errors.itens?.message ?? formState.errors.itens?.root?.message
  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="mb-2 text-tabela font-semibold text-texto">Correção pedida</legend>
      {lista.fields.map((campo, i) => (
        <div key={campo.id} className="flex flex-wrap items-end gap-3">
          <div className="w-40">
            <Controller
              control={control}
              name={`itens.${i}.tipo`}
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
              erro={formState.errors.itens?.[i]?.hora?.message}
            >
              <Entrada type="time" {...register(`itens.${i}.hora`)} />
            </Campo>
          </div>
          {marcacoes.length > 0 && (
            <div className="w-52">
              <Controller
                control={control}
                name={`itens.${i}.marcacao`}
                render={({ field }) => (
                  <Campo rotulo={`Corrigir uma batida deste dia (${i + 1})`}>
                    <Selecao
                      rotulo={`Corrigir uma batida deste dia (${i + 1})`}
                      opcoes={opcoesDeBatida}
                      valor={field.value}
                      aoMudar={(v) => {
                        field.onChange(v ?? NOVA)
                        const escolhida = marcacoes.find((m) => String(m.id) === v)
                        if (escolhida) setValue(`itens.${i}.tipo`, escolhida.tipo)
                      }}
                    />
                  </Campo>
                )}
              />
            </div>
          )}
          <Controller
            control={control}
            name={`itens.${i}.diaSeguinte`}
            render={({ field }) => (
              <CaixaDeSelecao
                rotulo={`No dia seguinte (${i + 1})`}
                marcada={field.value}
                aoMudar={field.onChange}
              />
            )}
          />
          {lista.fields.length > 1 && (
            <Botao
              variante="texto"
              tamanho="pequeno"
              onClick={() => lista.remove(i)}
              aria-label={`Remover a batida ${i + 1}`}
            >
              Remover
            </Botao>
          )}
        </div>
      ))}
      <div className="flex gap-3">
        <Botao
          variante="secundario"
          tamanho="pequeno"
          disabled={lista.fields.length >= 10}
          onClick={() => lista.append({ ...BATIDA_VAZIA })}
        >
          Incluir uma batida
        </Botao>
        <Botao
          variante="secundario"
          tamanho="pequeno"
          disabled={lista.fields.length >= 9}
          onClick={() =>
            lista.append([
              { ...BATIDA_VAZIA, tipo: 'ENTRADA' },
              { ...BATIDA_VAZIA, tipo: 'SAIDA' },
            ])
          }
        >
          Incluir um período (entrada e saída)
        </Botao>
      </div>
      {erroDaLista && (
        <p role="alert" className="text-pequeno text-erro-texto">
          {erroDaLista}
        </p>
      )}
    </fieldset>
  )
}

/** "Pedir ajuste de ponto" (sem print; `telas\fatia-3.md`). A hora digitada vira UTC pelo -03:00. */
export function ModalPedirAjuste({ dia, aoFechar }: { dia?: string; aoFechar: () => void }) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const criar = $api.useMutation('post', '/api/eu/ponto/pedidos-de-ajuste')
  const ontem = somarDias(hojeEmSaoPaulo(), -1)
  const esquema = useMemo(() => criarEsquema(ontem), [ontem])

  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo="Pedir ajuste de ponto"
      tamanho="larga"
      rodape={
        <>
          <Botao variante="secundario" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao type="submit" form="form-pedir-ajuste" carregando={criar.isPending}>
            {criar.isPending ? 'Enviando...' : 'Enviar pedido'}
          </Botao>
        </>
      }
    >
      <FormularioComErroDoServidor
        id="form-pedir-ajuste"
        esquema={esquema}
        valoresIniciais={{ data: dia ?? '', itens: [{ ...BATIDA_VAZIA }], justificativa: '' }}
        aoEnviar={async (valores) => {
          try {
            await criar.mutateAsync({
              body: {
                data: valores.data,
                justificativa: valores.justificativa,
                itens: valores.itens.map((item) => ({
                  tipo: item.tipo,
                  instanteUtc: instanteUtcDaHoraLocal(valores.data, item.hora, item.diaSeguinte),
                  ...(item.marcacao === NOVA ? {} : { marcacaoId: Number(item.marcacao) }),
                })),
              },
            })
          } catch (erro) {
            throw erroNoCampo(erro, CAMPO_POR_CODIGO)
          }
          notificacao.sucesso('Pedido enviado. Você recebe um e-mail a cada resposta.')
          await Promise.all([
            invalidarRota(queryClient, '/api/eu/ponto'),
            invalidarContadoresDoMenu(queryClient),
          ])
          aoFechar()
        }}
      >
        {(formulario) => (
          <div className="flex flex-col gap-5">
            <p className="text-corpo text-texto-suave">
              O pedido passa pela análise do gestor (se a sua equipe tem gestor) e depois pelo RH.
              Você recebe um e-mail a cada resposta.
            </p>
            <Campo rotulo="Dia do ponto" erro={formulario.formState.errors.data?.message}>
              <Entrada
                type="date"
                lang="pt-BR"
                max={ontem}
                readOnly={!!dia}
                {...formulario.register('data')}
              />
            </Campo>
            <RegistroAtual control={formulario.control} />
            <Batidas formulario={formulario} />
            <Campo rotulo="Justificativa" erro={formulario.formState.errors.justificativa?.message}>
              <AreaDeTexto limite={500} rows={3} {...formulario.register('justificativa')} />
            </Campo>
          </div>
        )}
      </FormularioComErroDoServidor>
    </Janela>
  )
}
