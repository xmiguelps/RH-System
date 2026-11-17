import { formatarDiaMesDaData, nomeDoMes } from '../../shared/formato/datas'

export type BarraDoCalendario = {
  /** Coluna da primeira semana (1 a N). */
  coluna: number
  /** Quantas colunas a barra ocupa. */
  largura: number
  texto: string
}

/** A posição da última semana que começa em `data` ou antes (as datas ISO comparam como texto). */
function colunaDaData(semanas: readonly string[], data: string): number {
  let achada = 0
  semanas.forEach((segunda, i) => {
    if (segunda <= data) achada = i
  })
  return achada + 1
}

/**
 * A barra de um período de férias no quadro de semanas (uma coluna por segunda-feira; a janela vai
 * até `fim`, o último domingo). `null` quando o período não toca a janela. Só compara datas como
 * texto: nunca `new Date('aaaa-mm-dd')`, que num fuso a oeste de Greenwich mostra o dia anterior.
 */
export function barraDoPeriodo(
  semanas: readonly string[],
  fim: string,
  dataInicio: string,
  dataFim: string,
): BarraDoCalendario | null {
  const primeira = semanas[0]
  if (!primeira || dataFim < primeira || dataInicio > fim) return null
  const comecaAntes = dataInicio < primeira
  const passaDoFim = dataFim > fim
  const coluna = comecaAntes ? 1 : colunaDaData(semanas, dataInicio)
  const ultima = passaDoFim ? semanas.length : colunaDaData(semanas, dataFim)
  const texto =
    comecaAntes && passaDoFim
      ? 'Durante todo o período'
      : comecaAntes
        ? `Até ${formatarDiaMesDaData(dataFim)}`
        : passaDoFim
          ? `A partir de ${formatarDiaMesDaData(dataInicio)}`
          : `${formatarDiaMesDaData(dataInicio)} a ${formatarDiaMesDaData(dataFim)}`
  return { coluna, largura: ultima - coluna + 1, texto }
}

/** Os meses do cabeçalho do quadro: cada semana vale pelo mês da sua segunda-feira. */
export function mesesDasSemanas(
  semanas: readonly string[],
): readonly { mes: string; colunas: number }[] {
  const meses: { mes: string; colunas: number }[] = []
  for (const segunda of semanas) {
    const nome = nomeDoMes(Number(segunda.slice(5, 7)))
    const mes = `${nome.charAt(0).toUpperCase()}${nome.slice(1)}`
    const ultimo = meses.at(-1)
    if (ultimo?.mes === mes) ultimo.colunas += 1
    else meses.push({ mes, colunas: 1 })
  }
  return meses
}
