import type { Esquema } from '../../shared/api/tipos'
import { primeiroNome } from '../../shared/formato/texto'
import { AreaDeTexto } from '../../shared/ui/AreaDeTexto'
import { Campo } from '../../shared/ui/Campo'
import { ChipsDosPeriodos } from './PecasDoPonto'

type Pedido = Esquema<'PontoPedidoAjuste'>

/** Registro atual, correção pedida e justificativa: o miolo do pedido nas telas 13 e 55. */
export function MioloDoPedido({ pedido }: { pedido: Pedido }) {
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col gap-2">
        <span className="text-tabela font-semibold text-texto">Registro atual</span>
        <ChipsDosPeriodos periodos={pedido.periodosDoDia} ehHoje={false} />
      </div>
      <div className="flex flex-col gap-2">
        <span className="text-tabela font-semibold text-texto">Correção pedida</span>
        <p className="rounded-campo bg-sucesso-fundo px-4 py-3 text-corpo text-sucesso-texto">
          {pedido.resumoDaCorrecao}
        </p>
      </div>
      <div className="flex flex-col gap-2">
        <span className="text-tabela font-semibold text-texto">Justificativa</span>
        <p className="rounded-campo bg-superficie-suave px-4 py-3 text-corpo text-texto">
          “{pedido.justificativa}”
        </p>
      </div>
    </div>
  )
}

export function CampoDaObservacao({
  pedido,
  valor,
  aoMudar,
  desabilitado,
}: {
  pedido: Pedido
  valor: string
  aoMudar: (texto: string) => void
  desabilitado?: boolean
}) {
  return (
    <Campo rotulo={`Observação para ${primeiroNome(pedido.funcionario.nome)} (opcional)`}>
      <AreaDeTexto
        limite={500}
        rows={3}
        value={valor}
        disabled={desabilitado}
        onChange={(e) => aoMudar(e.target.value.slice(0, 500))}
      />
    </Campo>
  )
}
