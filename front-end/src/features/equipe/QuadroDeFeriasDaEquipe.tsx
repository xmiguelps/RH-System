import type { Esquema } from '../../shared/api/tipos'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { cn } from '../../shared/ui/estilo'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { barraDoPeriodo, mesesDasSemanas } from './calendarioDeFerias'

type Calendario = Esquema<'FeriasCalendario'>
type Item = Esquema<'FeriasCalendarioItem'>
type Pessoa = { id: number; nome: string }

// Todas as classes por extenso: o Tailwind só gera o que acha escrito inteiro no código.
const COLUNA_INICIAL = [
  'col-start-1',
  'col-start-2',
  'col-start-3',
  'col-start-4',
  'col-start-5',
  'col-start-6',
  'col-start-7',
  'col-start-8',
  'col-start-9',
  'col-start-10',
  'col-start-11',
  'col-start-12',
  'col-start-13',
] as const
const LARGURA = [
  'col-span-1',
  'col-span-2',
  'col-span-3',
  'col-span-4',
  'col-span-5',
  'col-span-6',
  'col-span-7',
  'col-span-8',
  'col-span-9',
  'col-span-10',
  'col-span-11',
  'col-span-12',
  'col-span-13',
] as const

const COR_DA_BARRA: Record<Item['situacao'], string> = {
  APROVADA: 'bg-primaria text-pagina',
  AGUARDANDO_VOCE: 'border border-dashed border-alerta-ponto bg-alerta-fundo text-alerta-forte',
  COM_O_RH: 'border border-dashed border-pagina bg-primaria text-pagina',
}
const ROTULO_DA_SITUACAO: Record<Item['situacao'], string> = {
  APROVADA: 'Aprovada',
  AGUARDANDO_VOCE: 'Aguardando você',
  COM_O_RH: 'Aguardando o RH',
}

/** "Outubro a dezembro, por semana" (do primeiro ao último mês da janela). */
function resumoDaJanela(semanas: readonly string[]): string {
  const meses = mesesDasSemanas(semanas)
  const primeiro = meses[0]?.mes
  const ultimo = meses.at(-1)?.mes
  if (!primeiro || !ultimo) return ''
  return primeiro === ultimo
    ? `${primeiro}, por semana`
    : `${primeiro} a ${ultimo.toLowerCase()}, por semana`
}

/**
 * "Férias da equipe": uma linha por pessoa (na ordem de `pessoas`) e uma coluna por semana. Quem não
 * tem férias na janela não vem em `itens`: a linha aparece sem barra. Quem tem mais de um período
 * (ou uma sugestão de data ao lado) ganha uma barra por período.
 */
export function QuadroDeFeriasDaEquipe({
  calendario,
  pessoas,
  carregando,
  erro,
  aoTentarDeNovo,
}: {
  calendario: Calendario | undefined
  pessoas: readonly Pessoa[]
  carregando: boolean
  erro: unknown
  aoTentarDeNovo: () => void
}) {
  return (
    <section aria-labelledby="titulo-ferias-da-equipe" className="flex flex-col gap-3">
      <div className="flex items-baseline justify-between gap-3">
        <h2 id="titulo-ferias-da-equipe" className="text-t3 font-bold text-texto">
          Férias da equipe
        </h2>
        {calendario && (
          <span className="text-tabela text-texto-suave">{resumoDaJanela(calendario.semanas)}</span>
        )}
      </div>
      {erro ? (
        <AvisoDeErro erro={erro} aoTentarDeNovo={aoTentarDeNovo} />
      ) : carregando || !calendario ? (
        <Esqueleto className="h-40 w-full" />
      ) : (
        <div className="overflow-x-auto rounded-cartao border border-borda bg-superficie p-4">
          <div role="table" aria-label="Férias da equipe por semana" className="min-w-[760px]">
            <div role="row" className="grid grid-cols-[160px_1fr] items-end gap-2 pb-1">
              <span role="columnheader" className="text-tabela text-texto-suave">
                Pessoa
              </span>
              <div className="flex flex-col gap-1">
                <div className="grid grid-cols-13 text-tabela font-semibold text-texto">
                  {mesesDasSemanas(calendario.semanas).map((m, i) => (
                    <span key={`${m.mes}-${i}`} className={cn(LARGURA[m.colunas - 1])}>
                      {m.mes}
                    </span>
                  ))}
                </div>
                <div className="grid grid-cols-13 text-pequeno text-texto-suave">
                  {calendario.semanas.map((segunda) => (
                    <span key={segunda} role="columnheader">
                      {segunda.slice(8, 10)}
                    </span>
                  ))}
                </div>
              </div>
            </div>
            {pessoas.map((pessoa) => {
              const barras = calendario.itens.filter((i) => i.funcionario.id === pessoa.id)
              return (
                <div
                  key={pessoa.id}
                  role="row"
                  className="grid grid-cols-[160px_1fr] items-center gap-2 border-t border-divisor py-2"
                >
                  <span role="rowheader" className="truncate text-tabela text-texto">
                    {pessoa.nome}
                  </span>
                  <div className="grid min-h-7 grid-cols-13 gap-y-1">
                    {barras.map((item) => {
                      const barra = barraDoPeriodo(
                        calendario.semanas,
                        calendario.fim,
                        item.dataInicio,
                        item.dataFim,
                      )
                      if (!barra) return null
                      return (
                        <span
                          key={item.feriasId}
                          role="cell"
                          data-situacao={item.situacao}
                          aria-label={`${pessoa.nome}: ${barra.texto} (${ROTULO_DA_SITUACAO[item.situacao]})`}
                          className={cn(
                            'truncate rounded-botao px-2 py-1 text-pequeno',
                            COLUNA_INICIAL[barra.coluna - 1],
                            LARGURA[barra.largura - 1],
                            COR_DA_BARRA[item.situacao],
                          )}
                        >
                          {barra.texto}
                        </span>
                      )
                    })}
                  </div>
                </div>
              )
            })}
          </div>
          <ul
            aria-label="Legenda"
            className="mt-3 flex flex-wrap gap-4 text-pequeno text-texto-suave"
          >
            {(Object.keys(ROTULO_DA_SITUACAO) as Item['situacao'][]).map((s) => (
              <li key={s} className="flex items-center gap-2">
                <span
                  aria-hidden="true"
                  className={cn('inline-block h-3 w-6 rounded-botao', COR_DA_BARRA[s])}
                />
                {ROTULO_DA_SITUACAO[s]}
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  )
}
