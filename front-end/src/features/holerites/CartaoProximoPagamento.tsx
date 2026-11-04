import { formatarDiaEMes, nomeDoMes } from '../../shared/formato/datas'
import { Cartao } from '../../shared/ui/Cartao'

/**
 * O cartão "Próximo pagamento" (componente único: telas 36 e 49 aqui; 32 e 46 na fatia 7).
 * `data` é `pagamento.data` e `mesReferencia` é `competencia.mes`; sem `data`, não desenha nada.
 */
export function CartaoProximoPagamento({
  data,
  mesReferencia,
}: {
  data: string | null | undefined
  mesReferencia: number | null | undefined
}) {
  if (!data) return null
  return (
    <Cartao tom="escuro" aria-label="Próximo pagamento" className="flex flex-col gap-2 p-6">
      <span className="text-tabela text-menu-rotulo">Próximo pagamento</span>
      <span className="text-t2 font-bold">Até {formatarDiaEMes(data)}</span>
      {mesReferencia ? (
        <span className="text-corpo text-menu-apoio">
          O holerite de {nomeDoMes(mesReferencia)} aparece aqui quando o RH publicar. Você também
          recebe um e-mail.
        </span>
      ) : null}
      <span className="text-pequeno text-menu-apoio">Dúvida sobre um valor? Fale com o RH</span>
    </Cartao>
  )
}
