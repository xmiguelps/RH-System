import { Plus } from 'lucide-react'
import { Outlet, useSearchParams } from 'react-router'
import { $api } from '../../shared/api/api'
import type { Esquema } from '../../shared/api/tipos'
import { formatarDiaMes } from '../../shared/formato/datas'
import { formatarNumero } from '../../shared/formato/numeros'
import { FiltroDeSelecao } from '../../shared/lista/FiltroDeSelecao'
import { valorDeEnum } from '../../shared/lista/valoresDeLista'
import { Abas } from '../../shared/ui/Abas'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { CampoDeBusca } from '../../shared/ui/CampoDeBusca'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { cn } from '../../shared/ui/estilo'
import { Paginacao } from '../../shared/ui/Paginacao'
import { Progresso } from '../../shared/ui/Progresso'
import { NumerosDoPonto } from '../ponto/PecasDoPonto'
import { DetalheDoAvisoRh } from './DetalheDoAvisoRh'
import { SeloDoTipo } from './PecasDosAvisos'
import { ABAS_DO_RH, linhaDoAvisoRh, OPCOES_DE_TIPO } from './rotulosDosAvisos'

type Situacao = Esquema<'AvisoSituacao'>
type Tipo = Esquema<'AvisoTipo'>
type AvisoRh = Esquema<'AvisoItemRh'>

const SITUACOES = ['ATIVO', 'AGENDADO', 'EXPIRADO', 'RASCUNHO'] as const
const TIPOS = ['SIMPLES', 'DETALHADO'] as const

function CartaoDoAvisoRh({
  aviso,
  selecionado,
  aoEscolher,
}: {
  aviso: AvisoRh
  selecionado: boolean
  aoEscolher: () => void
}) {
  return (
    <li>
      <button
        type="button"
        onClick={aoEscolher}
        aria-pressed={selecionado}
        className={cn(
          'flex w-full flex-col gap-2 rounded-cartao border p-5 text-left focus-visible:outline-2 focus-visible:outline-primaria-media',
          selecionado
            ? 'border-primaria-media bg-superficie-suave'
            : 'border-borda bg-superficie hover:border-primaria-media',
        )}
      >
        <span className="flex flex-wrap items-center gap-2">
          <SeloDoTipo tipo={aviso.tipo} />
          {aviso.fixado && <span className="text-pequeno font-semibold text-primaria">Fixado</span>}
        </span>
        <span className="font-bold text-texto">{aviso.titulo}</span>
        <span className="line-clamp-2 text-tabela text-texto-suave">{aviso.previa}</span>
        <span className="flex flex-wrap items-center justify-between gap-2 text-pequeno text-texto-suave">
          <span>{linhaDoAvisoRh(aviso)}</span>
          {aviso.leitura && (
            <span className="font-semibold text-texto">
              {aviso.leitura.viram} de {aviso.leitura.total} viram
            </span>
          )}
        </span>
        {aviso.leitura && (
          <Progresso
            valor={aviso.leitura.viram}
            maximo={Math.max(aviso.leitura.total, 1)}
            rotulo={`Leitura de ${aviso.titulo}`}
          />
        )}
      </button>
    </li>
  )
}

