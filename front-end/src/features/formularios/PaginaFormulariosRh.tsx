import { useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { Outlet, useNavigate, useSearchParams } from 'react-router'
import { $api, clienteHttp } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import type { Esquema } from '../../shared/api/tipos'
import { plural } from '../../shared/formato/texto'
import { FiltroDeSelecao } from '../../shared/lista/FiltroDeSelecao'
import { usePainelSobreLista } from '../../shared/lista/usePainelSobreLista'
import { inteiroDaUrl, valorDeEnum } from '../../shared/lista/valoresDeLista'
import { Abas } from '../../shared/ui/Abas'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { CampoDeBusca } from '../../shared/ui/CampoDeBusca'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { JanelaDeConfirmacao } from '../../shared/ui/JanelaDeConfirmacao'
import { MenuDeAcoes } from '../../shared/ui/MenuDeAcoes'
import { Paginacao } from '../../shared/ui/Paginacao'
import { Progresso } from '../../shared/ui/Progresso'
import { Selo } from '../../shared/ui/Selo'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import {
  rotuloDoTipo,
  SITUACAO_DO_FORMULARIO,
  textoDasRespostas,
  textoDoPrazo,
} from './rotulosDosFormularios'

type Item = Esquema<'FormularioRhItem'>
type Tipo = Esquema<'FormularioTipo'>
type Aba = Tipo | 'TODOS'
type Acao = 'encerrar' | 'excluir'
type Confirmacao = { acao: Acao; item: Item; texto: string; comConfirmar: boolean } | null

const TIPOS = ['ENTREVISTA', 'ADMISSAO', 'AVALIACAO', 'PESQUISA', 'OUTRO'] as const
const SITUACOES = ['RASCUNHO', 'AGENDADO', 'PUBLICADO', 'ENCERRADO'] as const
const OPCOES_DE_SITUACAO = SITUACOES.map((s) => ({
  valor: s,
  rotulo: SITUACAO_DO_FORMULARIO[s].texto,
}))

function textoDaConfirmacao(acao: Acao, pendentes: number | null): string {
  if (pendentes === null)
    return acao === 'encerrar'
      ? 'Este formulário é anônimo: o sistema não diz quantas pessoas ainda não responderam. Encerrar mesmo assim?'
      : 'Este formulário é anônimo: o sistema não diz quantas pessoas ainda não responderam. Excluir apaga as respostas. Excluir mesmo assim?'
  const quem = `${pendentes} ${plural(pendentes, 'pessoa ainda não respondeu', 'pessoas ainda não responderam')}.`
  return acao === 'encerrar'
    ? `${quem} Encerrar mesmo assim?`
    : `${quem} Excluir apaga as respostas. Excluir mesmo assim?`
}

/** Tela 16: formulários do RH. O assistente (17 a 20) é a rota filha. */
export function PaginaFormulariosRh() {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const navegar = useNavigate()
  const { paraPainel } = usePainelSobreLista()
  const [parametros, definirParametros] = useSearchParams()
  const tipo = valorDeEnum(parametros.get('tipo') ?? undefined, TIPOS)
  const situacao = valorDeEnum(parametros.get('situacao') ?? undefined, SITUACOES)
  const busca = parametros.get('busca') ?? ''
  const pagina = inteiroDaUrl(parametros.get('pagina') ?? undefined) ?? 1
  const lista = $api.useQuery('get', '/api/rh/formularios', {
    params: {
      query: {
        ...(tipo ? { tipo } : {}),
        ...(situacao ? { situacao } : {}),
        ...(busca ? { busca } : {}),
        ...(pagina > 1 ? { pagina } : {}),
      },
    },
  })
  const [confirmacao, setConfirmacao] = useState<Confirmacao>(null)
  const [executando, setExecutando] = useState(false)
  const [erroDaAcao, setErroDaAcao] = useState<unknown>(null)
  const dados = lista.data
  const c = dados?.contagemPorTipo

  function mudar(chave: string, valor: string | null, substituir = false) {
    definirParametros(
      (atual) => {
        const novo = new URLSearchParams(atual)
        if (valor) novo.set(chave, valor)
        else novo.delete(chave)
        if (chave !== 'pagina') novo.delete('pagina')
        return novo
      },
      { replace: substituir },
    )
  }

  async function executar(acao: Acao, item: Item, confirmar: boolean) {
    const params = {
      path: { formularioId: item.id },
      query: confirmar ? { confirmar: true } : {},
    }
    if (acao === 'encerrar')
      await clienteHttp.POST('/api/rh/formularios/{formularioId}/encerrar', { params })
    else await clienteHttp.DELETE('/api/rh/formularios/{formularioId}', { params })
  }

  async function tentar(acao: Acao, item: Item, confirmar: boolean) {
    setExecutando(true)
    setErroDaAcao(null)
    try {
      await executar(acao, item, confirmar)
      setConfirmacao(null)
      notificacao.sucesso(acao === 'encerrar' ? 'Formulário encerrado.' : 'Formulário excluído.')
      await invalidarRota(queryClient, '/api/rh/formularios')
    } catch (erro) {
      if (
        erro instanceof ErroDeApi &&
        erro.status === 409 &&
        erro.codigo === 'CONFIRMACAO_NECESSARIA'
      ) {
        const pendentes = (erro.corpo?.pendentes as number | null | undefined) ?? null
        setConfirmacao({
          acao,
          item,
          texto: textoDaConfirmacao(acao, pendentes),
          comConfirmar: true,
        })
      } else if (confirmacao) {
        setErroDaAcao(erro)
      } else {
        setConfirmacao({ acao, item, texto: '', comConfirmar: confirmar })
        setErroDaAcao(erro)
      }
    } finally {
      setExecutando(false)
    }
  }

  const colunas: readonly ColunaDaTabela<Item>[] = [
    {
      chave: 'formulario',
      titulo: 'Formulário',
      celula: (f) => (
        <span className="flex flex-col">
          <span className="font-semibold text-texto">{f.titulo}</span>
          <span className="text-pequeno text-texto-suave">
            {rotuloDoTipo(f.tipo, f.sentidoAvaliacao)}
            {f.anonimo ? ', anônimo' : ''}
          </span>
        </span>
      ),
    },
    { chave: 'quem', titulo: 'Quem responde', celula: (f) => f.quemRespondeResumo },
    {
      chave: 'respostas',
      titulo: 'Respostas',
      celula: (f) => {
        const r = textoDasRespostas(f)
        return (
          <span className="flex min-w-32 flex-col gap-1">
            <span className="text-tabela text-texto">{r.texto}</span>
            {r.barra && (
              <Progresso
                valor={r.barra.valor}
                maximo={r.barra.maximo}
                rotulo={`Respostas de ${f.titulo}`}
              />
            )}
          </span>
        )
      },
    },
    { chave: 'prazo', titulo: 'Prazo', celula: (f) => textoDoPrazo(f) },
    {
      chave: 'situacao',
      titulo: 'Situação',
      celula: (f) => (
        <Selo tom={SITUACAO_DO_FORMULARIO[f.situacao].tom}>
          {SITUACAO_DO_FORMULARIO[f.situacao].texto}
        </Selo>
      ),
    },
    {
      chave: 'acoes',
      titulo: 'Ações',
      alinhamento: 'direita',
      celula: (f) => (
        <span className="flex items-center justify-end gap-2">
          {f.situacao === 'ENCERRADO' ? (
            <span
              title="Formulário encerrado não muda"
              className="inline-flex h-9 items-center rounded-botao border border-borda px-3 text-tabela text-texto-fraco"
              aria-disabled="true"
            >
              Editar
            </span>
          ) : (
            <BotaoLink
              variante="secundario"
              tamanho="pequeno"
              to={paraPainel(`${f.id}/editar`)}
              aria-label={`Editar ${f.titulo}`}
            >
              Editar
            </BotaoLink>
          )}
          {f.situacao !== 'RASCUNHO' && (
            <BotaoLink
              tamanho="pequeno"
              to={`/rh/formularios/${f.id}/respondentes`}
              aria-label={`Quem respondeu ${f.titulo}`}
            >
              Quem respondeu
            </BotaoLink>
          )}
          <MenuDeAcoes
            rotulo={`Mais ações de ${f.titulo}`}
            acoes={[
              ...(f.situacao === 'PUBLICADO'
                ? [
                    {
                      chave: 'encerrar',
                      rotulo: 'Encerrar',
                      aoEscolher: () => void tentar('encerrar', f, false),
                    },
                  ]
                : []),
              {
                chave: 'resultado',
                rotulo: 'Ver resultado',
                desabilitada:
                  (f.anonimo && f.situacao !== 'ENCERRADO') || f.situacao === 'RASCUNHO',
                dica:
                  f.anonimo && f.situacao !== 'ENCERRADO'
                    ? 'O resultado do anônimo só aparece depois de encerrar'
                    : undefined,
                aoEscolher: () => navegar(`/rh/formularios/${f.id}/resultado`),
              },
              {
                chave: 'excluir',
                rotulo: 'Excluir',
                perigo: true,
                aoEscolher: () => {
                  setErroDaAcao(null)
                  setConfirmacao({
                    acao: 'excluir',
                    item: f,
                    texto:
                      'Excluir este formulário? As perguntas e as respostas são apagadas. Esta ação não pode ser desfeita.',
                    comConfirmar: false,
                  })
                },
              },
            ]}
          />
        </span>
      ),
    },
  ]

  const vazioComFiltro = !!(busca || situacao || tipo)
  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo="Formulários"
        subtitulo="Entrevistas, admissão, avaliações, pesquisas e sugestões"
        acoes={
          <BotaoLink to={paraPainel('novo')}>
            <Plus aria-hidden="true" className="h-4 w-4" />
            Novo formulário
          </BotaoLink>
        }
      />
      <Abas<Aba>
        rotulo="Tipos de formulário"
        itens={[
          { valor: 'TODOS', rotulo: 'Todos', contagem: c?.todos },
          { valor: 'ENTREVISTA', rotulo: 'Entrevistas', contagem: c?.entrevistas },
          { valor: 'ADMISSAO', rotulo: 'Admissão', contagem: c?.admissoes },
          { valor: 'AVALIACAO', rotulo: 'Avaliações', contagem: c?.avaliacoes },
          { valor: 'PESQUISA', rotulo: 'Pesquisas', contagem: c?.pesquisas },
          { valor: 'OUTRO', rotulo: 'Sugestões', contagem: c?.sugestoes },
        ]}
        valor={tipo ?? 'TODOS'}
        aoMudar={(v) => mudar('tipo', v === 'TODOS' ? null : v)}
      />
      <div className="grid grid-cols-[minmax(0,2fr)_minmax(0,1fr)] items-end gap-4">
        <CampoDeBusca
          rotulo="Buscar formulário"
          placeholder="Buscar formulário"
          valor={busca}
          aoMudar={(texto) => mudar('busca', texto.trim() || null, true)}
        />
        <FiltroDeSelecao
          rotulo="Situação"
          todos="Todas as situações"
          opcoes={OPCOES_DE_SITUACAO}
          valor={situacao}
          aoMudar={(v) => mudar('situacao', v)}
        />
      </div>
      <div className="overflow-x-auto">
        <Tabela
          rotulo="Formulários"
          colunas={colunas}
          linhas={dados?.itens}
          chaveDaLinha={(f) => f.id}
          carregando={lista.isLoading}
          erro={lista.error ?? undefined}
          aoTentarDeNovo={() => void lista.refetch()}
          vazio={
            vazioComFiltro ? (
              <EstadoVazio titulo="Nenhum formulário encontrado" />
            ) : (
              <EstadoVazio
                titulo="Nenhum formulário ainda"
                acao={<BotaoLink to={paraPainel('novo')}>Novo formulário</BotaoLink>}
              />
            )
          }
          rodape={
            dados ? (
              <div className="flex w-full flex-wrap items-center justify-between gap-3">
                <span className="text-tabela text-texto-suave">
                  {dados.total} {plural(dados.total, 'formulário', 'formulários')}
                </span>
                {dados.totalPaginas > 1 && (
                  <Paginacao
                    dados={dados}
                    aoMudar={(p) => mudar('pagina', p > 1 ? String(p) : null)}
                  />
                )}
              </div>
            ) : undefined
          }
        />
      </div>
      <JanelaDeConfirmacao
        aberta={confirmacao !== null}
        titulo={
          confirmacao?.acao === 'encerrar'
            ? 'Encerrar este formulário?'
            : 'Excluir este formulário?'
        }
        descricao={confirmacao?.texto ?? ''}
        rotuloDeConfirmar={confirmacao?.acao === 'encerrar' ? 'Encerrar' : 'Excluir'}
        perigo
        confirmando={executando}
        erro={erroDaAcao ?? undefined}
        aoCancelar={() => setConfirmacao(null)}
        aoConfirmar={() =>
          confirmacao
            ? tentar(confirmacao.acao, confirmacao.item, confirmacao.comConfirmar)
            : undefined
        }
      />
      <Outlet />
    </div>
  )
}
