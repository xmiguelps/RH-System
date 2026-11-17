import type { ReactNode } from 'react'
import { Link } from 'react-router'
import type { Esquema } from '../../shared/api/tipos'
import { formatarDataHora, formatarHora } from '../../shared/formato/datas'
import { formatarNumero } from '../../shared/formato/numeros'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { Cartao } from '../../shared/ui/Cartao'
import { cn } from '../../shared/ui/estilo'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Progresso } from '../../shared/ui/Progresso'
import { destinoDaPendencia, TITULO_DA_PENDENCIA } from './pendencias'

type Painel = Esquema<'PainelRh'>

type Estado = {
  painel: Painel | undefined
  carregando: boolean
  erro: unknown
  aoTentarDeNovo: () => void
}

function Bloco({
  titulo,
  id,
  acao,
  estado,
  altura = 'h-24',
  children,
}: {
  titulo: string
  id: string
  acao?: { texto: string; para: string }
  estado: Estado
  altura?: string
  children: (painel: Painel) => ReactNode
}) {
  return (
    <section aria-labelledby={id} className="flex flex-col gap-3">
      <div className="flex items-baseline justify-between gap-3">
        <h2 id={id} className="text-t3 font-bold text-texto">
          {titulo}
        </h2>
        {acao && (
          <Link to={acao.para} className="text-corpo font-semibold text-primaria">
            {acao.texto}
          </Link>
        )}
      </div>
      {estado.erro ? (
        <AvisoDeErro erro={estado.erro} aoTentarDeNovo={estado.aoTentarDeNovo} />
      ) : !estado.painel ? (
        <Esqueleto className={cn(altura, 'w-full')} />
      ) : (
        children(estado.painel)
      )}
    </section>
  )
}

/** "Precisa da sua atenção": um cartão por tipo com quantidade maior que zero, na ordem da API. */
export function BlocoDePendencias({ estado }: { estado: Estado }) {
  return (
    <Bloco
      titulo="Precisa da sua atenção"
      id="titulo-atencao"
      acao={{ texto: 'Ver todas as pendências', para: '/rh/painel/pendencias' }}
      estado={estado}
    >
      {(p) => {
        const cartoes = p.pendencias.filter((x) => x.quantidade > 0)
        if (cartoes.length === 0)
          return <p className="text-corpo text-texto-suave">Nada esperando revisão.</p>
        return (
          <ul className="grid grid-cols-2 gap-4">
            {cartoes.map((x) => (
              <li
                key={x.tipo}
                aria-label={TITULO_DA_PENDENCIA[x.tipo]}
                className="flex items-center justify-between gap-4 rounded-cartao border border-borda bg-superficie p-5"
              >
                <div className="flex items-center gap-4">
                  <span className="text-numero font-bold text-texto">
                    {formatarNumero(x.quantidade)}
                  </span>
                  <span className="flex flex-col">
                    <span className="text-corpo font-semibold text-texto">
                      {TITULO_DA_PENDENCIA[x.tipo]}
                    </span>
                    {x.resumo && <span className="text-tabela text-texto-suave">{x.resumo}</span>}
                  </span>
                </div>
                <BotaoLink
                  to={destinoDaPendencia(x)}
                  variante="secundario"
                  tamanho="pequeno"
                  aria-label={`Revisar: ${TITULO_DA_PENDENCIA[x.tipo]}`}
                >
                  Revisar
                </BotaoLink>
              </li>
            ))}
          </ul>
        )
      }}
    </Bloco>
  )
}

const MES_CURTO = [
  'jan',
  'fev',
  'mar',
  'abr',
  'mai',
  'jun',
  'jul',
  'ago',
  'set',
  'out',
  'nov',
  'dez',
]

