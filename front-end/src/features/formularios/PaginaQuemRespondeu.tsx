import { useState } from 'react'
import { Link, Outlet, useNavigate, useParams, useSearchParams } from 'react-router'
import { $api, clienteHttp } from '../../shared/api/api'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { baixarExportacao } from '../../shared/arquivos/baixar'
import { useBaixarPorLink } from '../../shared/arquivos/useBaixarPorLink'
import {
  formatarDataDoInstante,
  formatarDataHora,
  formatarDiaMes,
  hojeEmSaoPaulo,
} from '../../shared/formato/datas'
import { plural } from '../../shared/formato/texto'
import { FiltroDeSelecao } from '../../shared/lista/FiltroDeSelecao'
import { inteiroDaUrl, valorDeEnum } from '../../shared/lista/valoresDeLista'
import { Abas } from '../../shared/ui/Abas'
import { Avatar } from '../../shared/ui/Avatar'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CampoDeBusca } from '../../shared/ui/CampoDeBusca'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Paginacao } from '../../shared/ui/Paginacao'
import { Selo } from '../../shared/ui/Selo'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { NumerosDoPonto } from '../ponto/PecasDoPonto'
import { rotuloDoTipo, SITUACAO_DO_FORMULARIO } from './rotulosDosFormularios'

type Formulario = Esquema<'FormularioDetalheRh'>
type Respondente = Esquema<'FormularioRespondenteRh'>
type Candidato = Esquema<'CandidatoResumo'>
type Aba = 'TODOS' | Esquema<'FormularioRespondenteSituacao'>

const SITUACOES = ['RESPONDIDO', 'NAO_RESPONDEU'] as const
const ATE_ENCERRAR = 'disponível ao encerrar'

function diaDaResposta(instante: string | null): string {
  if (!instante) return '—'
  const completa = formatarDataDoInstante(instante)
  return completa.slice(6) === hojeEmSaoPaulo().slice(0, 4) ? formatarDiaMes(instante) : completa
}

function useAcoesDoCabecalho(formulario: Formulario) {
  const notificacao = useNotificacao()
  const [baixando, setBaixando] = useState(false)
  async function baixarRespostas() {
    if (baixando) return
    setBaixando(true)
    try {
      await baixarExportacao(
        clienteHttp.GET('/api/rh/formularios/{formularioId}/exportacao', {
          params: { path: { formularioId: formulario.id } },
          parseAs: 'blob',
        }),
        `respostas-formulario-${formulario.id}.csv`,
      )
    } catch (erro) {
      notificacao.erro(mensagemDeErro(erro))
    } finally {
      setBaixando(false)
    }
  }
  return { baixarRespostas, baixando }
}

function textoDoLembrete(r: Esquema<'FormularioLembreteResultado'>): string {
  if (r.anonimo || r.enviados === null) return 'Lembrete enviado.'
  const base = `Lembrete enviado a ${r.enviados} ${plural(r.enviados, 'pessoa', 'pessoas')}.`
  return r.ignorados
    ? `${base} ${r.ignorados} já tinham respondido ou receberam um lembrete hoje.`
    : base
}

