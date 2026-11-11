import { keepPreviousData, useQueryClient } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { useAuth } from '../../autenticacao/useAuth'
import { $api } from '../../../shared/api/api'
import { mensagemDeErro } from '../../../shared/api/mensagemDeErro'
import type { Esquema } from '../../../shared/api/tipos'
import { formatarDataHora } from '../../../shared/formato/datas'
import { FiltroDeSelecao } from '../../../shared/lista/FiltroDeSelecao'
import { useEstadoDaLista } from '../../../shared/lista/useEstadoDaLista'
import { valorDeEnum } from '../../../shared/lista/valoresDeLista'
import { Botao } from '../../../shared/ui/Botao'
import { JanelaDeConfirmacao } from '../../../shared/ui/JanelaDeConfirmacao'
import { Selo } from '../../../shared/ui/Selo'
import type { ColunaDaTabela } from '../../../shared/ui/Tabela'
import { useNotificacao } from '../../../shared/ui/useNotificacao'
import { rotuloDoNivelDeAcesso } from '../../colaboradores/rotulos'
import { invalidarCadastros } from '../invalidarCadastros'
import { ListaDeCadastro } from '../ListaDeCadastro'
import { OPCOES_DE_SITUACAO_DO_LOGIN, rotuloDaSituacaoDoLogin } from './rotulos'

type Usuario = Esquema<'UsuarioSistemaItem'>
type Situacao = Esquema<'UsuarioSistemaSituacao'>
const SITUACOES = OPCOES_DE_SITUACAO_DO_LOGIN.map((o) => o.valor)
const ACESSOS = ['true', 'false'] as const

