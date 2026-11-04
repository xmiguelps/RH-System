import type { Esquema } from '../../shared/api/tipos'
import { formatarDiaMes, formatarHora } from '../../shared/formato/datas'
import { Botao } from '../../shared/ui/Botao'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { CelulaDoDia, EtiquetaDoDia, Horas } from './PecasDoPonto'

type Dia = Esquema<'PontoEspelhoDia'>
type Marcacao = Esquema<'PontoMarcacao'>

function marcacoesDoDia(dia: Dia): Marcacao[] {
  return dia.periodos
    .flatMap((p) => [p.entrada, p.saida])
    .filter((m): m is Marcacao => m !== null)
    .sort((a, b) => a.ordem - b.ordem)
}

function CelulaDaBatida({ dia, ordem }: { dia: Dia; ordem: 1 | 2 | 3 | 4 }) {
  const marcacoes = marcacoesDoDia(dia)
  const marcacao = marcacoes.find((m) => m.ordem === ordem)
  const extras = ordem === 4 ? marcacoes.length - 4 : 0
  if (marcacao) {
    return (
      <span className="flex flex-col">
        <span className="tabular-nums">{formatarHora(marcacao.instanteUtc)}</span>
        {extras > 0 && <span className="text-pequeno text-texto-suave">+{extras} batidas</span>}
      </span>
    )
  }
  const aberto = dia.periodos.some((p) => p.entrada && !p.saida)
  if (aberto && !dia.ehHoje && ordem === marcacoes.length + 1) {
    return <span className="text-alerta-texto">Sem registro</span>
  }
  return <span className="text-texto-suave">—</span>
}

/** As batidas corrigidas pelo RH: "Saída corrigida pelo RH em 07/10". */
function NotasDeCorrecao({ dia }: { dia: Dia }) {
  const corrigidas = marcacoesDoDia(dia).filter((m) => m.origem === 'CORRIGIDA_RH')
  if (corrigidas.length === 0) return null
  return (
    <>
      {corrigidas.map((m) => (
        <span key={m.id} className="text-pequeno text-texto-suave">
          {m.rotulo} corrigida pelo RH em {formatarDiaMes(m.corrigidaEmUtc)}
        </span>
      ))}
    </>
  )
}

/** A tabela do espelho da tela 12 (com as folgas, como o PNG 12). */
export function TabelaDoEspelhoRh({
  dias,
  carregando,
  erro,
  aoTentarDeNovo,
  aoEditar,
  aoRevisarPedido,
}: {
  dias: readonly Dia[] | undefined
  carregando?: boolean
  erro?: unknown
  aoTentarDeNovo?: () => void
  aoEditar: (dia: Dia) => void
  aoRevisarPedido: (pedidoId: number) => void
}) {
  const colunas: readonly ColunaDaTabela<Dia>[] = [
    { chave: 'dia', titulo: 'Dia', celula: (d) => <CelulaDoDia dia={d} /> },
    { chave: 'entrada', titulo: 'Entrada', celula: (d) => <CelulaDaBatida dia={d} ordem={1} /> },
    {
      chave: 'saidaAlmoco',
      titulo: 'Saída almoço',
      celula: (d) => <CelulaDaBatida dia={d} ordem={2} />,
    },
    { chave: 'volta', titulo: 'Volta', celula: (d) => <CelulaDaBatida dia={d} ordem={3} /> },
    { chave: 'saida', titulo: 'Saída', celula: (d) => <CelulaDaBatida dia={d} ordem={4} /> },
    { chave: 'total', titulo: 'Total', celula: (d) => <Horas minutos={d.horasMinutos} /> },
    {
      chave: 'situacao',
      titulo: 'Situação',
      celula: (d) => (
        <span className="flex flex-col items-start gap-1">
          <EtiquetaDoDia dia={d} onde="rh" />
          <NotasDeCorrecao dia={d} />
        </span>
      ),
    },
    {
      chave: 'acao',
      titulo: 'Ação',
      celula: (d) => {
        const rotuloDoDia = `${d.data.slice(8, 10)}/${d.data.slice(5, 7)}`
        if (d.pedidoEmAnaliseId !== null) {
          const pedidoId = d.pedidoEmAnaliseId
          return (
            <Botao
              tamanho="pequeno"
              onClick={() => aoRevisarPedido(pedidoId)}
              aria-label={`Revisar pedido do dia ${rotuloDoDia}`}
            >
              Revisar pedido
            </Botao>
          )
        }
        if (d.situacao === 'FOLGA') return null
        return (
          <Botao
            variante="secundario"
            tamanho="pequeno"
            onClick={() => aoEditar(d)}
            aria-label={`Editar o dia ${rotuloDoDia}`}
          >
            Editar
          </Botao>
        )
      },
    },
  ]
  return (
    <Tabela
      rotulo="Espelho de ponto"
      colunas={colunas}
      linhas={dias}
      chaveDaLinha={(d) => d.data}
      carregando={carregando}
      erro={erro}
      aoTentarDeNovo={aoTentarDeNovo}
      vazio={<EstadoVazio titulo="Nada registrado neste mês." />}
    />
  )
}
