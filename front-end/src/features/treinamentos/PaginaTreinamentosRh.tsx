import { useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { Outlet, useNavigate, useSearchParams } from 'react-router'
import { $api, clienteHttp } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { baixarExportacao } from '../../shared/arquivos/baixar'
import { useBaixarPorLink } from '../../shared/arquivos/useBaixarPorLink'
import { formatarData, formatarDiaMesDaData } from '../../shared/formato/datas'
import { formatarNumero } from '../../shared/formato/numeros'
import { plural } from '../../shared/formato/texto'
import { FiltroDeSelecao } from '../../shared/lista/FiltroDeSelecao'
import { usePainelSobreLista } from '../../shared/lista/usePainelSobreLista'
import { inteiroDaUrl, valorDeEnum } from '../../shared/lista/valoresDeLista'
import { Abas } from '../../shared/ui/Abas'
import { Avatar } from '../../shared/ui/Avatar'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
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
import { NumerosDoPonto } from '../ponto/PecasDoPonto'
import {
  diaDoInstante,
  seloDoTreinamento,
  SITUACAO_DA_PESSOA,
  SITUACAO_DO_TREINAMENTO,
  textoDoInicio,
} from './rotulosDosTreinamentos'

type Resumo = Esquema<'TreinamentoRhResumo'>
type Situacao = Esquema<'TreinamentoSituacao'>
type Participante = Esquema<'TreinamentoParticipante'>
type Aba = Situacao | 'TODOS'
type AbaDaPessoa = Esquema<'TreinamentoParticipanteSituacao'> | 'TODOS'
type Acao = 'encerrar' | 'excluir'
type Confirmacao = { acao: Acao; item: Resumo; texto: string; comConfirmar: boolean } | null

const SITUACOES = ['RASCUNHO', 'AGENDADO', 'EM_ANDAMENTO', 'ENCERRADO'] as const

function textoDaConfirmacao(
  acao: Acao,
  p: { pendentes: number; emAndamento: number; naoIniciaram: number },
) {
  const quem = `${p.pendentes} ${plural(p.pendentes, 'pessoa ainda não concluiu', 'pessoas ainda não concluíram')} este treinamento (${p.emAndamento} em andamento, ${p.naoIniciaram} não ${plural(p.naoIniciaram, 'começou', 'começaram')}).`
  return acao === 'encerrar'
    ? `${quem} Quem já começou continua e termina; quem não começou deixa de poder começar. Encerrar mesmo assim?`
    : `${quem} Excluir apaga o treinamento e o andamento de todos. Excluir mesmo assim?`
}

function textoDaConclusao(t: Resumo): { texto: string; barra?: boolean } {
  if (t.situacao === 'RASCUNHO') return { texto: 'Sem participantes ainda' }
  if (t.obrigatorio)
    return { texto: `${t.concluidos} de ${t.totalParticipantes} concluíram`, barra: true }
  return {
    texto: `${t.totalParticipantes} ${plural(t.totalParticipantes, 'inscrito', 'inscritos')}`,
  }
}

function legendaDoPublico(publico: Esquema<'TreinamentoPublico'> | null): string {
  if (publico === 'TODOS') return 'para todos'
  if (publico === 'SETORES') return 'para setores'
  if (publico === 'PESSOAS') return 'para pessoas escolhidas'
  return ''
}

function QuemFez({ treinamentoId }: { treinamentoId: number }) {
  const notificacao = useNotificacao()
  const { baixar } = useBaixarPorLink()
  const [aba, setAba] = useState<AbaDaPessoa>('TODOS')
  const [setorId, setSetorId] = useState<number | null>(null)
  const [pagina, setPagina] = useState(1)
  const [baixando, setBaixando] = useState(false)
  const filtros = {
    ...(aba !== 'TODOS' ? { situacao: aba } : {}),
    ...(setorId ? { setorId } : {}),
  }
  const consulta = $api.useQuery('get', '/api/rh/treinamentos/{treinamentoId}/participantes', {
    params: {
      path: { treinamentoId },
      query: { ...filtros, tamanhoPagina: 10, ...(pagina > 1 ? { pagina } : {}) },
    },
  })
  const setores = $api.useQuery('get', '/api/rh/setores', {
    params: { query: { pagina: 1, tamanhoPagina: 100 } },
  })
  const dados = consulta.data
  const t = dados?.treinamento
  const c = dados?.contagens

  async function baixarLista() {
    if (baixando) return
    setBaixando(true)
    try {
      await baixarExportacao(
        clienteHttp.GET('/api/rh/treinamentos/{treinamentoId}/participantes/exportacao', {
          params: { path: { treinamentoId }, query: filtros },
          parseAs: 'blob',
        }),
        `quem-fez-treinamento-${treinamentoId}.csv`,
      )
    } catch (erro) {
      notificacao.erro(mensagemDeErro(erro))
    } finally {
      setBaixando(false)
    }
  }

  const colunas: readonly ColunaDaTabela<Participante>[] = [
    {
      chave: 'pessoa',
      titulo: 'Pessoa',
      celula: (p) => (
        <span className="flex items-center gap-3">
          <Avatar nome={p.funcionario.nome} />
          <span className="text-texto">{p.funcionario.nome}</span>
          {!p.funcionario.ativo && <Selo tom="neutro">Inativo</Selo>}
        </span>
      ),
    },
    { chave: 'setor', titulo: 'Setor', celula: (p) => p.funcionario.setor ?? '—' },
    {
      chave: 'situacao',
      titulo: 'Situação',
      celula: (p) => (
        <Selo tom={SITUACAO_DA_PESSOA[p.situacao].tom}>{SITUACAO_DA_PESSOA[p.situacao].texto}</Selo>
      ),
    },
    {
      chave: 'inicio',
      titulo: 'Início',
      celula: (p) => (p.iniciadoEm ? formatarData(p.iniciadoEm) : '—'),
    },
    { chave: 'conclusao', titulo: 'Conclusão', celula: (p) => diaDoInstante(p.concluidoEmUtc) },
    {
      chave: 'certificado',
      titulo: 'Certificado',
      alinhamento: 'direita',
      celula: (p) =>
        p.certificadoDisponivel ? (
          <Botao
            variante="secundario"
            tamanho="pequeno"
            aria-label={`Baixar o certificado de ${p.funcionario.nome}`}
            onClick={() =>
              void baixar(() =>
                clienteHttp.GET(
                  '/api/rh/treinamentos/{treinamentoId}/participantes/{funcionarioId}/certificado/download',
                  { params: { path: { treinamentoId, funcionarioId: p.funcionario.id } } },
                ),
              )
            }
          >
            Baixar
          </Botao>
        ) : (
          '—'
        ),
    },
  ]

  return (
    <section
      aria-labelledby="titulo-quem-fez"
      className="flex flex-col gap-4 border-t border-divisor pt-6"
    >
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <span className="text-pequeno font-semibold uppercase text-texto-suave">Quem fez</span>
          <h2 id="titulo-quem-fez" className="text-t2 font-bold text-texto">
            {t?.titulo ?? 'Treinamento'}
          </h2>
          {t && (
            <p className="text-tabela text-texto-suave">
              {[
                `${t.obrigatorio ? 'Obrigatório' : 'Opcional'} ${legendaDoPublico(t.publico)}`.trim(),
                t.emiteCertificado ? 'com certificado' : null,
                t.comecaEm ? `desde ${formatarDiaMesDaData(t.comecaEm)}` : null,
                t.encerradoEmUtc
                  ? `encerrado em ${diaDoInstante(t.encerradoEmUtc).slice(0, 5)}`
                  : null,
              ]
                .filter(Boolean)
                .join(', ')}
              .
            </p>
          )}
        </div>
        <Botao variante="secundario" onClick={() => void baixarLista()} carregando={baixando}>
          Baixar lista
        </Botao>
      </div>
      <NumerosDoPonto
        rotulo="Andamento do treinamento"
        carregando={!c}
        itens={[
          { rotulo: 'Concluíram', valor: c ? formatarNumero(c.concluidos) : '—' },
          { rotulo: 'Em andamento', valor: c ? formatarNumero(c.emAndamento) : '—' },
          {
            rotulo: 'Não iniciaram',
            valor: c ? formatarNumero(c.naoIniciados) : '—',
            tom: c && c.naoIniciados > 0 ? 'alerta' : 'neutro',
          },
        ]}
      />
      <div className="flex flex-wrap items-end justify-between gap-4">
        <Abas<AbaDaPessoa>
          rotulo="Situação das pessoas"
          itens={[
            { valor: 'TODOS', rotulo: 'Todos' },
            { valor: 'CONCLUIDO', rotulo: 'Concluíram' },
            { valor: 'EM_ANDAMENTO', rotulo: 'Em andamento' },
            { valor: 'INSCRITO', rotulo: 'Não iniciaram' },
          ]}
          valor={aba}
          aoMudar={(v) => {
            setPagina(1)
            setAba(v)
          }}
        />
        <div className="w-56">
          <FiltroDeSelecao
            rotulo="Setor"
            todos="Todos os setores"
            opcoes={(setores.data?.itens ?? []).map((s) => ({
              valor: String(s.id),
              rotulo: s.nome,
            }))}
            valor={setorId ? String(setorId) : undefined}
            aoMudar={(v) => {
              setPagina(1)
              setSetorId(v ? Number(v) : null)
            }}
          />
        </div>
      </div>
      <Tabela
        rotulo="Quem fez"
        colunas={colunas}
        linhas={dados?.itens}
        chaveDaLinha={(p) => p.funcionario.id}
        carregando={consulta.isLoading}
        erro={consulta.error ?? undefined}
        aoTentarDeNovo={() => void consulta.refetch()}
        vazio={<EstadoVazio titulo="Ninguém nesta situação" />}
        rodape={
          dados ? (
            <div className="flex w-full flex-wrap items-center justify-between gap-3">
              <span className="text-tabela text-texto-suave">
                Mostrando {dados.itens.length} de {dados.total}
              </span>
              {dados.totalPaginas > 1 && <Paginacao dados={dados} aoMudar={setPagina} />}
            </div>
          ) : undefined
        }
      />
    </section>
  )
}

/** Tela 22: treinamentos do RH (lista em cima, "Quem fez" embaixo, em ?quemFez=). */
export function PaginaTreinamentosRh() {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const navegar = useNavigate()
  const { paraPainel } = usePainelSobreLista()
  const [parametros, definirParametros] = useSearchParams()
  const situacao = valorDeEnum(parametros.get('situacao') ?? undefined, SITUACOES)
  const busca = parametros.get('busca') ?? ''
  const pagina = inteiroDaUrl(parametros.get('pagina') ?? undefined) ?? 1
  const quemFez = inteiroDaUrl(parametros.get('quemFez') ?? undefined)
  const indicadores = $api.useQuery('get', '/api/rh/treinamentos/resumo')
  const lista = $api.useQuery('get', '/api/rh/treinamentos', {
    params: {
      query: {
        ...(situacao ? { situacao } : {}),
        ...(busca ? { busca } : {}),
        ...(pagina > 1 ? { pagina } : {}),
      },
    },
  })
  const [confirmacao, setConfirmacao] = useState<Confirmacao>(null)
  const [executando, setExecutando] = useState(false)
  const [erroDaAcao, setErroDaAcao] = useState<unknown>(null)
  const i = indicadores.data
  const dados = lista.data
  const c = dados?.contagens

  function mudar(valores: Record<string, string | null>, substituir = false) {
    definirParametros(
      (atual) => {
        const novo = new URLSearchParams(atual)
        for (const [chave, valor] of Object.entries(valores)) {
          if (valor) novo.set(chave, valor)
          else novo.delete(chave)
        }
        return novo
      },
      { replace: substituir },
    )
  }

  async function tentar(acao: Acao, item: Resumo, confirmar: boolean) {
    setExecutando(true)
    setErroDaAcao(null)
    const params = { path: { treinamentoId: item.id }, query: confirmar ? { confirmar: true } : {} }
    try {
      if (acao === 'encerrar')
        await clienteHttp.POST('/api/rh/treinamentos/{treinamentoId}/encerrar', { params })
      else await clienteHttp.DELETE('/api/rh/treinamentos/{treinamentoId}', { params })
      setConfirmacao(null)
      notificacao.sucesso(
        acao === 'encerrar'
          ? 'Treinamento encerrado. Quem já começou ainda pode terminar.'
          : 'Treinamento excluído.',
      )
      if (acao === 'excluir' && quemFez === item.id) mudar({ quemFez: null })
      await invalidarRota(queryClient, '/api/rh/treinamentos')
    } catch (erro) {
      if (
        erro instanceof ErroDeApi &&
        erro.status === 409 &&
        erro.codigo === 'CONFIRMACAO_NECESSARIA'
      ) {
        const corpo = (erro.corpo ?? {}) as {
          pendentes?: number
          emAndamento?: number
          naoIniciaram?: number
        }
        setConfirmacao({
          acao,
          item,
          comConfirmar: true,
          texto: textoDaConfirmacao(acao, {
            pendentes: corpo.pendentes ?? 0,
            emAndamento: corpo.emAndamento ?? 0,
            naoIniciaram: corpo.naoIniciaram ?? 0,
          }),
        })
      } else {
        setConfirmacao((atual) => atual ?? { acao, item, texto: '', comConfirmar: confirmar })
        setErroDaAcao(erro)
      }
    } finally {
      setExecutando(false)
    }
  }

  const colunas: readonly ColunaDaTabela<Resumo>[] = [
    {
      chave: 'treinamento',
      titulo: 'Treinamento',
      celula: (t) => (
        <span className="flex flex-col">
          <span className="font-semibold text-texto">{t.titulo}</span>
          <span className="text-pequeno text-texto-suave">
            {t.totalModulos} {plural(t.totalModulos, 'módulo', 'módulos')}
          </span>
        </span>
      ),
    },
    { chave: 'comeca', titulo: 'Começa em', celula: (t) => textoDoInicio(t) },
    {
      chave: 'obrigatoriedade',
      titulo: 'Obrigatoriedade',
      celula: (t) => <Selo tom={t.obrigatorio ? 'alerta' : 'info'}>{seloDoTreinamento(t)}</Selo>,
    },
    {
      chave: 'conclusao',
      titulo: 'Conclusão',
      celula: (t) => {
        const r = textoDaConclusao(t)
        return (
          <span className="flex min-w-32 flex-col gap-1">
            <span className="text-tabela text-texto">{r.texto}</span>
            {r.barra && (
              <Progresso
                valor={t.concluidos}
                maximo={Math.max(t.totalParticipantes, 1)}
                rotulo={`Conclusão de ${t.titulo}`}
              />
            )}
          </span>
        )
      },
    },
    {
      chave: 'situacao',
      titulo: 'Situação',
      celula: (t) => (
        <Selo tom={SITUACAO_DO_TREINAMENTO[t.situacao].tom}>
          {SITUACAO_DO_TREINAMENTO[t.situacao].texto}
        </Selo>
      ),
    },
    {
      chave: 'acoes',
      titulo: 'Ações',
      alinhamento: 'direita',
      celula: (t) => (
        <span className="flex items-center justify-end gap-2">
          {t.situacao === 'RASCUNHO' ? (
            <BotaoLink
              variante="secundario"
              tamanho="pequeno"
              to={paraPainel(`${t.id}/editar`)}
              aria-label={`Editar ${t.titulo}`}
            >
              Editar
            </BotaoLink>
          ) : (
            <Botao
              tamanho="pequeno"
              aria-label={`Ver quem fez ${t.titulo}`}
              onClick={() => {
                mudar({ quemFez: String(t.id) }, true)
                requestAnimationFrame(() =>
                  document
                    .getElementById('titulo-quem-fez')
                    ?.scrollIntoView?.({ behavior: 'smooth' }),
                )
              }}
            >
              Ver quem fez
            </Botao>
          )}
          <MenuDeAcoes
            rotulo={`Mais ações de ${t.titulo}`}
            acoes={[
              {
                chave: 'editar',
                rotulo: 'Editar',
                aoEscolher: () => navegar(paraPainel(`${t.id}/editar`)),
              },
              ...(t.situacao === 'EM_ANDAMENTO'
                ? [
                    {
                      chave: 'encerrar',
                      rotulo: 'Encerrar',
                      aoEscolher: () => void tentar('encerrar', t, false),
                    },
                  ]
                : []),
              {
                chave: 'excluir',
                rotulo: 'Excluir',
                perigo: true,
                desabilitada: t.certificadosEmitidos > 0,
                dica:
                  t.certificadosEmitidos > 0
                    ? 'Treinamento com certificado emitido só pode ser encerrado'
                    : undefined,
                aoEscolher: () => {
                  setErroDaAcao(null)
                  setConfirmacao({
                    acao: 'excluir',
                    item: t,
                    texto: `Excluir "${t.titulo}"? Isso não pode ser desfeito.`,
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

  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo="Treinamentos"
        subtitulo="Onboarding, treinamentos obrigatórios e opcionais"
        acoes={
          <BotaoLink to={paraPainel('novo')}>
            <Plus aria-hidden="true" className="h-4 w-4" />
            Novo treinamento
          </BotaoLink>
        }
      />
      {indicadores.error ? (
        <AvisoDeErro erro={indicadores.error} aoTentarDeNovo={() => void indicadores.refetch()} />
      ) : (
        <NumerosDoPonto
          rotulo="Números dos treinamentos"
          carregando={!i}
          itens={[
            { rotulo: 'Em andamento', valor: i ? formatarNumero(i.emAndamento) : '—' },
            { rotulo: 'Obrigatórios', valor: i ? formatarNumero(i.obrigatorios) : '—' },
            {
              rotulo: i ? `Certificados emitidos em ${i.ano}` : 'Certificados emitidos no ano',
              valor: i ? formatarNumero(i.certificadosEmitidosNoAno) : '—',
            },
            {
              rotulo: 'Pessoas com obrigatório pendente',
              valor: i ? formatarNumero(i.pessoasComObrigatorioPendente) : '—',
              tom: i && i.pessoasComObrigatorioPendente > 0 ? 'alerta' : 'neutro',
            },
          ]}
        />
      )}
      <div className="flex flex-wrap items-end justify-between gap-4">
        <Abas<Aba>
          rotulo="Situação dos treinamentos"
          itens={[
            { valor: 'TODOS', rotulo: 'Todos', contagem: c?.todos },
            { valor: 'EM_ANDAMENTO', rotulo: 'Em andamento', contagem: c?.emAndamento },
            { valor: 'AGENDADO', rotulo: 'Agendados', contagem: c?.agendados },
            { valor: 'RASCUNHO', rotulo: 'Rascunhos', contagem: c?.rascunhos },
            { valor: 'ENCERRADO', rotulo: 'Encerrados', contagem: c?.encerrados },
          ]}
          valor={situacao ?? 'TODOS'}
          aoMudar={(v) => mudar({ situacao: v === 'TODOS' ? null : v, pagina: null })}
        />
        <div className="w-72">
          <CampoDeBusca
            rotulo="Buscar treinamento"
            placeholder="Buscar treinamento"
            valor={busca}
            aoMudar={(texto) => mudar({ busca: texto.trim() || null, pagina: null }, true)}
          />
        </div>
      </div>
      <Tabela
        rotulo="Treinamentos"
        colunas={colunas}
        linhas={dados?.itens}
        chaveDaLinha={(t) => t.id}
        carregando={lista.isLoading}
        erro={lista.error ?? undefined}
        aoTentarDeNovo={() => void lista.refetch()}
        vazio={
          busca ? (
            <EstadoVazio titulo="Nenhum treinamento encontrado" />
          ) : (
            <EstadoVazio
              titulo="Nenhum treinamento ainda"
              acao={<BotaoLink to={paraPainel('novo')}>Novo treinamento</BotaoLink>}
            />
          )
        }
        rodape={
          dados && dados.totalPaginas > 1 ? (
            <Paginacao dados={dados} aoMudar={(p) => mudar({ pagina: p > 1 ? String(p) : null })} />
          ) : undefined
        }
      />
      {quemFez ? (
        <QuemFez key={quemFez} treinamentoId={quemFez} />
      ) : (
        <p className="text-corpo text-texto-suave">Escolha um treinamento em "Ver quem fez".</p>
      )}
      <JanelaDeConfirmacao
        aberta={confirmacao !== null}
        titulo={
          confirmacao?.acao === 'encerrar'
            ? 'Encerrar este treinamento?'
            : 'Excluir este treinamento?'
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
