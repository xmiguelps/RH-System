import { CircleAlert, CircleCheck, TriangleAlert } from 'lucide-react'
import type { Esquema } from '../../shared/api/tipos'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { cn } from '../../shared/ui/estilo'
import { useVerificacao } from './regrasDoAssistente'

type Item = Esquema<'FormularioVerificacaoItem'>

const ICONE = { OK: CircleCheck, AVISO: TriangleAlert, BLOQUEIO: CircleAlert } as const
const COR = {
  OK: 'text-primaria-media',
  AVISO: 'text-alerta-ponto',
  BLOQUEIO: 'text-erro-texto',
} as const
const ROTULO = { OK: 'Certo', AVISO: 'Aviso', BLOQUEIO: 'Impede a publicação' } as const

/** Tela 19: a conferência vem da API, na ordem recebida, com três situações (OK, AVISO, BLOQUEIO). */
export function EtapaVerificacao({ formularioId }: { formularioId: number }) {
  const consulta = useVerificacao(formularioId)
  const dados = consulta.data
  return (
    <div className="flex flex-col gap-4">
      <h2 className="text-t3 font-bold text-texto">Verificação</h2>
      <p className="text-corpo text-texto-suave">
        O sistema confere o formulário antes de publicar. Os itens em laranja não impedem a
        publicação.
      </p>
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      ) : !dados ? (
        <div role="status" aria-label="Carregando a verificação" className="flex flex-col gap-3">
          {[1, 2, 3, 4].map((n) => (
            <Esqueleto key={n} className="h-10 w-full" />
          ))}
        </div>
      ) : (
        <>
          <ul aria-label="Conferência" className="flex flex-col gap-2">
            {dados.itens.map((item: Item, i) => {
              const Icone = ICONE[item.situacao]
              return (
                <li
                  key={`${item.codigo}-${i}`}
                  data-situacao={item.situacao}
                  className={cn(
                    'flex items-start gap-3 rounded-cartao border p-3',
                    item.situacao === 'BLOQUEIO'
                      ? 'border-erro-texto bg-erro-fundo'
                      : item.situacao === 'AVISO'
                        ? 'border-alerta-ponto bg-alerta-fundo'
                        : 'border-borda bg-superficie',
                  )}
                >
                  <Icone
                    aria-hidden="true"
                    className={cn('mt-0.5 h-5 w-5 shrink-0', COR[item.situacao])}
                  />
                  <span className="text-corpo text-texto">
                    <span className="sr-only">{ROTULO[item.situacao]}: </span>
                    {item.mensagem}
                  </span>
                </li>
              )
            })}
          </ul>
          {!dados.podePublicar && (
            <p role="alert" className="text-tabela font-semibold text-erro-texto">
              Corrija os itens em vermelho para publicar.
            </p>
          )}
        </>
      )}
    </div>
  )
}
