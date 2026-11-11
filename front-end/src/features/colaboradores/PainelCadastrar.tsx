import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Controller } from 'react-hook-form'
import { $api } from '../../shared/api/api'
import { lerCadastroExistente, type CadastroExistente } from '../../shared/api/cadastroExistente'
import { invalidarRota } from '../../shared/api/invalidar'
import { erroNoCampo } from '../../shared/form/erroNoCampo'
import { FormularioComErroDoServidor } from '../../shared/form/FormularioComErroDoServidor'
import { usePainelSobreLista } from '../../shared/lista/usePainelSobreLista'
import { Abas } from '../../shared/ui/Abas'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'
import { Janela } from '../../shared/ui/Janela'
import { CaixaDoFormularioDeAdmissao } from '../formularios/CaixaDoFormularioDeAdmissao'
import { CaixaDoOnboarding } from '../treinamentos/CaixaDoOnboarding'
import { CorpoDaImportacao, RodapeDaImportacao } from '../importacao/ImportacaoDePlanilha'
import { useImportacao } from '../importacao/useImportacao'
import { CamposDoColaborador } from './CamposDoColaborador'
import {
  esquemaDoColaborador,
  montarCorpoDeCadastro,
  valoresIniciaisDoCadastro,
} from './esquemaDoColaborador'
import { useOpcoesDeApoio } from './useOpcoesDeApoio'

export type AvisoDeCadastro = {
  funcionarioId: number
  nome: string
  emailDoConvite: string | null
}

const CAMPO_POR_CODIGO = {
  CPF_JA_CADASTRADO: 'cpf',
  EMAIL_JA_CADASTRADO: 'emailCorporativo',
  GESTOR_DO_SETOR_INATIVO: 'setorId',
} as const

export function PainelCadastrar() {
  const { fechar } = usePainelSobreLista()
  const queryClient = useQueryClient()
  const opcoes = useOpcoesDeApoio()
  const cadastrar = $api.useMutation('post', '/api/rh/funcionarios')
  const [duplicado, setDuplicado] = useState<{
    campo: 'cpf' | 'emailCorporativo'
    cadastro: CadastroExistente
  } | null>(null)
  const [modo, setModo] = useState<'um' | 'planilha'>('um')
  const [formularioAdmissaoId, setFormularioAdmissaoId] = useState<number | null>(null)
  const [incluirNoOnboarding, setIncluirNoOnboarding] = useState(false)
  // Trocar de modo ou fechar descarta a prévia (nada foi gravado).
  const importacao = useImportacao()
  const naPlanilha = modo === 'planilha'

  return (
    <Janela
      aberta
      aoFechar={() => fechar()}
      titulo="Adicionar colaborador"
      posicao="direita"
      largura={naPlanilha ? 'larga' : 'media'}
      rodape={
        naPlanilha ? (
          <RodapeDaImportacao importacao={importacao} aoFechar={() => fechar()} />
        ) : (
          <>
            <Botao variante="secundario" onClick={() => fechar()}>
              Cancelar
            </Botao>
            <Botao
              type="submit"
              form="form-cadastrar"
              carregando={cadastrar.isPending}
              disabled={opcoes.carregando || !!opcoes.erro}
            >
              Adicionar colaborador
            </Botao>
          </>
        )
      }
    >
      <div className="flex flex-col gap-6">
        <div className="flex flex-col gap-2">
          <Abas
            rotulo="Como cadastrar"
            itens={[
              { valor: 'um', rotulo: 'Cadastrar um colaborador' },
              { valor: 'planilha', rotulo: 'Importar planilha' },
            ]}
            valor={modo}
            aoMudar={(valor) => {
              importacao.reiniciar()
              setModo(valor === 'planilha' ? 'planilha' : 'um')
            }}
          />
        </div>
        {naPlanilha ? (
          <CorpoDaImportacao importacao={importacao} />
        ) : (
          <>
            <p className="text-corpo text-texto-suave">
              Os campos marcados entram no cadastro. O restante a própria pessoa completa no
              formulário de admissão.
            </p>
            {!!opcoes.erro && (
              <AvisoDeErro erro={opcoes.erro} aoTentarDeNovo={opcoes.tentarDeNovo} />
            )}
            <FormularioComErroDoServidor
              id="form-cadastrar"
              esquema={esquemaDoColaborador}
              valoresIniciais={valoresIniciaisDoCadastro}
              aoEnviar={async (valores) => {
                setDuplicado(null)
                try {
                  const ficha = await cadastrar.mutateAsync({
                    body: montarCorpoDeCadastro(valores, formularioAdmissaoId, incluirNoOnboarding),
                  })
                  await invalidarRota(queryClient, '/api/rh/funcionarios')
                  const aviso: AvisoDeCadastro = {
                    funcionarioId: ficha.id,
                    nome: ficha.nome,
                    emailDoConvite: valores.enviarConvite ? valores.emailCorporativo : null,
                  }
                  fechar({ cadastrado: aviso })
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
              }}
            >
              {(formulario) => (
                <div className="flex flex-col gap-8">
                  <CamposDoColaborador
                    formulario={formulario}
                    opcoes={opcoes}
                    modo="cadastro"
                    duplicado={duplicado}
                  />
                  <fieldset className="flex flex-col gap-3 rounded-cartao bg-superficie-suave p-5">
                    <legend className="mb-2 text-t3 font-bold text-texto">Depois de salvar</legend>
                    <Controller
                      control={formulario.control}
                      name="enviarConvite"
                      render={({ field }) => (
                        <CaixaDeSelecao
                          rotulo="Enviar convite de acesso ao portal"
                          marcada={field.value}
                          aoMudar={field.onChange}
                        />
                      )}
                    />
                    <CaixaDoFormularioDeAdmissao
                      valor={formularioAdmissaoId}
                      aoMudar={setFormularioAdmissaoId}
                    />
                    <CaixaDoOnboarding
                      marcada={incluirNoOnboarding}
                      aoMudar={setIncluirNoOnboarding}
                    />
                  </fieldset>
                </div>
              )}
            </FormularioComErroDoServidor>
          </>
        )}
      </div>
    </Janela>
  )
}
