import { Controller, type UseFormReturn } from 'react-hook-form'
import type { CadastroExistente } from '../../shared/api/cadastroExistente'
import { mascararCpf, mascararTelefone } from '../../shared/formato/documentos'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { Selecao } from '../../shared/ui/Selecao'
import type { EntradaDoColaborador, SaidaDoColaborador } from './esquemaDoColaborador'
import { OPCOES_DE_NIVEL_DE_ACESSO, OPCOES_DE_TIPO_DE_CONTRATO } from './rotulos'
import type { OpcoesDeApoio } from './useOpcoesDeApoio'

type Formulario = UseFormReturn<EntradaDoColaborador, unknown, SaidaDoColaborador>
type Duplicado = { campo: 'cpf' | 'emailCorporativo'; cadastro: CadastroExistente }

function Bloco({ titulo, children }: { titulo: string; children: React.ReactNode }) {
  return (
    <fieldset className="flex flex-col gap-4">
      <legend className="mb-2 text-t3 font-bold text-texto">{titulo}</legend>
      {children}
    </fieldset>
  )
}

function LinkDoDuplicado({ duplicado }: { duplicado: Duplicado }) {
  return (
    <BotaoLink
      to={`/rh/colaboradores/${duplicado.cadastro.funcionarioId}`}
      variante="texto"
      tamanho="pequeno"
      className="self-start"
    >
      {`Abrir a ficha de ${duplicado.cadastro.nome}`}
    </BotaoLink>
  )
}

function textoDoGestor(gestor: string | null | undefined): string {
  if (gestor === undefined) return ''
  return gestor ?? 'Este setor não tem gestor'
}

