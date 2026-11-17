import type { Esquema } from '../api/tipos'
import { formatarNumero } from '../formato/numeros'
import { Botao } from './Botao'

type DadosDaPaginacao = Pick<
  Esquema<'Paginacao'>,
  'pagina' | 'tamanhoPagina' | 'total' | 'totalPaginas'
>

function resumoPadrao(de: number, ate: number, total: number): string {
  return `Mostrando ${formatarNumero(de)} a ${formatarNumero(ate)} de ${formatarNumero(total)}`
}

export function Paginacao({
  dados,
  aoMudar,
  resumo = resumoPadrao,
}: {
  dados: DadosDaPaginacao
  aoMudar: (pagina: number) => void
  resumo?: (de: number, ate: number, total: number) => string
}) {
  const { pagina, tamanhoPagina, total, totalPaginas } = dados
  // página além da última (os itens dela foram apagados): mostra a última que existe, em vez de "21 a 20"
  const paginaMostrada = totalPaginas > 0 ? Math.min(pagina, totalPaginas) : pagina
  const de = (paginaMostrada - 1) * tamanhoPagina + 1
  const ate = Math.min(paginaMostrada * tamanhoPagina, total)
  return (
    <div className="flex items-center justify-between gap-4">
      <p className="text-tabela text-texto-suave">
        {total === 0 ? 'Nenhum registro' : resumo(de, ate, total)}
      </p>
      <div className="flex gap-2">
        <Botao
          variante="secundario"
          tamanho="pequeno"
          disabled={pagina <= 1}
          onClick={() => aoMudar(Math.min(pagina, totalPaginas + 1) - 1)}
        >
          Anterior
        </Botao>
        <Botao
          variante="secundario"
          tamanho="pequeno"
          disabled={pagina >= totalPaginas}
          onClick={() => aoMudar(pagina + 1)}
        >
          Próxima
        </Botao>
      </div>
    </div>
  )
}
