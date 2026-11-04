import { useEffect, useState } from 'react'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { formatarData, formatarDiaMesDaData } from '../../shared/formato/datas'
import { formatarNumero } from '../../shared/formato/numeros'
import { useAuth } from '../autenticacao/useAuth'
import type { PropsDaAbaDaEquipe } from '../equipe/abasDaFichaDaEquipe'
import { Aviso } from '../../shared/ui/Aviso'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Avatar } from '../../shared/ui/Avatar'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { cn } from '../../shared/ui/estilo'
import { Progresso } from '../../shared/ui/Progresso'
import { Selo } from '../../shared/ui/Selo'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { NumerosDoPonto } from '../ponto/PecasDoPonto'
import { diaDoInstante, seloDoTreinamento, SITUACAO_DA_PESSOA } from './rotulosDosTreinamentos'

type DaEquipe = Esquema<'TreinamentoGestorResumo'>
type MeuCartao = Esquema<'TreinamentoMeuCartao'>
type ItemDaFicha = Esquema<'TreinamentoFichaGestorItem'>

function linhaDoCartao(t: DaEquipe): string {
  return t.obrigatorio
    ? `${t.concluiram} de ${t.totalNaEquipe} concluíram`
    : `${t.inscritos} de ${t.totalNaEquipe} inscritos`
}