/** "Próximos dias": o dia grande, o mês abreviado, o rótulo e o título (feriados e aniversários). */
export function BlocoDeProximosDias({ estado }: { estado: Estado }) {
  return (
    <Bloco titulo="Próximos dias" id="titulo-proximos-dias" estado={estado} altura="h-40">
      {(p) =>
        p.proximosDias.length === 0 ? (
          <p className="text-corpo text-texto-suave">Nada marcado para os próximos dias.</p>
        ) : (
          <ul className="flex flex-col gap-3 rounded-cartao border border-borda bg-superficie p-5">
            {p.proximosDias.map((d) => (
              <li key={`${d.data}-${d.titulo}`} className="flex items-center gap-4">
                <span className="flex w-12 flex-col items-center rounded-campo bg-superficie-destaque py-1">
                  <span className="text-corpo font-bold text-texto">{d.data.slice(8, 10)}</span>
                  <span className="text-pequeno text-texto-suave">
                    {MES_CURTO[Number(d.data.slice(5, 7)) - 1]}
                  </span>
                </span>
                <span className="flex flex-col">
                  <span className="text-pequeno text-texto-suave">{d.rotulo}</span>
                  <span className="text-corpo text-texto">{d.titulo}</span>
                </span>
              </li>
            ))}
          </ul>
        )
      }
    </Bloco>
  )
}

/** "Treinamentos em andamento": o texto "N de M concluíram" e uma barra proporcional. */
export function BlocoDeTreinamentos({ estado }: { estado: Estado }) {
  return (
    <Bloco
      titulo="Treinamentos em andamento"
      id="titulo-treinamentos"
      acao={{ texto: 'Gerenciar treinamentos', para: '/rh/treinamentos' }}
      estado={estado}
      altura="h-32"
    >
      {(p) =>
        p.treinamentosEmAndamento.length === 0 ? (
          <p className="text-corpo text-texto-suave">Nenhum treinamento em andamento.</p>
        ) : (
          <ul className="flex flex-col gap-4 rounded-cartao border border-borda bg-superficie p-5">
            {p.treinamentosEmAndamento.map((t) => (
              <li key={t.treinamentoId} className="flex flex-col gap-1">
                <div className="flex items-baseline justify-between gap-3">
                  <span className="text-corpo font-semibold text-texto">{t.titulo}</span>
                  <span className="text-tabela text-texto-suave">
                    {t.concluiram} de {t.total} concluíram
                  </span>
                </div>
                <Progresso
                  valor={t.concluiram}
                  maximo={Math.max(t.total, 1)}
                  rotulo={`Conclusão de ${t.titulo}`}
                />
              </li>
            ))}
          </ul>
        )
      }
    </Bloco>
  )
}

const SEGMENTOS = [
  {
    chave: 'trabalhando',
    rotulo: 'Trabalhando',
    cor: 'bg-primaria',
    preenchimento: 'fill-primaria',
  },
  {
    chave: 'ferias',
    rotulo: 'Férias',
    cor: 'bg-primaria-media',
    preenchimento: 'fill-primaria-media',
  },
  {
    chave: 'atestado',
    rotulo: 'Atestado',
    cor: 'bg-primaria-clara',
    preenchimento: 'fill-primaria-clara',
  },
  {
    chave: 'semRegistro',
    rotulo: 'Sem registro',
    cor: 'bg-alerta-ponto',
    preenchimento: 'fill-alerta-ponto',
  },
  {
    chave: 'aindaNaoComecou',
    rotulo: 'Ainda não começou',
    cor: 'bg-borda',
    preenchimento: 'fill-borda',
  },
] as const

/** A barra de presença: um trecho por situação, na ordem, de largura proporcional (SVG: a política do site não aceita `style=`). */
function BarraDePresenca({
  contagens,
  total,
}: {
  contagens: Esquema<'PresencaContagens'>
  total: number
}) {
  const trechos = SEGMENTOS.filter((s) => contagens[s.chave] > 0).map((s) => ({
    ...s,
    largura: (contagens[s.chave] / total) * 100,
  }))
  return (
    <svg
      role="img"
      aria-label="Distribuição da presença hoje"
      viewBox="0 0 100 3"
      preserveAspectRatio="none"
      className="h-3 w-full overflow-hidden rounded-full bg-borda"
    >
      {trechos.map((t, i) => (
        <rect
          key={t.chave}
          data-segmento={t.chave}
          x={trechos.slice(0, i).reduce((soma, anterior) => soma + anterior.largura, 0)}
          y={0}
          width={t.largura}
          height={3}
          className={t.preenchimento}
        />
      ))}
    </svg>
  )
}

