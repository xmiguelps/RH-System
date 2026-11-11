import { ChevronRight } from 'lucide-react'
import { useState } from 'react'
import { Link, Outlet } from 'react-router'
import { $api } from '../../shared/api/api'
import type { Esquema } from '../../shared/api/tipos'
import {
  formatarDiaEMes,
  formatarDiaMesDaData,
  formatarDiaMesHora,
  hojeEmSaoPaulo,
} from '../../shared/formato/datas'
import { formatarNumero } from '../../shared/formato/numeros'
import { plural } from '../../shared/formato/texto'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Avatar } from '../../shared/ui/Avatar'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Selo } from '../../shared/ui/Selo'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import {
  ChipsDosPeriodos,
  EtiquetaDoDia,
  Horas,
  NumerosDoPonto,
  Saldo,
  type NumeroDoPonto,
} from './PecasDoPonto'
import { etiquetaDoPedido, rotuloDaSituacaoHoje } from './rotulosDoPonto'

type Item = Esquema<'PontoEquipeItem'>

function SituacaoDaPessoa({ item }: { item: Item }) {
  if (item.situacaoHoje === null)
    return (
      <EtiquetaDoDia
        dia={{ situacao: item.situacaoDoDia, atrasoMinutos: null, nomeFeriado: null }}
      />
    )
  const { texto, tom } = rotuloDaSituacaoHoje(item.situacaoHoje, item.periodoAberto)
  return <Selo tom={tom}>{texto}</Selo>
}

function numeros(dia: Esquema<'PontoEquipeDia'>, ehHoje: boolean): NumeroDoPonto[] {
  const r = dia.resumo
  const semRegistro = r.pessoasSemRegistro
  const ferias = r.pessoasDeFerias
  const apoioSem =
    semRegistro.length === 0
      ? undefined
      : `${semRegistro[0]!.nome}${semRegistro.length > 1 ? ` e mais ${semRegistro.length - 1}` : ''}`
  const apoioFerias =
    ferias.length === 0
      ? undefined
      : `${ferias[0]!.funcionario.nome}, até ${formatarDiaMesDaData(ferias[0]!.ate)}${
          ferias.length > 1 ? ` e mais ${ferias.length - 1}` : ''
        }`
  const itens: NumeroDoPonto[] = []
  if (r.contagensHoje) {
    itens.push({
      rotulo: 'Trabalhando agora',
      valor: formatarNumero(r.contagensHoje.trabalhando),
      apoio: r.diaUtil ? undefined : 'Hoje não há expediente',
    })
  } else {
    itens.push({
      rotulo: 'Com registro',
      valor: formatarNumero(dia.itens.filter((i) => i.periodos.length > 0).length),
    })
  }
  itens.push(
    {
      rotulo: ehHoje ? 'Sem registro hoje' : 'Sem registro',
      valor: formatarNumero(r.contagensHoje?.semRegistro ?? semRegistro.length),
      apoio: apoioSem,
    },
    {
      rotulo: 'De férias',
      valor: formatarNumero(r.contagensHoje?.ferias ?? ferias.length),
      apoio: apoioFerias,
    },
    {
      rotulo: 'Pedidos para analisar',
      valor: formatarNumero(r.pedidosParaAnalisar),
      tom: r.pedidosParaAnalisar > 0 ? 'alerta' : 'neutro',
    },
  )
  return itens
}