export function CadastroDeUsuarios() {
  const { usuario: logado } = useAuth()
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const lista = useEstadoDaLista({ filtros: ['situacao', 'ativo'] as const })
  const situacao = valorDeEnum<Situacao>(lista.filtros.situacao, SITUACOES)
  const ativo = valorDeEnum(lista.filtros.ativo, ACESSOS)
  const consulta = $api.useQuery(
    'get',
    '/api/rh/usuarios',
    {
      params: {
        query: {
          pagina: lista.consulta.pagina,
          tamanhoPagina: lista.consulta.tamanhoPagina,
          busca: lista.consulta.busca,
          situacao,
          ativo: ativo === undefined ? undefined : ativo === 'true',
        },
      },
    },
    { placeholderData: keepPreviousData },
  )
  const convite = $api.useMutation('post', '/api/rh/usuarios/{funcionarioId}/convite')
  const ativar = $api.useMutation('post', '/api/rh/usuarios/{funcionarioId}/ativar')
  const desativar = $api.useMutation('post', '/api/rh/usuarios/{funcionarioId}/desativar')
  const [desativando, setDesativando] = useState<Usuario | null>(null)
  const [erroDoDialogo, setErroDoDialogo] = useState<unknown>(null)
  const emAndamento = useRef(false)

  const executar = async (acao: () => Promise<unknown>, sucesso?: string) => {
    if (emAndamento.current) return false
    emAndamento.current = true
    try {
      await acao()
      if (sucesso) notificacao.sucesso(sucesso)
      return true
    } catch (erro) {
      notificacao.erro(mensagemDeErro(erro))
      return false
    } finally {
      emAndamento.current = false
      await invalidarCadastros(queryClient, '/api/rh/usuarios')
    }
  }

  const ocupado = convite.isPending || ativar.isPending || desativar.isPending
  const caminho = (u: Usuario) => ({ params: { path: { funcionarioId: u.funcionarioId } } })

  const colunas: ColunaDaTabela<Usuario>[] = [
    {
      chave: 'pessoa',
      titulo: 'Pessoa',
      celula: (u) => (
        <div className="flex flex-col">
          <span className="font-semibold text-texto">{u.nome}</span>
          {u.emailCorporativo && (
            <span className="text-pequeno text-texto-suave">{u.emailCorporativo}</span>
          )}
        </div>
      ),
    },
    { chave: 'nivel', titulo: 'Nível de acesso', celula: (u) => rotuloDoNivelDeAcesso(u.perfil) },
    {
      chave: 'situacao',
      titulo: 'Situação',
      celula: (u) => {
        const { texto, tom } = rotuloDaSituacaoDoLogin(u.situacao)
        return <Selo tom={tom}>{texto}</Selo>
      },
    },
    { chave: 'acesso', titulo: 'Acesso', celula: (u) => (u.ativo ? 'Liberado' : 'Desativado') },
    {
      chave: 'ultimo',
      titulo: 'Último acesso',
      celula: (u) => (u.ultimoLoginEmUtc ? formatarDataHora(u.ultimoLoginEmUtc) : 'Nunca entrou'),
    },
    {
      chave: 'convite',
      titulo: 'Convite enviado em',
      celula: (u) => formatarDataHora(u.conviteEnviadoEmUtc),
    },
    {
      chave: 'acoes',
      titulo: 'Ações',
      alinhamento: 'direita',
      celula: (u) => {
        const proprio = logado?.funcionarioId === u.funcionarioId
        return (
          <div className="flex justify-end gap-2">
            {u.situacao !== 'SENHA_DEFINIDA' && (
              <Botao
                variante="secundario"
                tamanho="pequeno"
                disabled={!u.emailCorporativo || ocupado}
                title={
                  u.emailCorporativo
                    ? undefined
                    : 'Esta pessoa não tem e-mail corporativo cadastrado.'
                }
                onClick={() =>
                  void executar(
                    () => convite.mutateAsync(caminho(u)),
                    `Convite enviado para ${u.emailCorporativo}.`,
                  )
                }
              >
                {u.situacao === 'SEM_CONVITE' ? 'Enviar convite' : 'Reenviar convite'}
              </Botao>
            )}
            {!u.ativo && u.situacao !== 'SEM_CONVITE' && (
              <Botao
                variante="secundario"
                tamanho="pequeno"
                disabled={ocupado}
                onClick={() => void executar(() => ativar.mutateAsync(caminho(u)))}
              >
                Ativar
              </Botao>
            )}
            {u.ativo && (
              <Botao
                variante="perigo"
                tamanho="pequeno"
                disabled={proprio || ocupado}
                title={proprio ? 'Você não pode desativar o seu próprio login.' : undefined}
                onClick={() => {
                  setErroDoDialogo(null)
                  setDesativando(u)
                }}
              >
                Desativar
              </Botao>
            )}
          </div>
        )
      },
    },
  ]

  return (
    <>
      <ListaDeCadastro
        titulo="Usuários do sistema"
        apoio="Logins de acesso ao portal"
        rotuloDaTabela="Usuários do sistema"
        colunas={colunas}
        chaveDaLinha={(u) => u.funcionarioId}
        dados={consulta.data}
        carregando={consulta.isFetching}
        erro={consulta.error}
        aoTentarDeNovo={() => void consulta.refetch()}
        busca={{ valor: lista.busca, aoMudar: lista.definirBusca }}
        aoMudarPagina={lista.definirPagina}
        vazio="Nenhum usuário encontrado."
        vazioComBusca="Nenhum usuário encontrado."
        filtros={
          <>
            <div className="w-[220px]">
              <FiltroDeSelecao
                rotulo="Situação do login"
                todos="Todas as situações"
                opcoes={OPCOES_DE_SITUACAO_DO_LOGIN}
                valor={situacao}
                aoMudar={(v) => lista.definirFiltro('situacao', v)}
              />
            </div>
            <div className="w-[200px]">
              <FiltroDeSelecao
                rotulo="Acesso"
                todos="Todos os acessos"
                opcoes={[
                  { valor: 'true', rotulo: 'Liberados' },
                  { valor: 'false', rotulo: 'Desativados' },
                ]}
                valor={ativo}
                aoMudar={(v) => lista.definirFiltro('ativo', v)}
              />
            </div>
          </>
        }
      />
      <JanelaDeConfirmacao
        aberta={desativando !== null}
        titulo={`Desativar o acesso de ${desativando?.nome ?? ''}?`}
        descricao="O login fica bloqueado e as sessões abertas dela são encerradas. Nada é apagado: 'Ativar' desfaz."
        rotuloDeConfirmar="Desativar"
        perigo
        confirmando={desativar.isPending}
        erro={erroDoDialogo ?? undefined}
        aoCancelar={() => setDesativando(null)}
        aoConfirmar={async () => {
          if (!desativando) return
          try {
            await desativar.mutateAsync(caminho(desativando))
          } catch (erro) {
            setErroDoDialogo(erro)
            await invalidarCadastros(queryClient, '/api/rh/usuarios')
            return
          }
          setDesativando(null)
          await invalidarCadastros(queryClient, '/api/rh/usuarios')
        }}
      />
    </>
  )
}