function ListaDeRespondentes({ formulario }: { formulario: Formulario }) {
  const navegar = useNavigate()
  const notificacao = useNotificacao()
  const [parametros, definirParametros] = useSearchParams()
  const situacao = valorDeEnum(parametros.get('situacao') ?? undefined, SITUACOES)
  const busca = parametros.get('busca') ?? ''
  const setorId = inteiroDaUrl(parametros.get('setorId') ?? undefined)
  const pagina = inteiroDaUrl(parametros.get('pagina') ?? undefined) ?? 1
  const consulta = $api.useQuery('get', '/api/rh/formularios/{formularioId}/respondentes', {
    params: {
      path: { formularioId: formulario.id },
      query: {
        ...(situacao ? { situacao } : {}),
        ...(busca ? { busca } : {}),
        ...(setorId ? { setorId } : {}),
        ...(pagina > 1 ? { pagina } : {}),
      },
    },
  })
  const setores = $api.useQuery(
    'get',
    '/api/rh/setores',
    { params: { query: { pagina: 1, tamanhoPagina: 100 } } },
    { enabled: !formulario.anonimo },
  )
  const lembrar = $api.useMutation('post', '/api/rh/formularios/{formularioId}/lembretes')
  const dados = consulta.data
  const anonimo = dados?.anonimo ?? formulario.anonimo
  const ehSugestao = formulario.tipo === 'OUTRO'
  const ehAvaliacao = formulario.tipo === 'AVALIACAO'
  const publicado = formulario.situacao === 'PUBLICADO'
  const r = dados?.resumo

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

  async function lembrarDe(funcionarioIds: number[]) {
    if (lembrar.isPending) return
    try {
      const resultado = await lembrar.mutateAsync({
        params: { path: { formularioId: formulario.id } },
        body: { funcionarioIds },
      })
      if (resultado) notificacao.sucesso(textoDoLembrete(resultado))
    } catch (erro) {
      notificacao.erro(mensagemDeErro(erro))
    }
  }

  function abrirRespostas(linha: Respondente) {
    const consultaDoModal = new URLSearchParams()
    if (linha.avaliado) consultaDoModal.set('avaliadoId', String(linha.avaliado.id))
    if (ehSugestao && linha.respondidoEmUtc)
      consultaDoModal.set('respondidoEmUtc', linha.respondidoEmUtc)
    const texto = consultaDoModal.toString()
    navegar({ pathname: `funcionarios/${linha.funcionario.id}`, search: texto ? `?${texto}` : '' })
  }

  const numero = (n: number | null | undefined) => (n === null || n === undefined ? '—' : String(n))
  const cartoes = ehSugestao
    ? [{ rotulo: 'Sugestões recebidas', valor: numero(r?.respondidos) }]
    : anonimo && formulario.situacao !== 'ENCERRADO'
      ? [
          { rotulo: 'Esperados', valor: numero(r?.esperados) },
          { rotulo: 'Responderam', valor: ATE_ENCERRAR },
          { rotulo: 'Faltam responder', valor: ATE_ENCERRAR },
          { rotulo: 'Taxa de resposta', valor: ATE_ENCERRAR },
        ]
      : [
          { rotulo: 'Responderam', valor: numero(r?.respondidos) },
          { rotulo: 'Faltam responder', valor: numero(r?.faltam) },
          {
            rotulo: 'Taxa de resposta',
            valor: r?.taxaDeRespostaPercentual == null ? '—' : `${r.taxaDeRespostaPercentual}%`,
          },
        ]

  const colunas: ColunaDaTabela<Respondente>[] = [
    {
      chave: 'pessoa',
      titulo: 'Pessoa',
      celula: (l) => (
        <span className="flex items-center gap-3">
          <Avatar nome={l.funcionario.nome} />
          <span className="text-texto">{l.funcionario.nome}</span>
        </span>
      ),
    },
  ]
  if (ehAvaliacao)
    colunas.push({ chave: 'avaliado', titulo: 'Avaliado', celula: (l) => l.avaliado?.nome ?? '—' })
  colunas.push(
    { chave: 'setor', titulo: 'Setor', celula: (l) => l.funcionario.setor ?? '—' },
    {
      chave: 'situacao',
      titulo: 'Situação',
      celula: (l) =>
        l.situacao === 'RESPONDIDO' ? (
          <Selo tom="sucesso">Respondido</Selo>
        ) : (
          <Selo tom="alerta">Não respondeu</Selo>
        ),
    },
    {
      chave: 'quando',
      titulo: 'Respondido em',
      celula: (l) =>
        ehSugestao && l.respondidoEmUtc
          ? formatarDataHora(l.respondidoEmUtc)
          : diaDaResposta(l.respondidoEmUtc),
    },
    {
      chave: 'acoes',
      titulo: 'Ações',
      alinhamento: 'direita',
      celula: (l) =>
        l.podeVerRespostas ? (
          <Botao
            variante="secundario"
            tamanho="pequeno"
            aria-label={`Ver respostas de ${l.funcionario.nome}`}
            onClick={() => abrirRespostas(l)}
          >
            Ver respostas
          </Botao>
        ) : l.situacao === 'NAO_RESPONDEU' && publicado ? (
          <Botao
            variante="texto"
            tamanho="pequeno"
            aria-label={`Lembrar ${l.funcionario.nome}`}
            onClick={() => void lembrarDe([l.funcionario.id])}
          >
            Lembrar
          </Botao>
        ) : null,
    },
  )

  return (
    <div className="flex flex-col gap-5">
      {publicado && !ehSugestao && (
        <div className="flex justify-end">
          <Botao
            variante="secundario"
            onClick={() => void lembrarDe([])}
            carregando={lembrar.isPending}
          >
            Lembrar quem falta por e-mail
          </Botao>
        </div>
      )}
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      ) : (
        <NumerosDoPonto rotulo="Participação" carregando={!dados} itens={cartoes} />
      )}
      {!anonimo && (
        <>
          <div className="flex flex-wrap items-end justify-between gap-4">
            {!ehSugestao && (
              <Abas<Aba>
                rotulo="Situação de quem responde"
                itens={[
                  { valor: 'TODOS', rotulo: 'Todos' },
                  { valor: 'RESPONDIDO', rotulo: 'Responderam' },
                  { valor: 'NAO_RESPONDEU', rotulo: 'Faltam' },
                ]}
                valor={situacao ?? 'TODOS'}
                aoMudar={(v) => mudar('situacao', v === 'TODOS' ? null : v)}
              />
            )}
            <div className="flex items-end gap-3">
              <div className="w-64">
                <CampoDeBusca
                  rotulo="Buscar pessoa"
                  placeholder="Buscar pessoa"
                  valor={busca}
                  aoMudar={(t) => mudar('busca', t.trim() || null, true)}
                />
              </div>
              <div className="w-56">
                <FiltroDeSelecao
                  rotulo="Setor"
                  todos="Todos os setores"
                  opcoes={(setores.data?.itens ?? []).map((s) => ({
                    valor: String(s.id),
                    rotulo: s.nome,
                  }))}
                  valor={setorId ? String(setorId) : undefined}
                  aoMudar={(v) => mudar('setorId', v)}
                />
              </div>
            </div>
          </div>
          <Tabela
            rotulo="Quem respondeu"
            colunas={colunas}
            linhas={dados?.itens}
            chaveDaLinha={(l) =>
              `${l.funcionario.id}-${l.avaliado?.id ?? 0}-${l.respondidoEmUtc ?? 'nao'}`
            }
            carregando={consulta.isLoading}
            vazio={
              <EstadoVazio
                titulo={
                  situacao === 'NAO_RESPONDEU'
                    ? 'Ninguém falta responder'
                    : 'Ninguém respondeu ainda'
                }
              />
            }
            rodape={
              dados ? (
                <div className="flex w-full flex-wrap items-center justify-between gap-3">
                  <span className="text-tabela text-texto-suave">
                    Mostrando {dados.itens.length} de {dados.total}
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
        </>
      )}
    </div>
  )
}