/** O cartão "Presença agora" (`CONVENCOES.md`, seção 10): o número de quem registrou entrada e a barra por situação. */
export function CartaoPresencaAgora({ estado }: { estado: Estado }) {
  const p = estado.painel?.presencaAgora
  return (
    <Cartao tom="destaque" aria-label="Presença agora" className="flex flex-col gap-4">
      <div className="flex items-baseline justify-between gap-3">
        <h2 className="text-t3 font-bold text-texto">Presença agora</h2>
        {p && (
          <span className="text-tabela text-texto-suave">
            Atualizado às {formatarHora(p.atualizadoEmUtc).replace(/^0/, '')}
          </span>
        )}
      </div>
      {estado.erro ? (
        <AvisoDeErro erro={estado.erro} aoTentarDeNovo={estado.aoTentarDeNovo} />
      ) : !p ? (
        <Esqueleto className="h-24 w-full" />
      ) : !p.diaUtil ? (
        <div className="flex flex-col gap-1">
          <span className="text-corpo font-semibold text-texto">Hoje não há expediente</span>
          {p.contagensHoje.trabalhando > 0 && (
            <span className="text-corpo text-texto">
              {p.contagensHoje.trabalhando}{' '}
              {p.contagensHoje.trabalhando === 1
                ? 'colaborador registrou'
                : 'colaboradores registraram'}{' '}
              entrada hoje
            </span>
          )}
        </div>
      ) : (
        <>
          <div className="flex items-baseline gap-3">
            <span className="text-numero font-bold text-texto">{p.contagensHoje.trabalhando}</span>
            <span className="text-corpo text-texto">
              de {formatarNumero(p.total)} colaboradores registraram entrada hoje
            </span>
          </div>
          {p.total > 0 && <BarraDePresenca contagens={p.contagensHoje} total={p.total} />}
          <ul
            aria-label="Legenda"
            className="flex flex-wrap gap-x-5 gap-y-1 text-tabela text-texto"
          >
            {SEGMENTOS.filter(
              (s) => s.chave !== 'aindaNaoComecou' || p.contagensHoje.aindaNaoComecou > 0,
            ).map((s) => (
              <li key={s.chave} className="flex items-center gap-2">
                <span
                  aria-hidden="true"
                  className={cn('inline-block h-3 w-3 rounded-full', s.cor)}
                />
                {s.rotulo} {p.contagensHoje[s.chave]}
              </li>
            ))}
          </ul>
          {p.contagensHoje.semRegistro > 0 && (
            <Link to="/rh/painel/sem-registro" className="text-corpo font-semibold text-primaria">
              Ver quem está sem registro
            </Link>
          )}
        </>
      )}
    </Cartao>
  )
}

/** "Últimas alterações": o que a API manda, como texto (nunca como HTML). */
export function BlocoDeAlteracoes({ estado }: { estado: Estado }) {
  return (
    <Bloco
      titulo="Últimas alterações"
      id="titulo-alteracoes"
      acao={{ texto: 'Abrir log de auditoria', para: '/rh/auditoria' }}
      estado={estado}
      altura="h-32"
    >
      {(p) =>
        p.ultimasAlteracoes.length === 0 ? (
          <p className="text-corpo text-texto-suave">Nenhuma alteração por enquanto.</p>
        ) : (
          <table
            aria-label="Últimas alterações"
            className="w-full border-collapse overflow-hidden rounded-cartao border border-borda bg-superficie text-left"
          >
            <thead className="bg-superficie-suave">
              <tr>
                <th
                  scope="col"
                  className="h-[45px] px-[18px] text-tabela font-medium text-texto-suave"
                >
                  Quando
                </th>
                <th
                  scope="col"
                  className="h-[45px] px-[18px] text-tabela font-medium text-texto-suave"
                >
                  Quem
                </th>
                <th
                  scope="col"
                  className="h-[45px] px-[18px] text-tabela font-medium text-texto-suave"
                >
                  O que aconteceu
                </th>
              </tr>
            </thead>
            <tbody>
              {p.ultimasAlteracoes.map((a) => (
                <tr key={`${a.quandoUtc}-${a.descricao}`} className="border-t border-divisor">
                  <td className="px-[18px] py-3 text-tabela text-texto">
                    {formatarDataHora(a.quandoUtc)}
                  </td>
                  <td className="px-[18px] py-3 text-tabela text-texto">{a.autor}</td>
                  <td className="px-[18px] py-3 text-tabela text-texto">{a.descricao}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )
      }
    </Bloco>
  )
}
