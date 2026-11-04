import { Plus } from 'lucide-react'
import { Navigate, Outlet, useNavigate, useParams, useSearchParams } from 'react-router'
import { $api } from '../../shared/api/api'
import type { Esquema } from '../../shared/api/tipos'
import { formatarNumero } from '../../shared/formato/numeros'
import { plural } from '../../shared/formato/texto'
import { FiltroDeSelecao } from '../../shared/lista/FiltroDeSelecao'
import { usePainelSobreLista } from '../../shared/lista/usePainelSobreLista'
import { inteiroDaUrl, valorDeEnum } from '../../shared/lista/valoresDeLista'
import { Abas } from '../../shared/ui/Abas'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { CampoDeBusca } from '../../shared/ui/CampoDeBusca'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Paginacao } from '../../shared/ui/Paginacao'
import { NumerosDoPonto } from '../ponto/PecasDoPonto'
import { PainelDetalheDaAdvertenciaRh } from './DetalhesDaAdvertencia'
import { TabelaDeAdvertencias } from './PecasDasAdvertencias'
import { PainelRegistrarAdvertencia } from './PainelRegistrarAdvertencia'
import { OPCOES_DE_TIPO, SITUACOES, TIPOS } from './rotulosDasAdvertencias'

type Situacao = Esquema<'AdvertenciaSituacao'>
type Aba = Situacao | 'TODAS'

function useVoltarParaALista() {
  const { fechar } = usePainelSobreLista()
  return () => fechar()
}

/** Tela 15 a partir da lista: /rh/advertencias/nova. */
export function RotaNovaAdvertenciaRh() {
  const voltar = useVoltarParaALista()
  return <PainelRegistrarAdvertencia modo="rh" aoFechar={voltar} />
}

/** O detalhe a partir da lista: /rh/advertencias/:advertenciaId. */
export function RotaDetalheDaAdvertenciaRh() {
  const id = inteiroDaUrl(useParams().advertenciaId)
  const voltar = useVoltarParaALista()
  if (id === undefined) return <Navigate to="/rh/advertencias" replace />
  return <PainelDetalheDaAdvertenciaRh advertenciaId={id} aoFechar={voltar} />
}

/** Tela 14: advertências do RH (ano atual, como o PNG). */
export function PaginaAdvertenciasRh() {
  const [parametros, definirParametros] = useSearchParams()
  const { paraPainel } = usePainelSobreLista()
  const navegar = useNavigate()
  const situacao = valorDeEnum(parametros.get('situacao') ?? undefined, SITUACOES)
  const tipo = valorDeEnum(parametros.get('tipo') ?? undefined, TIPOS)
  const setorId = inteiroDaUrl(parametros.get('setorId') ?? undefined)
  const busca = parametros.get('busca') ?? ''
  const pagina = inteiroDaUrl(parametros.get('pagina') ?? undefined) ?? 1
  const resumo = $api.useQuery('get', '/api/rh/advertencias/resumo')
  const lista = $api.useQuery('get', '/api/rh/advertencias', {
    params: {
      query: {
        ...(situacao ? { situacao } : {}),
        ...(tipo ? { tipo } : {}),
        ...(setorId ? { setorId } : {}),
        ...(busca ? { busca } : {}),
        ...(pagina > 1 ? { pagina } : {}),
      },
    },
  })
  const setores = $api.useQuery('get', '/api/rh/setores', {
    params: { query: { pagina: 1, tamanhoPagina: 100 } },
  })
  const numeros = resumo.data
  const dados = lista.data
  const ano = numeros?.ano

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

  const c = numeros?.contagens
  const setor = numeros?.setorComMaisRegistros
  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo="Advertências"
        subtitulo="Registros disciplinares de todos os setores"
        acoes={
          <BotaoLink to={paraPainel('nova')}>
            <Plus aria-hidden="true" className="h-4 w-4" />
            Nova advertência
          </BotaoLink>
        }
      />
      {resumo.error ? (
        <AvisoDeErro erro={resumo.error} aoTentarDeNovo={() => void resumo.refetch()} />
      ) : (
        <NumerosDoPonto
          rotulo="Números das advertências"
          carregando={!numeros}
          itens={[
            {
              rotulo: ano ? `Registradas em ${ano}` : 'Registradas no ano',
              valor: c ? formatarNumero(c.todas) : '—',
            },
            {
              rotulo: 'Aguardando ciência',
              valor: c ? formatarNumero(c.pendentes) : '—',
              tom: c && c.pendentes > 0 ? 'alerta' : 'neutro',
            },
            {
              rotulo: 'Contestadas',
              valor: c ? formatarNumero(c.contestadas) : '—',
              tom: c && c.contestadas > 0 ? 'alerta' : 'neutro',
            },
            {
              rotulo: 'Setor com mais registros',
              valor: setor?.setor ?? '—',
              apoio: setor
                ? `${setor.quantidade} ${plural(setor.quantidade, 'advertência', 'advertências')}`
                : undefined,
            },
          ]}
        />
      )}
      <div className="flex flex-wrap items-end justify-between gap-4">
        <Abas<Aba>
          rotulo="Situação das advertências"
          itens={[
            { valor: 'TODAS', rotulo: 'Todas', contagem: c?.todas },
            { valor: 'PENDENTE', rotulo: 'Aguardando ciência', contagem: c?.pendentes },
            { valor: 'APLICADA', rotulo: 'Ciente', contagem: c?.aplicadas },
            { valor: 'CONTESTADA', rotulo: 'Contestada', contagem: c?.contestadas },
          ]}
          valor={situacao === 'CANCELADA' || !situacao ? 'TODAS' : situacao}
          aoMudar={(v) => mudar('situacao', v === 'TODAS' ? null : v)}
        />
      </div>
      <div className="grid grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1fr)] items-end gap-4">
        <CampoDeBusca
          rotulo="Buscar colaborador ou motivo"
          placeholder="Buscar colaborador ou motivo"
          valor={busca}
          aoMudar={(texto) => mudar('busca', texto.trim() || null, true)}
        />
        <FiltroDeSelecao
          rotulo="Setor"
          todos="Todos os setores"
          opcoes={(setores.data?.itens ?? []).map((s) => ({ valor: String(s.id), rotulo: s.nome }))}
          valor={setorId ? String(setorId) : undefined}
          aoMudar={(v) => mudar('setorId', v)}
          carregando={setores.isLoading}
        />
        <FiltroDeSelecao
          rotulo="Tipo"
          todos="Todos os tipos"
          opcoes={OPCOES_DE_TIPO}
          valor={tipo}
          aoMudar={(v) => mudar('tipo', v)}
        />
      </div>
      <TabelaDeAdvertencias
        rotulo="Advertências"
        linhas={dados?.itens}
        colaborador="com-setor"
        aoAbrir={(a) => navegar(paraPainel(String(a.id)))}
        carregando={lista.isLoading}
        erro={lista.error ?? undefined}
        aoTentarDeNovo={() => void lista.refetch()}
        vazio={
          <EstadoVazio
            titulo={
              busca
                ? 'Nenhuma advertência encontrada para esta busca.'
                : situacao
                  ? 'Nenhuma advertência nesta situação.'
                  : 'Nenhuma advertência encontrada.'
            }
          />
        }
        rodape={
          dados ? (
            <div className="flex w-full flex-wrap items-center justify-between gap-3">
              <span className="text-tabela text-texto-suave">
                Mostrando {dados.itens.length} de {dados.total}
                {ano ? ` em ${ano}` : ''}
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
      <Outlet />
    </div>
  )
}
