import { useQueryClient } from '@tanstack/react-query'
import { Controller } from 'react-hook-form'
import { z } from 'zod'
import { $api } from '../../../shared/api/api'
import { ErroDeApi } from '../../../shared/api/erroDeApi'
import type { Esquema } from '../../../shared/api/tipos'
import { campoHora } from '../../../shared/form/esquemas'
import { FormularioComErroDoServidor } from '../../../shared/form/FormularioComErroDoServidor'
import { OPCOES_DE_UF, UFS } from '../../../shared/formato/ufs'
import { Aviso } from '../../../shared/ui/Aviso'
import { AvisoDeErro } from '../../../shared/ui/AvisoDeErro'
import { Botao } from '../../../shared/ui/Botao'
import { Campo } from '../../../shared/ui/Campo'
import { Carregando } from '../../../shared/ui/Carregando'
import { Entrada } from '../../../shared/ui/Entrada'
import { Selecao } from '../../../shared/ui/Selecao'
import { useNotificacao } from '../../../shared/ui/useNotificacao'
import { invalidarCadastros } from '../invalidarCadastros'

type Empresa = Esquema<'EmpresaConfiguracao'>

const inteiro = (minimo: number, maximo: number, mensagem: string) =>
  z
    .string()
    .trim()
    .regex(/^\d+$/, mensagem)
    .transform(Number)
    .refine((n) => n >= minimo && n <= maximo, { message: mensagem })

const esquema = z.object({
  nomeEmpresa: z.string().trim().min(1, 'Informe o nome da empresa.').max(150),
  uf: z.enum(UFS as [Esquema<'EmpresaUf'>, ...Esquema<'EmpresaUf'>[]], {
    message: 'Escolha a UF.',
  }),
  municipio: z.string().trim().min(1, 'Informe o município.').max(100),
  horarioEntrada: campoHora(),
  horarioSaida: campoHora(),
  toleranciaAtrasoMinutos: inteiro(0, 120, 'Use um número inteiro de 0 a 120.'),
  diaPagamento: inteiro(1, 31, 'Use um dia de 1 a 31.'),
})

type Entrada = z.input<typeof esquema>

function valoresDa(empresa: Empresa | undefined): Entrada {
  return {
    nomeEmpresa: empresa?.nomeEmpresa ?? '',
    uf: (empresa?.uf ?? '') as Entrada['uf'],
    municipio: empresa?.municipio ?? '',
    horarioEntrada: empresa?.horarioEntrada ?? '',
    horarioSaida: empresa?.horarioSaida ?? '',
    toleranciaAtrasoMinutos: empresa ? String(empresa.toleranciaAtrasoMinutos) : '',
    diaPagamento: empresa ? String(empresa.diaPagamento) : '',
  }
}

export function CadastroDeEmpresa() {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const consulta = $api.useQuery('get', '/api/eu/empresa', {}, { retry: false })
  const salvar = $api.useMutation('put', '/api/rh/empresa')
  const naoConfigurada = consulta.error instanceof ErroDeApi && consulta.error.status === 404
  const outroErro = consulta.error && !naoConfigurada

  return (
    <section aria-label="Empresa" className="flex flex-col gap-4">
      <div>
        <h2 className="text-t2 font-bold text-texto">Empresa</h2>
        <p className="mt-1 text-corpo text-texto-suave">Nome, horário e dia de pagamento</p>
      </div>
      {consulta.isLoading && <Carregando />}
      {outroErro && (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      )}
      {naoConfigurada && <Aviso tom="info">A empresa ainda não foi configurada.</Aviso>}
      {!consulta.isLoading && !outroErro && (
        <FormularioComErroDoServidor
          esquema={esquema}
          valoresIniciais={valoresDa(consulta.data)}
          aoEnviar={async (valores) => {
            await salvar.mutateAsync({ body: valores })
            await invalidarCadastros(queryClient, '/api/eu/empresa')
            notificacao.sucesso('Configuração salva.')
          }}
          className="flex max-w-[640px] flex-col gap-4"
        >
          {({ register, control, formState }) => {
            const erros = formState.errors
            return (
              <>
                <Campo rotulo="Nome da empresa" erro={erros.nomeEmpresa?.message}>
                  <Entrada {...register('nomeEmpresa')} />
                </Campo>
                <div className="grid grid-cols-[140px_1fr] gap-4">
                  <Controller
                    control={control}
                    name="uf"
                    render={({ field, fieldState }) => (
                      <Campo rotulo="UF" erro={fieldState.error?.message}>
                        <Selecao
                          rotulo="UF"
                          opcoes={OPCOES_DE_UF}
                          valor={field.value || null}
                          aoMudar={(v) => field.onChange(v ?? '')}
                        />
                      </Campo>
                    )}
                  />
                  <Campo rotulo="Município" erro={erros.municipio?.message}>
                    <Entrada {...register('municipio')} />
                  </Campo>
                </div>
                <p className="text-pequeno text-texto-suave">
                  A UF escolhe os feriados estaduais e o município, os municipais. Valem para todos.
                </p>
                <div className="grid grid-cols-2 gap-4">
                  <Campo rotulo="Horário padrão de entrada" erro={erros.horarioEntrada?.message}>
                    <Entrada {...register('horarioEntrada')} type="time" />
                  </Campo>
                  <Campo rotulo="Horário padrão de saída" erro={erros.horarioSaida?.message}>
                    <Entrada {...register('horarioSaida')} type="time" />
                  </Campo>
                </div>
                <p className="text-pequeno text-texto-suave">
                  É o horário de todos. A saída pode ser menor que a entrada (turno que atravessa a
                  meia-noite).
                </p>
                <Campo
                  rotulo="Tolerância de atraso, em minutos"
                  dica="O atraso é medido contra a entrada mais a tolerância."
                  erro={erros.toleranciaAtrasoMinutos?.message}
                >
                  <Entrada
                    {...register('toleranciaAtrasoMinutos')}
                    type="number"
                    min={0}
                    max={120}
                  />
                </Campo>
                <Campo
                  rotulo="Dia do pagamento"
                  dica="Mês sem esse dia: vale o último dia do mês. Fim de semana ou feriado: antecipa para o dia útil anterior."
                  erro={erros.diaPagamento?.message}
                >
                  <Entrada {...register('diaPagamento')} type="number" min={1} max={31} />
                </Campo>
                <div>
                  <Botao type="submit" carregando={salvar.isPending}>
                    Salvar
                  </Botao>
                </div>
              </>
            )
          }}
        </FormularioComErroDoServidor>
      )}
    </section>
  )
}