function PainelDaEquipeNoTreinamento({
  treinamentoId,
  aoSumir,
}: {
  treinamentoId: number
  aoSumir: (id: number, mensagem: string) => void
}) {
  const consulta = $api.useQuery(
    'get',
    '/api/gestor/treinamentos/{treinamentoId}/equipe',
    { params: { path: { treinamentoId } } },
    { retry: false },
  )
  const sumiu = consulta.error instanceof ErroDeApi && consulta.error.status === 404
  const erro = consulta.error
  useEffect(() => {
    if (sumiu) aoSumir(treinamentoId, mensagemDeErro(erro))
  }, [sumiu, erro, aoSumir, treinamentoId])
  const d = consulta.data
  if (sumiu) return null
  if (consulta.error)
    return <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
  if (!d) return <Esqueleto className="h-60 w-full" />
  return (
    <section
      aria-label="Quem fez"
      className="flex flex-col gap-4 rounded-cartao border border-borda bg-superficie p-5"
    >
      <div className="flex flex-col gap-1">
        <span className="text-pequeno font-semibold uppercase text-texto-suave">Quem fez</span>
        <h2 className="text-t3 font-bold text-texto">{d.treinamento.titulo}</h2>
        <p className="text-tabela text-texto-suave">
          {d.treinamento.obrigatorio ? 'Obrigatório' : 'Opcional'}
        </p>
      </div>
      <NumerosDoPonto
        rotulo="Andamento da equipe"
        itens={[
          { rotulo: 'Concluíram', valor: formatarNumero(d.concluiram) },
          {
            rotulo: 'Faltam concluir',
            valor: formatarNumero(d.faltamConcluir),
            tom: d.faltamConcluir > 0 ? 'alerta' : 'neutro',
          },
        ]}
      />
      {d.itens.length === 0 ? (
        <p className="text-corpo text-texto-suave">Ninguém da equipe tem este treinamento ainda</p>
      ) : (
        <ul aria-label="Pessoas da equipe" className="flex flex-col divide-y divide-divisor">
          {d.itens.map((p) => (
            <li key={p.funcionario.id} className="flex items-center justify-between gap-3 py-3">
              <span className="flex items-center gap-3">
                <Avatar nome={p.funcionario.nome} />
                <span className="flex flex-col">
                  <span className="text-corpo text-texto">{p.funcionario.nome}</span>
                  <span className="text-pequeno text-texto-suave">
                    {p.funcionario.cargo}
                    {p.emFeriasAte ? `, de férias até ${formatarDiaMesDaData(p.emFeriasAte)}` : ''}
                  </span>
                </span>
              </span>
              <span className="flex items-center gap-3">
                {p.situacao === 'CONCLUIDO' && p.concluidoEmUtc && (
                  <span className="text-pequeno text-texto-suave">
                    em {diaDoInstante(p.concluidoEmUtc).slice(0, 5)}
                  </span>
                )}
                {p.situacao === 'EM_ANDAMENTO' && p.moduloAtual !== null && (
                  <span className="text-pequeno text-texto-suave">
                    módulo {p.moduloAtual} de {p.totalModulos}
                  </span>
                )}
                <Selo tom={SITUACAO_DA_PESSOA[p.situacao].tom}>
                  {SITUACAO_DA_PESSOA[p.situacao].texto}
                </Selo>
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function SuaParte({ item }: { item: MeuCartao }) {
  return (
    <Aviso
      tom="alerta"
      titulo={`Sua parte: ${item.titulo.charAt(0).toLowerCase()}${item.titulo.slice(1)}`}
      acao={
        <BotaoLink tamanho="pequeno" to={`/eu/treinamentos/${item.id}`}>
          {item.situacao === 'NAO_INICIADO' ? 'Começar treinamento' : 'Continuar treinamento'}
        </BotaoLink>
      }
    >
      {item.moduloAtual !== null
        ? `Você está no módulo ${item.moduloAtual} de ${item.totalModulos}.`
        : `${item.totalModulos} módulos.`}
    </Aviso>
  )
}

/** Tela 58: treinamentos do setor. Sem prazo e sem "Lembrar quem falta". */
export function PaginaTreinamentosDaEquipe() {
  const { usuario } = useAuth()
  const notificacao = useNotificacao()
  const lista = $api.useQuery('get', '/api/gestor/treinamentos')
  const meus = $api.useQuery('get', '/api/eu/treinamentos')
  const [escolhido, setEscolhido] = useState<number | null>(null)
  const [escondidos, setEscondidos] = useState<number[]>([])
  const itens = (lista.data?.itens ?? []).filter((t) => !escondidos.includes(t.id))
  const atual = itens.find((t) => t.id === escolhido) ?? itens[0]
  const suaParte = (meus.data?.itens ?? []).find(
    (t) =>
      t.obrigatorio &&
      !t.encerrado &&
      (t.situacao === 'NAO_INICIADO' || t.situacao === 'EM_ANDAMENTO'),
  )
  const [esconder] = useState(() => (id: number, mensagem: string) => {
    notificacao.erro(mensagem)
    setEscondidos((e) => (e.includes(id) ? e : [...e, id]))
  })
  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo="Treinamentos do setor"
        subtitulo={
          usuario?.setor
            ? `Veja quem do setor ${usuario.setor} já fez cada treinamento`
            : 'Veja quem da sua equipe já fez cada treinamento'
        }
      />
      {suaParte && <SuaParte item={suaParte} />}
      {lista.error ? (
        <AvisoDeErro erro={lista.error} aoTentarDeNovo={() => void lista.refetch()} />
      ) : !lista.data ? (
        <div
          role="status"
          aria-label="Carregando os treinamentos"
          className="grid grid-cols-[1fr_2fr] gap-6"
        >
          <Esqueleto className="h-60" />
          <Esqueleto className="h-60" />
        </div>
      ) : itens.length === 0 ? (
        <EstadoVazio titulo="Nenhum treinamento alcança o seu setor" />
      ) : (
        <div className="grid grid-cols-[minmax(0,1fr)_minmax(0,2fr)] items-start gap-6">
          <section aria-labelledby="titulo-do-setor" className="flex flex-col gap-3">
            <h2 id="titulo-do-setor" className="text-t3 font-bold text-texto">
              Treinamentos do setor
            </h2>
            <ul className="flex flex-col gap-3">
              {itens.map((t) => (
                <li key={t.id}>
                  <button
                    type="button"
                    aria-pressed={t.id === atual?.id}
                    onClick={() => setEscolhido(t.id)}
                    className={cn(
                      'flex w-full flex-col gap-2 rounded-cartao border p-4 text-left',
                      t.id === atual?.id
                        ? 'border-primaria bg-superficie-suave'
                        : 'border-borda bg-superficie',
                    )}
                  >
                    <span className="text-pequeno text-texto-suave">{seloDoTreinamento(t)}</span>
                    <span className="font-semibold text-texto">{t.titulo}</span>
                    <span className="flex justify-between text-pequeno text-texto-suave">
                      <span>{linhaDoCartao(t)}</span>
                      {t.situacao === 'AGENDADO' && t.comecaEm && (
                        <span>Começa em {formatarDiaMesDaData(t.comecaEm)}</span>
                      )}
                    </span>
                    <Progresso
                      valor={t.obrigatorio ? t.concluiram : t.inscritos}
                      maximo={Math.max(t.totalNaEquipe, 1)}
                      rotulo={`Andamento de ${t.titulo}`}
                    />
                  </button>
                </li>
              ))}
            </ul>
          </section>
          {atual && (
            <PainelDaEquipeNoTreinamento
              key={atual.id}
              treinamentoId={atual.id}
              aoSumir={esconder}
            />
          )}
        </div>
      )}
    </div>
  )
}

/** A aba Treinamentos da ficha da equipe (sem print): sem certificado, sem nota, sem data final. */
export function AbaTreinamentosDaEquipe({ ficha }: PropsDaAbaDaEquipe) {
  const consulta = $api.useQuery('get', '/api/gestor/equipe/{funcionarioId}/treinamentos', {
    params: { path: { funcionarioId: ficha.id } },
  })
  const d = consulta.data
  const colunas: readonly ColunaDaTabela<ItemDaFicha>[] = [
    { chave: 'titulo', titulo: 'Treinamento', celula: (t) => t.titulo },
    {
      chave: 'tipo',
      titulo: 'Obrigatoriedade',
      celula: (t) => (
        <Selo tom={t.obrigatorio ? 'alerta' : 'contorno'}>{seloDoTreinamento(t)}</Selo>
      ),
    },
    {
      chave: 'situacao',
      titulo: 'Situação',
      celula: (t) => (
        <Selo tom={SITUACAO_DA_PESSOA[t.situacao].tom}>{SITUACAO_DA_PESSOA[t.situacao].texto}</Selo>
      ),
    },
    {
      chave: 'modulos',
      titulo: 'Módulos',
      celula: (t) => `módulos ${t.modulosConcluidos} de ${t.totalModulos}`,
    },
    {
      chave: 'inicio',
      titulo: 'Início',
      celula: (t) => (t.iniciadoEm ? formatarData(t.iniciadoEm) : '—'),
    },
    { chave: 'conclusao', titulo: 'Conclusão', celula: (t) => diaDoInstante(t.concluidoEmUtc) },
  ]
  return (
    <div className="flex flex-col gap-5">
      <NumerosDoPonto
        rotulo="Números dos treinamentos"
        carregando={!d}
        itens={[
          {
            rotulo: 'Pendentes',
            valor: d ? formatarNumero(d.resumo.pendentes) : '—',
            tom: d && d.resumo.pendentes > 0 ? 'alerta' : 'neutro',
          },
          { rotulo: 'Em andamento', valor: d ? formatarNumero(d.resumo.emAndamento) : '—' },
          { rotulo: 'Concluídos', valor: d ? formatarNumero(d.resumo.concluidos) : '—' },
        ]}
      />
      <Tabela
        rotulo="Treinamentos da pessoa"
        colunas={colunas}
        linhas={d?.itens}
        chaveDaLinha={(t) => t.treinamentoId}
        carregando={consulta.isLoading}
        erro={consulta.error ?? undefined}
        aoTentarDeNovo={() => void consulta.refetch()}
        vazio={<EstadoVazio titulo="Esta pessoa ainda não tem treinamentos" />}
      />
    </div>
  )
}