function ListaDeCandidatos({ formulario }: { formulario: Formulario }) {
  const navegar = useNavigate()
  const { baixar } = useBaixarPorLink()
  const [busca, setBusca] = useState('')
  const [pagina, setPagina] = useState(1)
  const consulta = $api.useQuery('get', '/api/rh/formularios/{formularioId}/candidatos', {
    params: {
      path: { formularioId: formulario.id },
      query: { ...(busca ? { busca } : {}), ...(pagina > 1 ? { pagina } : {}) },
    },
  })
  const dados = consulta.data
  const colunas: readonly ColunaDaTabela<Candidato>[] = [
    { chave: 'nome', titulo: 'Nome', celula: (c) => c.nome },
    { chave: 'email', titulo: 'E-mail', celula: (c) => c.email },
    { chave: 'telefone', titulo: 'Telefone', celula: (c) => c.telefone ?? '—' },
    {
      chave: 'curriculo',
      titulo: 'Currículo',
      celula: (c) =>
        c.temCurriculo ? (
          <Botao
            variante="texto"
            tamanho="pequeno"
            aria-label={`Baixar currículo de ${c.nome}`}
            onClick={() =>
              void baixar(() =>
                clienteHttp.GET(
                  '/api/rh/formularios/{formularioId}/candidatos/{candidatoId}/curriculo/download',
                  { params: { path: { formularioId: formulario.id, candidatoId: c.id } } },
                ),
              )
            }
          >
            Baixar currículo
          </Botao>
        ) : (
          '—'
        ),
    },
    {
      chave: 'quando',
      titulo: 'Respondido em',
      celula: (c) => formatarDataHora(c.respondidoEmUtc),
    },
    {
      chave: 'acoes',
      titulo: 'Ações',
      alinhamento: 'direita',
      celula: (c) => (
        <Botao
          variante="secundario"
          tamanho="pequeno"
          aria-label={`Ver respostas de ${c.nome}`}
          onClick={() => navegar(`candidatos/${c.id}`)}
        >
          Ver respostas
        </Botao>
      ),
    },
  ]
  return (
    <div className="flex flex-col gap-5">
      <div className="w-80">
        <CampoDeBusca
          rotulo="Buscar candidato"
          placeholder="Nome ou e-mail"
          valor={busca}
          aoMudar={(t) => {
            setPagina(1)
            setBusca(t.trim())
          }}
        />
      </div>
      <Tabela
        rotulo="Candidatos"
        colunas={colunas}
        linhas={dados?.itens}
        chaveDaLinha={(c) => c.id}
        carregando={consulta.isLoading}
        erro={consulta.error ?? undefined}
        aoTentarDeNovo={() => void consulta.refetch()}
        vazio={<EstadoVazio titulo="Nenhum candidato respondeu ainda" />}
        rodape={
          dados && dados.totalPaginas > 1 ? (
            <Paginacao dados={dados} aoMudar={setPagina} />
          ) : undefined
        }
      />
    </div>
  )
}

