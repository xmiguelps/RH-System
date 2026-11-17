import { useSearchParams } from 'react-router'
import { $api } from '../../shared/api/api'
import type { Esquema } from '../../shared/api/tipos'
import { dataPorExtenso, formatarDataDoInstante, formatarDiaMes } from '../../shared/formato/datas'
import { FiltroDeSelecao } from '../../shared/lista/FiltroDeSelecao'
import { inteiroDaUrl } from '../../shared/lista/valoresDeLista'
import { Avatar } from '../../shared/ui/Avatar'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Paginacao } from '../../shared/ui/Paginacao'
import { Selo } from '../../shared/ui/Selo'
import { destinoDaPendencia, OPCOES_DE_TIPO_DE_PENDENCIA, TITULO_DA_PENDENCIA } from './pendencias'

type Tipo = Esquema<'PainelPendenciaTipo'>

const TAMANHO_DA_PAGINA = 10

/** Tela 7.5: "Ver todas as pendências" (`/rh/painel/pendencias`), da mais antiga para a mais nova. */
export function PaginaPendenciasDoRh() {
  const [busca, definirParametros] = useSearchParams()
  const tipo = OPCOES_DE_TIPO_DE_PENDENCIA.find((o) => o.valor === busca.get('tipo'))?.valor
  const pagina = inteiroDaUrl(busca.get('pagina') ?? undefined) ?? 1
  const lista = $api.useQuery('get', '/api/rh/painel/pendencias', {
    params: { query: { ...(tipo ? { tipo } : {}), pagina, tamanhoPagina: TAMANHO_DA_PAGINA } },
  })
  const dados = lista.data

  function mudar(chave: string, valor: string | null) {
    definirParametros((atual) => {
      const novo = new URLSearchParams(atual)
      if (valor) novo.set(chave, valor)
      else novo.delete(chave)
      if (chave !== 'pagina') novo.delete('pagina')
      return novo
    })
  }

  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina titulo="Pendências" subtitulo="O que está esperando o RH" />
      <div className="w-72">
        <FiltroDeSelecao<Tipo>
          rotulo="Tipo"
          todos="Todos os tipos"
          opcoes={OPCOES_DE_TIPO_DE_PENDENCIA}
          valor={tipo}
          aoMudar={(v) => mudar('tipo', v)}
        />
      </div>
      {lista.error ? (
        <AvisoDeErro erro={lista.error} aoTentarDeNovo={() => void lista.refetch()} />
      ) : !dados ? (
        <Esqueleto className="h-40 w-full" />
      ) : dados.itens.length === 0 ? (
        <EstadoVazio titulo="Nada esperando o RH." />
      ) : (
        <ul aria-label="Pendências" className="flex flex-col gap-3">
          {dados.itens.map((p) => (
            <li
              key={`${p.tipo}-${p.referenciaId}`}
              aria-label={`${TITULO_DA_PENDENCIA[p.tipo]}: ${p.funcionario.nome}`}
              className="flex items-center justify-between gap-4 rounded-cartao border border-borda bg-superficie p-5"
            >
              <div className="flex flex-col gap-1">
                <Selo tom="info">{TITULO_DA_PENDENCIA[p.tipo]}</Selo>
                <span className="text-corpo font-semibold text-texto">{p.funcionario.nome}</span>
                <span className="text-tabela text-texto">{p.descricao}</span>
                <span className="text-pequeno text-texto-suave">
                  {/* na admissão, `desdeEmUtc` é a data em que a pessoa começa (só ordena a fila): não é "desde" */}
                  {p.tipo === 'ADMISSAO_COM_DOCUMENTOS_PENDENTES'
                    ? `Começa em ${formatarDiaMes(p.desdeEmUtc)}`
                    : `Desde ${formatarDataDoInstante(p.desdeEmUtc)}`}
                </span>
              </div>
              <BotaoLink
                to={destinoDaPendencia({
                  tipo: p.tipo,
                  referenciaId: p.referenciaId,
                  funcionarioId: p.funcionario.id,
                })}
                variante="secundario"
                tamanho="pequeno"
                aria-label={`Revisar: ${TITULO_DA_PENDENCIA[p.tipo]} de ${p.funcionario.nome}`}
              >
                Revisar
              </BotaoLink>
            </li>
          ))}
        </ul>
      )}
      {dados && dados.total > 0 && (
        <Paginacao dados={dados} aoMudar={(p) => mudar('pagina', p > 1 ? String(p) : null)} />
      )}
    </div>
  )
}

/** Tela 7.6: "Ver quem está sem registro" (`/rh/painel/sem-registro`), em ordem alfabética. */
export function PaginaSemRegistroHoje() {
  const [busca, definirParametros] = useSearchParams()
  const pagina = inteiroDaUrl(busca.get('pagina') ?? undefined) ?? 1
  const lista = $api.useQuery('get', '/api/rh/painel/presenca/sem-registro', {
    params: { query: { pagina, tamanhoPagina: TAMANHO_DA_PAGINA } },
  })
  const painel = $api.useQuery('get', '/api/rh/painel')
  const dados = lista.data
  const semExpediente = painel.data?.presencaAgora.diaUtil === false

  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina titulo="Sem registro de ponto hoje" subtitulo={dataPorExtenso()} />
      {lista.error ? (
        <AvisoDeErro erro={lista.error} aoTentarDeNovo={() => void lista.refetch()} />
      ) : !dados ? (
        <Esqueleto className="h-40 w-full" />
      ) : dados.itens.length === 0 ? (
        <EstadoVazio
          titulo={semExpediente ? 'Hoje não há expediente.' : 'Ninguém sem registro agora.'}
        />
      ) : (
        <ul aria-label="Pessoas sem registro" className="flex flex-col gap-2">
          {dados.itens.map((p) => (
            <li
              key={p.id}
              className="flex items-center gap-3 rounded-cartao border border-borda bg-superficie px-5 py-3"
            >
              <Avatar nome={p.nome} />
              <span className="flex flex-col">
                <span className="text-corpo font-semibold text-texto">{p.nome}</span>
                <span className="text-pequeno text-texto-suave">
                  {[p.cargo, p.setor].filter(Boolean).join(', ')}
                </span>
              </span>
            </li>
          ))}
        </ul>
      )}
      {dados && dados.total > 0 && (
        <Paginacao
          dados={dados}
          aoMudar={(p) =>
            definirParametros((atual) => {
              const novo = new URLSearchParams(atual)
              if (p > 1) novo.set('pagina', String(p))
              else novo.delete('pagina')
              return novo
            })
          }
        />
      )}
    </div>
  )
}
