import { useQueryClient } from '@tanstack/react-query'
import { Controller, type UseFormReturn } from 'react-hook-form'
import { z } from 'zod'
import { $api } from '../../shared/api/api'
import { invalidarRota } from '../../shared/api/invalidar'
import { campoData } from '../../shared/form/esquemas'
import { FormularioComErroDoServidor } from '../../shared/form/FormularioComErroDoServidor'
import { hojeEmSaoPaulo } from '../../shared/formato/datas'
import { AreaDeTexto } from '../../shared/ui/AreaDeTexto'
import { Aviso } from '../../shared/ui/Aviso'
import { Botao } from '../../shared/ui/Botao'
import { Campo } from '../../shared/ui/Campo'
import { ComboDeBusca } from '../../shared/ui/ComboDeBusca'
import { Entrada } from '../../shared/ui/Entrada'
import { Janela } from '../../shared/ui/Janela'
import { Selecao } from '../../shared/ui/Selecao'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { useBuscaDeColaborador } from '../ponto/useBuscaDeColaborador'
import { LIMITE_DA_DESCRICAO, LIMITE_DO_MOTIVO, OPCOES_DE_TIPO } from './rotulosDasAdvertencias'

type Pessoa = { id: number; nome: string }
type Modo = 'rh' | 'gestor'

const OPCOES_DE_APLICADA_POR = [
  { valor: 'GESTOR_DO_COLABORADOR', rotulo: 'Gestor do colaborador' },
  { valor: 'RH', rotulo: 'RH' },
] as const

function criarEsquema(hoje: string) {
  return z.object({
    funcionarioId: z.string().min(1, 'Escolha um colaborador.'),
    dataDoOcorrido: z
      .string()
      .refine((v) => campoData().safeParse(v).success, 'Informe a data do ocorrido.')
      .refine((v) => v <= hoje, 'A data do ocorrido não pode ser no futuro.'),
    tipo: z.enum(['VERBAL', 'ESCRITA']),
    aplicadaPor: z.enum(['GESTOR_DO_COLABORADOR', 'RH']),
    motivo: z
      .string()
      .trim()
      .min(1, 'Escreva o motivo.')
      .max(LIMITE_DO_MOTIVO, 'O motivo tem até 255 caracteres.'),
    descricao: z
      .string()
      .trim()
      .min(1, 'Escreva a descrição.')
      .max(LIMITE_DA_DESCRICAO, 'A descrição tem até 5000 caracteres.'),
  })
}

type Esquema = ReturnType<typeof criarEsquema>
type Valores = z.input<Esquema>
type Formulario = UseFormReturn<Valores, unknown, z.output<Esquema>>

function ColaboradorDoRh({ formulario }: { formulario: Formulario }) {
  const busca = useBuscaDeColaborador()
  return (
    <Controller
      control={formulario.control}
      name="funcionarioId"
      render={({ field, fieldState }) => (
        <Campo rotulo="Colaborador" erro={fieldState.error?.message}>
          <ComboDeBusca
            rotulo="Colaborador"
            placeholder="Escolha um colaborador"
            opcoes={busca.opcoes}
            valor={field.value || null}
            aoDigitar={busca.aoDigitar}
            carregando={busca.carregando}
            aoMudar={(v) => field.onChange(v ?? '')}
          />
        </Campo>
      )}
    />
  )
}

function ColaboradorDaEquipe({ formulario }: { formulario: Formulario }) {
  const equipe = $api.useQuery('get', '/api/gestor/equipe')
  const opcoes = (equipe.data?.itens ?? []).map((p) => ({ valor: String(p.id), rotulo: p.nome }))
  return (
    <Controller
      control={formulario.control}
      name="funcionarioId"
      render={({ field, fieldState }) => (
        <Campo rotulo="Colaborador" erro={fieldState.error?.message}>
          <Selecao
            rotulo="Colaborador"
            placeholder="Escolha um colaborador"
            opcoes={opcoes}
            valor={field.value || null}
            carregando={equipe.isLoading}
            invalido={!!fieldState.error}
            aoMudar={(v) => field.onChange(v ?? '')}
          />
        </Campo>
      )}
    />
  )
}

