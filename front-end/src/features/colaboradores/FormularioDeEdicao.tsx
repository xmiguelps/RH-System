import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useAuth } from '../autenticacao/useAuth'
import { $api } from '../../shared/api/api'
import { lerCadastroExistente, type CadastroExistente } from '../../shared/api/cadastroExistente'
import { invalidarRota } from '../../shared/api/invalidar'
import type { Esquema } from '../../shared/api/tipos'
import { erroNoCampo } from '../../shared/form/erroNoCampo'
import { FormularioComErroDoServidor } from '../../shared/form/FormularioComErroDoServidor'
import { mascararCep, mascararCpf, mascararTelefone } from '../../shared/formato/documentos'
import { Aviso } from '../../shared/ui/Aviso'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { CamposDoColaborador } from './CamposDoColaborador'
import {
  esquemaDoColaborador,
  montarCorpoDeEdicao,
  type EntradaDoColaborador,
} from './esquemaDoColaborador'
import { useOpcoesDeApoio } from './useOpcoesDeApoio'

type Ficha = Esquema<'FuncionarioFichaRh'>

const CAMPO_POR_CODIGO = {
  CPF_JA_CADASTRADO: 'cpf',
  EMAIL_JA_CADASTRADO: 'emailCorporativo',
  GESTOR_DO_SETOR_INATIVO: 'setorId',
  GESTOR_PRECISA_TER_PERFIL_GESTOR: 'perfil',
  NAO_PODE_ALTERAR_O_PROPRIO_PERFIL: 'perfil',
} as const

/** A ficha pode ter campos obrigatórios null (o administrador do seed): o formulário os abre vazios. */
function valoresDaFicha(ficha: Ficha): EntradaDoColaborador {
  const e = ficha.endereco
  return {
    nome: ficha.nome,
    cpf: ficha.cpf ? mascararCpf(ficha.cpf) : '',
    dataNascimento: ficha.dataNascimento ?? '',
    telefone: ficha.telefone ? mascararTelefone(ficha.telefone) : '',
    emailCorporativo: ficha.emailCorporativo ?? '',
    dataAdmissao: ficha.dataAdmissao,
    tipoContrato: ficha.tipoContrato ?? 'CLT',
    cargoId: ficha.cargo ? String(ficha.cargo.id) : '',
    setorId: ficha.setor ? String(ficha.setor.id) : '',
    perfil: ficha.perfil ?? 'Funcionario',
    beneficioIds: ficha.beneficios.map((b) => b.beneficioId),
    enviarConvite: false,
    emailPessoal: ficha.emailPessoal ?? '',
    rg: ficha.rg ?? '',
    rgOrgaoEmissor: ficha.rgOrgaoEmissor ?? '',
    endereco: {
      cep: e.cep ? mascararCep(e.cep) : '',
      logradouro: e.logradouro ?? '',
      numero: e.numero ?? '',
      complemento: e.complemento ?? '',
      bairro: e.bairro ?? '',
      cidade: e.cidade ?? '',
      uf: e.uf ?? '',
    },
  }
}

export function FormularioDeEdicao({
  ficha,
  aoEncerrar,
}: {
  ficha: Ficha
  aoEncerrar: () => void
}) {
  const { usuario } = useAuth()
  const opcoes = useOpcoesDeApoio()
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const editar = $api.useMutation('put', '/api/rh/funcionarios/{funcionarioId}')
  const [duplicado, setDuplicado] = useState<{
    campo: 'cpf' | 'emailCorporativo'
    cadastro: CadastroExistente
  } | null>(null)
  const perfilBloqueado = usuario?.funcionarioId === ficha.id
  const setorOriginal = ficha.setor ? String(ficha.setor.id) : ''

  return (
    <div className="flex flex-col gap-4">
      {!!opcoes.erro && <AvisoDeErro erro={opcoes.erro} aoTentarDeNovo={opcoes.tentarDeNovo} />}
      <FormularioComErroDoServidor
        id="form-editar"
        esquema={esquemaDoColaborador}
        valoresIniciais={valoresDaFicha(ficha)}
        aoEnviar={async (valores) => {
          setDuplicado(null)
          try {
            await editar.mutateAsync({
              params: { path: { funcionarioId: ficha.id } },
              body: montarCorpoDeEdicao(valores),
            })
          } catch (erro) {
            const existente = lerCadastroExistente(erro)
            if (existente) {
              const codigo = (erro as { codigo?: string }).codigo
              setDuplicado({
                campo: codigo === 'EMAIL_JA_CADASTRADO' ? 'emailCorporativo' : 'cpf',
                cadastro: existente,
              })
            }
            throw erroNoCampo(erro, CAMPO_POR_CODIGO)
          }
          await invalidarRota(queryClient, '/api/rh/funcionarios')
          notificacao.sucesso('Dados salvos.')
          aoEncerrar()
        }}
      >
        {(formulario) => {
          const email = formulario.watch('emailCorporativo')
          const setor = formulario.watch('setorId')
          return (
            <div className="flex flex-col gap-6">
              <CamposDoColaborador
                formulario={formulario}
                opcoes={opcoes}
                modo="edicao"
                perfilBloqueado={perfilBloqueado}
                duplicado={duplicado}
              />
              {email.trim() !== (ficha.emailCorporativo ?? '') && (
                <Aviso tom="alerta">
                  Trocar o e-mail corporativo troca o e-mail de login: a pessoa precisa usar o novo.
                </Aviso>
              )}
              {setor !== setorOriginal && (
                <Aviso tom="info">O gestor da pessoa passa a ser o do novo setor.</Aviso>
              )}
              <div className="flex justify-end gap-3">
                <Botao variante="secundario" onClick={aoEncerrar}>
                  Cancelar
                </Botao>
                <Botao type="submit" carregando={editar.isPending} disabled={opcoes.carregando}>
                  Salvar alterações
                </Botao>
              </div>
            </div>
          )
        }}
      </FormularioComErroDoServidor>
    </div>
  )
}