/** Tela 21: quem respondeu (ou, num formulário por link, a lista de candidatos). */
export function PaginaQuemRespondeu() {
  const formularioId = inteiroDaUrl(useParams().formularioId)
  const consulta = $api.useQuery(
    'get',
    '/api/rh/formularios/{formularioId}',
    { params: { path: { formularioId: formularioId ?? 0 } } },
    { enabled: formularioId !== undefined },
  )
  const formulario = consulta.data
  if (consulta.error)
    return <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
  if (!formulario)
    return (
      <div role="status" aria-label="Carregando o formulário" className="flex flex-col gap-4">
        <Esqueleto className="h-16 w-full" />
        <Esqueleto className="h-60 w-full" />
      </div>
    )
  return <CabecalhoEConteudo formulario={formulario} />
}

function CabecalhoEConteudo({ formulario }: { formulario: Formulario }) {
  const { baixarRespostas, baixando } = useAcoesDoCabecalho(formulario)
  const anonimoAberto = formulario.anonimo && formulario.situacao !== 'ENCERRADO'
  const porLink = formulario.publicacao.alvo === 'LINK'
  const situacao = SITUACAO_DO_FORMULARIO[formulario.situacao]
  const autor = formulario.autor ? `, criado por ${formulario.autor.nome}` : ''
  return (
    <div className="flex flex-col gap-6">
      <nav aria-label="Trilha" className="text-tabela text-texto-suave">
        <Link to="/rh/formularios" className="text-primaria-media hover:underline">
          Formulários
        </Link>
        <span> / {porLink ? 'Candidatos' : 'Quem respondeu'}</span>
      </nav>
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-t1 font-bold text-texto">{formulario.titulo}</h1>
          <p className="text-corpo text-texto-suave">
            {rotuloDoTipo(formulario.tipo, formulario.sentidoAvaliacao)}
            {autor} em {formatarDataDoInstante(formulario.criadoEmUtc)}
          </p>
          <div>
            <Selo tom={situacao.tom}>{situacao.texto}</Selo>
          </div>
        </div>
        <div className="flex flex-wrap gap-3">
          {anonimoAberto ? (
            <Botao variante="secundario" disabled title="Só depois de encerrar">
              Ver resultado
            </Botao>
          ) : (
            <BotaoLink variante="secundario" to={`/rh/formularios/${formulario.id}/resultado`}>
              Ver resultado
            </BotaoLink>
          )}
          <Botao
            variante="secundario"
            onClick={() => void baixarRespostas()}
            carregando={baixando}
            disabled={anonimoAberto}
            title={anonimoAberto ? 'Só depois de encerrar' : undefined}
          >
            Baixar respostas
          </Botao>
        </div>
      </header>
      {porLink ? (
        <>
          {formulario.publicacao.tokenDoLink && (
            <p className="text-tabela text-texto-suave">
              Link: {`${window.location.origin}/formulario/${formulario.publicacao.tokenDoLink}`}
            </p>
          )}
          <ListaDeCandidatos formulario={formulario} />
        </>
      ) : (
        <ListaDeRespondentes formulario={formulario} />
      )}
      <Outlet />
    </div>
  )
}
