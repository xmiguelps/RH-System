import type { Esquema } from '../../shared/api/tipos'
import { formatarMinutosComoHoras } from '../../shared/formato/numeros'
import { Botao } from '../../shared/ui/Botao'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { CelulaDoDia, ChipsDosPeriodos, EtiquetaDoDia, Horas, Saldo } from './PecasDoPonto'

type Dia = Esquema<'PontoEspelhoDia'>
type DiaProprio = Esquema<'PontoEspelhoDiaProprio'>

const periodos: ColunaDaTabela<Dia> = {
  chave: 'periodos',
  titulo: 'Períodos registrados',
  celula: (d) => (
    <ChipsDosPeriodos
      periodos={d.periodos}
      ehHoje={d.ehHoje}
      semNada={d.situacao === 'FOLGA' ? 'Sem expediente' : 'Nenhum registro'}
    />
  ),
}

const COLUNAS_RESUMIDAS: readonly ColunaDaTabela<Dia>[] = [
  { chave: 'dia', titulo: 'Dia', celula: (d) => <CelulaDoDia dia={d} /> },
  periodos,
  { chave: 'horas', titulo: 'Horas', celula: (d) => <Horas minutos={d.horasMinutos} /> },
  { chave: 'saldo', titulo: 'Saldo', celula: (d) => <Saldo minutos={d.saldoMinutos} /> },
  { chave: 'situacao', titulo: 'Situação', celula: (d) => <EtiquetaDoDia dia={d} /> },
]

/**
 * A tabela do mês da ficha (04 e aba Ponto da equipe: sem as folgas) e do espelho da pessoa da equipe
 * (56: com as folgas). Os números vêm prontos da API.
 */
export function TabelaDoEspelho({
  rotulo,
  dias,
  semFolgas = false,
  carregando,
  erro,
  aoTentarDeNovo,
}: {
  rotulo: string
  dias: readonly Dia[] | undefined
  semFolgas?: boolean
  carregando?: boolean
  erro?: unknown
  aoTentarDeNovo?: () => void
}) {
  const linhas = semFolgas ? dias?.filter((d) => d.situacao !== 'FOLGA') : dias
  return (
    <Tabela
      rotulo={rotulo}
      colunas={COLUNAS_RESUMIDAS}
      linhas={linhas}
      chaveDaLinha={(d) => d.data}
      carregando={carregando}
      erro={erro}
      aoTentarDeNovo={aoTentarDeNovo}
      vazio={<EstadoVazio titulo="Nada registrado neste mês." />}
    />
  )
}

/** A tabela "Registros de {mês}" de Meu ponto (33 e 47): a última coluna é "Pedir ajuste" ou o chip. */
export function TabelaDoMeuEspelho({
  rotulo,
  dias,
  carregando,
  erro,
  aoTentarDeNovo,
  aoPedirAjuste,
}: {
  rotulo: string
  dias: readonly DiaProprio[] | undefined
  carregando?: boolean
  erro?: unknown
  aoTentarDeNovo?: () => void
  aoPedirAjuste: (dia: DiaProprio) => void
}) {
  const colunas: readonly ColunaDaTabela<DiaProprio>[] = [
    { chave: 'dia', titulo: 'Dia', celula: (d) => <CelulaDoDia dia={d} /> },
    periodos,
    {
      chave: 'horas',
      titulo: 'Horas trabalhadas',
      celula: (d) => <Horas minutos={d.horasMinutos} />,
    },
    {
      chave: 'carga',
      titulo: 'Carga horária',
      celula: (d) =>
        d.cargaMinutos === 0 ? (
          <span className="text-texto-suave">—</span>
        ) : (
          formatarMinutosComoHoras(d.cargaMinutos)
        ),
    },
    {
      chave: 'saldo',
      titulo: 'Saldo do dia',
      celula: (d) => <Saldo minutos={d.saldoMinutos} />,
    },
    {
      chave: 'acao',
      titulo: 'Ação',
      celula: (d) =>
        d.podePedirAjuste ? (
          <Botao
            variante="secundario"
            tamanho="pequeno"
            onClick={() => aoPedirAjuste(d)}
            aria-label={`Pedir ajuste do dia ${d.data.slice(8, 10)}/${d.data.slice(5, 7)}`}
          >
            Pedir ajuste
          </Botao>
        ) : (
          <EtiquetaDoDia dia={d} />
        ),
    },
  ]
  return (
    <Tabela
      rotulo={rotulo}
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