export function CamposDoColaborador({
  formulario,
  opcoes,
  modo,
  perfilBloqueado = false,
  duplicado = null,
}: {
  formulario: Formulario
  opcoes: OpcoesDeApoio
  modo: 'cadastro' | 'edicao'
  perfilBloqueado?: boolean
  duplicado?: Duplicado | null
}) {
  const { register, control, watch, formState } = formulario
  const erros = formState.errors
  const setorId = watch('setorId')

  return (
    <div className="flex flex-col gap-8">
      <Bloco titulo="Dados pessoais">
        <Campo rotulo="Nome completo" erro={erros.nome?.message}>
          <Entrada {...register('nome')} placeholder="Como está no documento" autoComplete="off" />
        </Campo>
        <div className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-1">
            <Campo rotulo="CPF" erro={erros.cpf?.message}>
              <Entrada
                {...register('cpf')}
                mascara={mascararCpf}
                placeholder="000.000.000-00"
                inputMode="numeric"
              />
            </Campo>
            {duplicado?.campo === 'cpf' && <LinkDoDuplicado duplicado={duplicado} />}
          </div>
          <Campo rotulo="Data de nascimento" erro={erros.dataNascimento?.message}>
            <Entrada {...register('dataNascimento')} type="date" lang="pt-BR" />
          </Campo>
        </div>
        <div className="grid grid-cols-2 gap-4">
          <Campo rotulo="Telefone" erro={erros.telefone?.message}>
            <Entrada
              {...register('telefone')}
              mascara={mascararTelefone}
              placeholder="(11) 90000-0000"
              inputMode="tel"
            />
          </Campo>
          <div className="flex flex-col gap-1">
            <Campo rotulo="E-mail corporativo" erro={erros.emailCorporativo?.message}>
              <Entrada
                {...register('emailCorporativo')}
                type="email"
                placeholder="nome.sobrenome@empresa.com.br"
              />
            </Campo>
            {duplicado?.campo === 'emailCorporativo' && <LinkDoDuplicado duplicado={duplicado} />}
          </div>
        </div>
        {modo === 'edicao' && (
          <>
            <Campo rotulo="E-mail pessoal" erro={erros.emailPessoal?.message}>
              <Entrada {...register('emailPessoal')} type="email" />
            </Campo>
            <div className="grid grid-cols-2 gap-4">
              <Campo rotulo="RG" erro={erros.rg?.message}>
                <Entrada {...register('rg')} />
              </Campo>
              <Campo rotulo="Órgão emissor" erro={erros.rgOrgaoEmissor?.message}>
                <Entrada {...register('rgOrgaoEmissor')} />
              </Campo>
            </div>
          </>
        )}
      </Bloco>

      {modo === 'edicao' && (
        <Bloco titulo="Endereço">
          <div className="grid grid-cols-[160px_1fr] gap-4">
            <Campo rotulo="CEP" erro={erros.endereco?.cep?.message}>
              <Entrada {...register('endereco.cep')} inputMode="numeric" />
            </Campo>
            <Campo rotulo="Logradouro" erro={erros.endereco?.logradouro?.message}>
              <Entrada {...register('endereco.logradouro')} />
            </Campo>
          </div>
          <div className="grid grid-cols-[120px_1fr] gap-4">
            <Campo rotulo="Número" erro={erros.endereco?.numero?.message}>
              <Entrada {...register('endereco.numero')} />
            </Campo>
            <Campo rotulo="Complemento" erro={erros.endereco?.complemento?.message}>
              <Entrada {...register('endereco.complemento')} />
            </Campo>
          </div>
          <div className="grid grid-cols-[1fr_1fr_100px] gap-4">
            <Campo rotulo="Bairro" erro={erros.endereco?.bairro?.message}>
              <Entrada {...register('endereco.bairro')} />
            </Campo>
            <Campo rotulo="Cidade" erro={erros.endereco?.cidade?.message}>
              <Entrada {...register('endereco.cidade')} />
            </Campo>
            <Campo rotulo="UF" erro={erros.endereco?.uf?.message}>
              <Entrada {...register('endereco.uf')} maxLength={2} />
            </Campo>
          </div>
        </Bloco>
      )}

      <Bloco titulo="Contrato">
        <div className="grid grid-cols-2 gap-4">
          <Campo rotulo="Data de admissão" erro={erros.dataAdmissao?.message}>
            <Entrada {...register('dataAdmissao')} type="date" lang="pt-BR" />
          </Campo>
          <Controller
            control={control}
            name="tipoContrato"
            render={({ field }) => (
              <Campo rotulo="Tipo de contrato" erro={erros.tipoContrato?.message}>
                <Selecao
                  rotulo="Tipo de contrato"
                  opcoes={OPCOES_DE_TIPO_DE_CONTRATO}
                  valor={field.value}
                  aoMudar={(v) => field.onChange(v ?? 'CLT')}
                />
              </Campo>
            )}
          />
        </div>
        <div className="grid grid-cols-2 gap-4">
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
                  invalido={!!fieldState.error}
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
                  invalido={!!fieldState.error}
                />
              </Campo>
            )}
          />
        </div>
        <div className="grid grid-cols-2 gap-4">
          <Campo rotulo="Gestor">
            <Entrada
              readOnly
              value={textoDoGestor(opcoes.gestorDoSetor(setorId))}
              placeholder="O gestor do setor escolhido"
            />
          </Campo>
          <Controller
            control={control}
            name="perfil"
            render={({ field }) => (
              <Campo
                rotulo="Nível de acesso"
                erro={erros.perfil?.message}
                dica={
                  perfilBloqueado
                    ? 'Você não pode alterar o seu próprio nível de acesso.'
                    : undefined
                }
              >
                <Selecao
                  rotulo="Nível de acesso"
                  opcoes={OPCOES_DE_NIVEL_DE_ACESSO}
                  valor={field.value}
                  aoMudar={(v) => field.onChange(v ?? 'Funcionario')}
                  desabilitado={perfilBloqueado}
                />
              </Campo>
            )}
          />
        </div>
      </Bloco>

      <Bloco titulo="Benefícios">
        <Controller
          control={control}
          name="beneficioIds"
          render={({ field }) => (
            <div className="grid grid-cols-2 gap-3">
              {opcoes.beneficios.map((beneficio) => (
                <CaixaDeSelecao
                  key={beneficio.id}
                  rotulo={beneficio.nome}
                  marcada={field.value.includes(beneficio.id)}
                  aoMudar={(marcada) =>
                    field.onChange(
                      marcada
                        ? [...field.value, beneficio.id]
                        : field.value.filter((id) => id !== beneficio.id),
                    )
                  }
                />
              ))}
            </div>
          )}
        />
      </Bloco>
    </div>
  )
}
