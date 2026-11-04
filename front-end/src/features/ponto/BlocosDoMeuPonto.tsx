import { useState } from 'react'
import { Link } from 'react-router'
import { $api } from '../../shared/api/api'
import type { Esquema } from '../../shared/api/tipos'
import {
  formatarDataHora,
  formatarDiaMesDaData,
  formatarDiaMesHora,
} from '../../shared/formato/datas'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Paginacao } from '../../shared/ui/Paginacao'
import { Selo } from '../../shared/ui/Selo'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { Saldo } from './PecasDoPonto'
import {
  etiquetaDoMeuPedido,
  rotuloDoTipoDeLancamento,
  sinalDoLancamento,
  periodoDoAtestado,
  respostaDoPedido,
  situacaoDoAtestado,
} from './rotulosDoPonto'

type Pedido = Esquema<'PontoPedidoAjuste'>
type Atestado = Esquema<'Atestado'>

const COLUNAS_DOS_PEDIDOS: readonly ColunaDaTabela<Pedido>[] = [
  { chave: 'criado', titulo: 'Pedido em', celula: (p) => formatarDiaMesHora(p.criadoEmUtc) },
  {
    chave: 'dia',
    titulo: 'Dia',
    celula: (p) => <span className="font-bold">{formatarDiaMesDaData(p.data)}</span>,
  },
  {
    chave: 'justificativa',
    titulo: 'Justificativa e resposta',
    celula: (p) => (
      <span className="flex flex-col gap-2">
        <span>{p.justificativa}</span>
        <span className="rounded-campo bg-superficie-suave px-3 py-2 text-pequeno text-texto-suave">
          {respostaDoPedido(p)}
        </span>
      </span>
    ),
  },
  {
    chave: 'situacao',
    titulo: 'Situação',
    celula: (p) => {
      const { texto, tom } = etiquetaDoMeuPedido(p.status)
      return <Selo tom={tom}>{texto}</Selo>
    },
  },
]

export function MeusPedidosDeAjuste() {
  const [pagina, setPagina] = useState(1)
  const consulta = $api.useQuery('get', '/api/eu/ponto/pedidos-de-ajuste', {
    params: { query: pagina === 1 ? {} : { pagina } },
  })
  const dados = consulta.data
  return (
    <section aria-labelledby="titulo-meus-pedidos" className="flex flex-col gap-3">
      <h2 id="titulo-meus-pedidos" className="text-t3 font-bold text-texto">
        Meus pedidos de ajuste
      </h2>
      <Tabela
        rotulo="Meus pedidos de ajuste"
        colunas={COLUNAS_DOS_PEDIDOS}
        linhas={dados?.itens}
        chaveDaLinha={(p) => p.id}
        carregando={consulta.isFetching && !dados}
        erro={consulta.error ?? undefined}
        aoTentarDeNovo={() => void consulta.refetch()}
        vazio={<EstadoVazio titulo="Você ainda não pediu ajustes." />}
        rodape={
          dados && dados.totalPaginas > 1 ? (
            <Paginacao dados={dados} aoMudar={setPagina} />
          ) : undefined
        }
      />
    </section>
  )
}

/** O bloco "Banco de horas" de Meu ponto: os quatro últimos lançamentos (`tamanhoPagina=4`). */
export function BlocoDoBancoDeHoras() {
  const consulta = $api.useQuery('get', '/api/eu/ponto/banco-de-horas', {
    params: { query: { tamanhoPagina: 4 } },
  })
  const extrato = consulta.data
  return (
    <section
      aria-labelledby="titulo-banco"
      className="flex flex-col gap-4 rounded-cartao border border-borda bg-superficie p-5"
    >
      <div className="flex items-baseline justify-between gap-3">
        <h2 id="titulo-banco" className="text-t3 font-bold text-texto">
          Banco de horas
        </h2>
        {extrato && <Saldo minutos={extrato.saldoMinutos} className="text-t3 font-bold" />}
      </div>
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      ) : !extrato ? (
        <Esqueleto className="h-24 w-full" />
      ) : (
        <>
          {extrato.atualizadoAte === null && (
            <p className="text-pequeno text-texto-suave">Banco de horas ainda não atualizado.</p>
          )}
          {extrato.itens.length === 0 ? (
            <p className="text-corpo text-texto-suave">Nenhum lançamento ainda.</p>
          ) : (
            <ul aria-label="Últimos lançamentos" className="flex flex-col divide-y divide-divisor">
              {extrato.itens.map((l) => (
                <li key={l.id} className="flex items-center justify-between gap-3 py-2">
                  <span className="flex flex-col">
                    <span className="font-bold text-texto">{rotuloDoTipoDeLancamento(l.tipo)}</span>
                    <span className="text-pequeno text-texto-suave">
                      {formatarDiaMesDaData(l.diaReferencia)}, {l.descricao.toLowerCase()}
                    </span>
                  </span>
                  <span
                    className={
                      l.tipo === 'CREDITO'
                        ? 'tabular-nums text-primaria-media'
                        : 'tabular-nums text-alerta-texto'
                    }
                  >
                    {sinalDoLancamento(l)}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
      <Link to="/eu/meu-ponto/extrato" className="text-corpo font-semibold text-primaria">
        Ver extrato completo
      </Link>
    </section>
  )
}

const COLUNAS_DOS_ATESTADOS: readonly ColunaDaTabela<Atestado>[] = [
  { chave: 'periodo', titulo: 'Período', celula: (a) => periodoDoAtestado(a) },
  { chave: 'arquivo', titulo: 'Arquivo', celula: (a) => a.nomeArquivo ?? '—' },
  { chave: 'enviado', titulo: 'Enviado em', celula: (a) => formatarDataHora(a.enviadoEmUtc) },
  {
    chave: 'situacao',
    titulo: 'Situação',
    celula: (a) => {
      const { texto, tom } = situacaoDoAtestado(a.situacao)
      return (
        <span className="flex flex-col gap-1">
          <Selo tom={tom}>{texto}</Selo>
          {a.situacao === 'VALIDADO' && (
            <span className="text-pequeno text-texto-suave">O dia não conta como falta</span>
          )}
          {a.situacao === 'RECUSADO' && (
            <span className="text-pequeno text-texto-suave">
              {a.motivoRecusa ? `${a.motivoRecusa} ` : ''}O dia segue como falta
            </span>
          )}
        </span>
      )
    },
  },
]

/** "Meus atestados": nunca há link para o arquivo aqui. */
export function MeusAtestados() {
  const [pagina, setPagina] = useState(1)
  const consulta = $api.useQuery('get', '/api/eu/ponto/atestados', {
    params: { query: pagina === 1 ? {} : { pagina } },
  })
  const dados = consulta.data
  return (
    <section aria-labelledby="titulo-meus-atestados" className="flex flex-col gap-3">
      <h2 id="titulo-meus-atestados" className="text-t3 font-bold text-texto">
        Meus atestados
      </h2>
      <Tabela
        rotulo="Meus atestados"
        colunas={COLUNAS_DOS_ATESTADOS}
        linhas={dados?.itens}
        chaveDaLinha={(a) => a.id}
        carregando={consulta.isFetching && !dados}
        erro={consulta.error ?? undefined}
        aoTentarDeNovo={() => void consulta.refetch()}
        vazio={<EstadoVazio titulo="Você ainda não enviou atestados." />}
        rodape={
          dados && dados.totalPaginas > 1 ? (
            <Paginacao dados={dados} aoMudar={setPagina} />
          ) : undefined
        }
      />
    </section>
  )
}
