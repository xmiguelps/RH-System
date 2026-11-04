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
  const de = (pagina - 1) * tamanhoPagina + 1
  const ate = Math.min(pagina * tamanhoPagina, total)
  const temVarias = totalPaginas > 1
  return (
    <div className="flex items-center justify-between gap-4">
      <p className="text-tabela text-texto-suave">
        {total === 0 ? 'Nenhum registro' : resumo(de, ate, total)}
      </p>
      <div className="flex gap-2">
        <Botao
          variante="secundario"
          tamanho="pequeno"
          disabled={!temVarias || pagina <= 1}
          onClick={() => aoMudar(pagina - 1)}
        >
          Anterior
        </Botao>
        <Botao
          variante="secundario"
          tamanho="pequeno"
          disabled={!temVarias || pagina >= totalPaginas}
          onClick={() => aoMudar(pagina + 1)}
        >
          Próxima
        </Botao>
      </div>
    </div>
  )
}