function CamposDaAdvertencia({
  formulario,
  modo,
  pessoaFixa,
}: {
  formulario: Formulario
  modo: Modo
  pessoaFixa?: Pessoa
}) {
  const { control, register, formState } = formulario
  const e = formState.errors
  return (
    <div className="flex flex-col gap-5">
      {pessoaFixa ? (
        <Campo rotulo="Colaborador">
          <Entrada value={pessoaFixa.nome} disabled readOnly />
        </Campo>
      ) : modo === 'rh' ? (
        <ColaboradorDoRh formulario={formulario} />
      ) : (
        <ColaboradorDaEquipe formulario={formulario} />
      )}
      <div className="grid grid-cols-2 gap-4">
        <Campo rotulo="Data do ocorrido" erro={e.dataDoOcorrido?.message}>
          <Entrada
            type="date"
            lang="pt-BR"
            max={hojeEmSaoPaulo()}
            {...register('dataDoOcorrido')}
          />
        </Campo>
        <Controller
          control={control}
          name="tipo"
          render={({ field }) => (
            <Campo rotulo="Tipo">
              <Selecao
                rotulo="Tipo"
                opcoes={OPCOES_DE_TIPO}
                valor={field.value}
                aoMudar={(v) => v && field.onChange(v)}
              />
            </Campo>
          )}
        />
      </div>
      {modo === 'rh' && (
        <Controller
          control={control}
          name="aplicadaPor"
          render={({ field }) => (
            <Campo rotulo="Aplicada por">
              <Selecao
                rotulo="Aplicada por"
                opcoes={OPCOES_DE_APLICADA_POR}
                valor={field.value}
                aoMudar={(v) => v && field.onChange(v)}
              />
            </Campo>
          )}
        />
      )}
      <Campo rotulo="Motivo" erro={e.motivo?.message}>
        <Entrada
          maxLength={LIMITE_DO_MOTIVO}
          placeholder="Resumo em uma linha, ex.: atrasos repetidos sem justificativa"
          {...register('motivo')}
        />
      </Campo>
      <Campo rotulo="Descrição" erro={e.descricao?.message}>
        <AreaDeTexto
          rows={5}
          limite={LIMITE_DA_DESCRICAO}
          placeholder="O que aconteceu, quando e qual regra foi descumprida"
          {...register('descricao')}
        />
      </Campo>
      <Aviso tom="info">
        {modo === 'rh'
          ? 'O colaborador vê a advertência no portal e confirma que tomou ciência. Se discordar, ele pode contestar e explicar o motivo.'
          : 'A advertência fica registrada como aplicada por você. O RH recebe uma cópia e o colaborador confirma a ciência pelo portal, podendo contestar.'}
      </Aviso>
    </div>
  )
}

/**
 * Telas 15 e 11 (RH: "Nova advertência", com "Aplicada por") e 53 e 60 (gestor: "Registrar
 * advertência", sem "Aplicada por"). Com `pessoaFixa`, o colaborador vem escolhido e travado.
 */
export function PainelRegistrarAdvertencia({
  modo,
  pessoaFixa,
  aoFechar,
}: {
  modo: Modo
  pessoaFixa?: Pessoa
  aoFechar: () => void
}) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const hoje = hojeEmSaoPaulo()
  const registrarRh = $api.useMutation('post', '/api/rh/funcionarios/{funcionarioId}/advertencias')
  const registrarGestor = $api.useMutation(
    'post',
    '/api/gestor/equipe/{funcionarioId}/advertencias',
  )
  const salvando = registrarRh.isPending || registrarGestor.isPending
  const formId = `form-advertencia-${modo}`
  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo={modo === 'rh' ? 'Nova advertência' : 'Registrar advertência'}
      posicao="direita"
      largura="media"
      rodape={
        <>
          <Botao variante="secundario" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao type="submit" form={formId} carregando={salvando}>
            Registrar advertência
          </Botao>
        </>
      }
    >
      <FormularioComErroDoServidor
        id={formId}
        esquema={criarEsquema(hoje)}
        valoresIniciais={{
          funcionarioId: pessoaFixa ? String(pessoaFixa.id) : '',
          dataDoOcorrido: hoje,
          tipo: 'VERBAL',
          aplicadaPor: 'GESTOR_DO_COLABORADOR',
          motivo: '',
          descricao: '',
        }}
        aoEnviar={async (v) => {
          const funcionarioId = Number(v.funcionarioId)
          const comum = {
            dataDoOcorrido: v.dataDoOcorrido,
            tipo: v.tipo,
            motivo: v.motivo,
            descricao: v.descricao,
          }
          const criada =
            modo === 'rh'
              ? await registrarRh.mutateAsync({
                  params: { path: { funcionarioId } },
                  body: { ...comum, aplicadaPor: v.aplicadaPor },
                })
              : await registrarGestor.mutateAsync({
                  params: { path: { funcionarioId } },
                  body: comum,
                })
          const nome = criada?.funcionario.nome ?? pessoaFixa?.nome ?? 'A pessoa'
          notificacao.sucesso(
            modo === 'rh'
              ? `Advertência registrada. ${nome} foi avisado por e-mail.`
              : `Advertência registrada. ${nome} foi avisado por e-mail e o RH recebeu uma cópia.`,
          )
          await Promise.all(
            modo === 'rh'
              ? [
                  invalidarRota(queryClient, '/api/rh/advertencias'),
                  invalidarRota(queryClient, '/api/rh/funcionarios/{funcionarioId}'),
                ]
              : [
                  invalidarRota(queryClient, '/api/gestor/advertencias'),
                  invalidarRota(queryClient, '/api/gestor/equipe'),
                ],
          )
          aoFechar()
        }}
      >
        {(formulario) => (
          <CamposDaAdvertencia formulario={formulario} modo={modo} pessoaFixa={pessoaFixa} />
        )}
      </FormularioComErroDoServidor>
    </Janela>
  )
}