/** Tela 54: o ponto do setor no dia, os pedidos que aguardam o gestor e o espelho de cada pessoa. */
export function PaginaPontoDaEquipe() {
  const hoje = hojeEmSaoPaulo()
  const [data, setData] = useState(hoje)
  const ehHoje = data === hoje
  const equipe = $api.useQuery('get', '/api/gestor/equipe')
  const dia = $api.useQuery('get', '/api/gestor/ponto/equipe', {
    params: { query: ehHoje ? {} : { data } },
  })
  const pedidos = $api.useQuery('get', '/api/gestor/ponto/pedidos-de-ajuste', {
    params: {
      query: { status: 'PENDENTE', etapa: 'GESTOR', ordenarPor: 'CRIADO_EM', direcao: 'ASC' },
    },
  })
  const setor = equipe.data?.setor?.nome
  const total = equipe.data?.total

  const colunas: readonly ColunaDaTabela<Item>[] = [
    {
      chave: 'pessoa',
      titulo: 'Pessoa',
      celula: (i) => (
        <span className="flex items-center gap-3">
          <Avatar nome={i.funcionario.nome} />
          <span className="flex flex-col">
            <span className="font-semibold">{i.funcionario.nome}</span>
            <span className="text-pequeno text-texto-suave">{i.funcionario.cargo ?? ''}</span>
          </span>
        </span>
      ),
    },
    {
      chave: 'periodos',
      titulo: ehHoje ? 'Períodos de hoje' : 'Períodos do dia',
      celula: (i) => <ChipsDosPeriodos periodos={i.periodos} ehHoje={ehHoje} />,
    },
    {
      chave: 'horas',
      titulo: 'Horas até agora',
      celula: (i) => <Horas minutos={i.horasAteAgoraMinutos} />,
    },
    {
      chave: 'banco',
      titulo: 'Banco de horas',
      celula: (i) => <Saldo minutos={i.saldoBancoHorasMinutos} />,
    },
    { chave: 'situacao', titulo: 'Situação', celula: (i) => <SituacaoDaPessoa item={i} /> },
    {
      chave: 'acao',
      titulo: 'Ação',
      celula: (i) => (
        <BotaoLink
          variante="secundario"
          tamanho="pequeno"
          to={`espelho/${i.funcionario.id}`}
          aria-label={`Ver espelho de ${i.funcionario.nome}`}
        >
          Ver espelho
        </BotaoLink>
      ),
    },
  ]

  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo="Ponto da equipe"
        subtitulo={
          setor && total !== undefined
            ? `Setor ${setor}, ${total} ${plural(total, 'pessoa', 'pessoas')}. Os pedidos de ajuste do setor chegam primeiro para você analisar.`
            : 'Os pedidos de ajuste do setor chegam primeiro para você analisar.'
        }
        acoes={
          <BotaoLink variante="secundario" to="atestados">
            Atestados da equipe
          </BotaoLink>
        }
      />
      <div className="w-64">
        <Campo rotulo={ehHoje ? `Hoje, ${formatarDiaEMes(hoje)}` : 'Dia'}>
          <Entrada
            type="date"
            lang="pt-BR"
            max={hoje}
            value={data}
            onChange={(e) => e.target.value && e.target.value <= hoje && setData(e.target.value)}
          />
        </Campo>
      </div>
      {dia.error ? (
        <AvisoDeErro erro={dia.error} aoTentarDeNovo={() => void dia.refetch()} />
      ) : (
        <NumerosDoPonto
          rotulo="Números do setor"
          carregando={!dia.data}
          itens={
            dia.data
              ? numeros(dia.data, ehHoje)
              : [
                  { rotulo: 'Trabalhando agora', valor: '—' },
                  { rotulo: 'Sem registro hoje', valor: '—' },
                  { rotulo: 'De férias', valor: '—' },
                  { rotulo: 'Pedidos para analisar', valor: '—' },
                ]
          }
        />
      )}
      <section aria-labelledby="titulo-pedidos-equipe" className="flex flex-col gap-3">
        <h2 id="titulo-pedidos-equipe" className="text-t3 font-bold text-texto">
          Pedidos de ajuste para analisar
        </h2>
        {pedidos.error ? (
          <AvisoDeErro erro={pedidos.error} aoTentarDeNovo={() => void pedidos.refetch()} />
        ) : !pedidos.data ? (
          <Esqueleto className="h-28 w-full" />
        ) : pedidos.data.itens.length === 0 ? (
          <p className="text-corpo text-texto-suave">Nenhum pedido para analisar.</p>
        ) : (
          <ul aria-label="Pedidos para analisar" className="grid grid-cols-2 gap-4">
            {pedidos.data.itens.map((p) => {
              const etiqueta = etiquetaDoPedido(p, true)
              return (
                <li key={p.id}>
                  <Link
                    to={`pedidos/${p.id}`}
                    aria-label={`Pedido de ${p.funcionario.nome}`}
                    className="flex items-center gap-4 rounded-cartao border border-borda bg-superficie p-4 hover:bg-superficie-destaque"
                  >
                    <div className="flex min-w-0 flex-1 flex-col gap-2">
                      <span className="flex items-center gap-3">
                        <Avatar nome={p.funcionario.nome} />
                        <span className="font-semibold text-texto">{p.funcionario.nome}</span>
                      </span>
                      <span className="text-pequeno text-texto-suave">
                        Ajuste do dia {formatarDiaMesDaData(p.data)}
                      </span>
                      <span className="rounded-campo bg-sucesso-fundo px-3 py-2 text-pequeno text-sucesso-texto">
                        {p.resumoDaCorrecao}
                      </span>
                      <span className="flex items-center justify-between gap-2">
                        <span className="text-pequeno text-texto-suave">
                          Pedido em {formatarDiaMesHora(p.criadoEmUtc)}
                        </span>
                        <Selo tom={etiqueta.tom}>{etiqueta.texto}</Selo>
                      </span>
                    </div>
                    <ChevronRight size={18} aria-hidden="true" className="text-texto-suave" />
                  </Link>
                </li>
              )
            })}
          </ul>
        )}
      </section>
      <section aria-labelledby="titulo-hoje-setor" className="flex flex-col gap-3">
        <h2 id="titulo-hoje-setor" className="text-t3 font-bold text-texto">
          {ehHoje ? 'Hoje no setor' : `No setor em ${formatarDiaMesDaData(data)}`}
        </h2>
        <Tabela
          rotulo="Hoje no setor"
          colunas={colunas}
          linhas={dia.data?.itens}
          chaveDaLinha={(i) => i.funcionario.id}
          carregando={dia.isLoading}
          vazio={<EstadoVazio titulo="Você ainda não é gestor de um setor." />}
        />
      </section>
      <Outlet />
    </div>
  )
}