/** Tela 27: avisos do RH. O aviso escolhido fica em ?aviso=; o painel 28 é a rota filha. */
export function PaginaAvisosRh() {
  const [parametros, definirParametros] = useSearchParams()
  const situacao: Situacao =
    valorDeEnum(parametros.get('situacao') ?? undefined, SITUACOES) ?? 'ATIVO'
  const tipo = valorDeEnum(parametros.get('tipo') ?? undefined, TIPOS)
  const busca = parametros.get('busca') ?? ''
  const pagina = Number(parametros.get('pagina') ?? '1') || 1
  const resumo = $api.useQuery('get', '/api/rh/avisos/resumo')
  const lista = $api.useQuery('get', '/api/rh/avisos', {
    params: {
      query: {
        situacao,
        ...(tipo ? { tipo } : {}),
        ...(busca ? { busca } : {}),
        ...(pagina > 1 ? { pagina } : {}),
      },
    },
  })
  const numeros = resumo.data
  const avisos = lista.data?.itens
  const escolhido = Number(parametros.get('aviso')) || avisos?.[0]?.id

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

  const contagemDaAba: Record<Situacao, number | undefined> = {
    ATIVO: numeros?.contagens.ativos,
    AGENDADO: numeros?.contagens.agendados,
    EXPIRADO: numeros?.contagens.expirados,
    RASCUNHO: numeros?.contagens.rascunhos,
  }

  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo="Avisos"
        subtitulo="Comunicados para a empresa toda"
        acoes={
          <BotaoLink to={{ pathname: 'novo', search: parametros.toString() }}>
            <Plus aria-hidden="true" className="h-4 w-4" />
            Novo aviso
          </BotaoLink>
        }
      />
      {resumo.error ? (
        <AvisoDeErro erro={resumo.error} aoTentarDeNovo={() => void resumo.refetch()} />
      ) : (
        <NumerosDoPonto
          rotulo="Números dos avisos"
          carregando={!numeros}
          itens={[
            { rotulo: 'Ativos', valor: numeros ? formatarNumero(numeros.contagens.ativos) : '—' },
            {
              rotulo: 'Agendados',
              valor: numeros ? formatarNumero(numeros.contagens.agendados) : '—',
            },
            {
              rotulo: 'Leitura média dos ativos',
              valor:
                numeros?.leituraMediaDosAtivosPercentual == null
                  ? '—'
                  : `${numeros.leituraMediaDosAtivosPercentual}%`,
            },
            {
              rotulo: 'Expiram nesta semana',
              valor: numeros ? formatarNumero(numeros.expiramNosProximos7Dias) : '—',
              tom: numeros && numeros.expiramNosProximos7Dias > 0 ? 'alerta' : 'neutro',
              apoio: numeros?.proximoAExpirar
                ? `${numeros.proximoAExpirar.titulo}, em ${formatarDiaMes(numeros.proximoAExpirar.expiraEmUtc)}`
                : undefined,
            },
          ]}
        />
      )}
      <div className="flex flex-wrap items-end justify-between gap-4">
        <Abas<Situacao>
          rotulo="Situação dos avisos"
          itens={ABAS_DO_RH.map((a) => ({ ...a, contagem: contagemDaAba[a.valor] }))}
          valor={situacao}
          aoMudar={(v) => mudar({ situacao: v === 'ATIVO' ? null : v, aviso: null, pagina: null })}
        />
        <div className="flex items-end gap-3">
          <div className="w-72">
            <CampoDeBusca
              rotulo="Buscar aviso"
              placeholder="Buscar aviso"
              valor={busca}
              aoMudar={(texto) =>
                mudar({ busca: texto.trim() || null, aviso: null, pagina: null }, true)
              }
            />
          </div>
          <div className="w-48">
            <FiltroDeSelecao<Tipo>
              rotulo="Tipo"
              todos="Todos os tipos"
              opcoes={OPCOES_DE_TIPO}
              valor={tipo}
              aoMudar={(v) => mudar({ tipo: v, aviso: null, pagina: null })}
            />
          </div>
        </div>
      </div>
      <div className="grid grid-cols-[minmax(0,1fr)_minmax(0,1.1fr)] items-start gap-6">
        <section aria-label="Lista de avisos" className="flex flex-col gap-3">
          {lista.error ? (
            <AvisoDeErro erro={lista.error} aoTentarDeNovo={() => void lista.refetch()} />
          ) : !avisos ? (
            <div role="status" aria-label="Carregando os avisos" className="flex flex-col gap-3">
              <Esqueleto className="h-28 w-full" />
              <Esqueleto className="h-28 w-full" />
            </div>
          ) : avisos.length === 0 ? (
            <EstadoVazio
              titulo={busca ? 'Nenhum aviso encontrado para esta busca.' : 'Nenhum aviso aqui.'}
            />
          ) : (
            <>
              <ul className="flex flex-col gap-3">
                {avisos.map((aviso) => (
                  <CartaoDoAvisoRh
                    key={aviso.id}
                    aviso={aviso}
                    selecionado={aviso.id === escolhido}
                    aoEscolher={() => mudar({ aviso: String(aviso.id) }, true)}
                  />
                ))}
              </ul>
              {lista.data && lista.data.totalPaginas > 1 && (
                <Paginacao
                  dados={lista.data}
                  aoMudar={(p) => mudar({ pagina: p > 1 ? String(p) : null, aviso: null })}
                />
              )}
            </>
          )}
        </section>
        <section
          aria-label="Detalhe do aviso"
          className="rounded-cartao border border-borda bg-superficie p-6"
        >
          {escolhido ? (
            <DetalheDoAvisoRh key={escolhido} avisoId={escolhido} />
          ) : (
            <p className="text-corpo text-texto-suave">Escolha um aviso para ver os detalhes.</p>
          )}
        </section>
      </div>
      <Outlet />
    </div>
  )
}
