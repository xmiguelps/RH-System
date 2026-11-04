import { useEmpresa } from '../../app/useEmpresa'
import type { Esquema } from '../../shared/api/tipos'
import { Botao } from '../../shared/ui/Botao'
import { Markdown } from '../../shared/ui/Markdown'
import { Progresso } from '../../shared/ui/Progresso'
import { Selo } from '../../shared/ui/Selo'
import { CampoDaPergunta } from './CampoDaPergunta'
import { rotuloDoTipo } from './rotulosDosFormularios'

type Pergunta = Esquema<'PerguntaFormulario'>

/** A prévia do assistente (montada no front: não há rota para ela). Mostra a primeira pergunta. */
export function PreviaDoFormulario({
  titulo,
  descricao,
  tipo,
  sentido,
  perguntas,
  legenda,
}: {
  titulo: string
  descricao: string
  tipo: Esquema<'FormularioTipo'>
  sentido: Esquema<'FormularioSentidoAvaliacao'> | null
  perguntas: readonly Pergunta[]
  legenda: string
}) {
  const empresa = useEmpresa() ?? '[Nome da empresa]'
  const primeira = perguntas[0]
  const total = Math.max(perguntas.length, 1)
  return (
    <section aria-label="Prévia" className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-3">
        <span className="text-tabela font-semibold text-texto">
          {tipo === 'ENTREVISTA' ? 'Como o candidato vai ver' : 'Como quem responde vai ver'}
        </span>
        <Selo tom="info">Prévia</Selo>
      </div>
      <div className="flex flex-col gap-4 rounded-cartao border border-borda bg-superficie p-5">
        <div className="flex items-center gap-3">
          <span
            aria-hidden="true"
            className="flex h-9 w-9 items-center justify-center rounded-campo bg-primaria font-bold text-pagina"
          >
            {empresa.replace(/^\[/, '').charAt(0)}
          </span>
          <span className="text-tabela font-semibold text-texto">{empresa}</span>
        </div>
        <div>
          <Selo tom="contorno">{rotuloDoTipo(tipo, sentido)}</Selo>
        </div>
        <p className="text-t3 font-bold text-texto">{titulo || 'Título do formulário'}</p>
        {descricao && <Markdown texto={descricao} className="text-tabela text-texto-suave" />}
        {primeira ? (
          <>
            <div className="flex items-center justify-between text-pequeno text-texto-suave">
              <span>Pergunta 1 de {perguntas.length}</span>
              <span>{Math.round(100 / total)}%</span>
            </div>
            <Progresso valor={1} maximo={total} rotulo="Progresso da prévia" />
            <CampoDaPergunta pergunta={primeira} valor={{}} aoMudar={() => {}} desabilitado />
            <div className="flex justify-end">
              <Botao disabled tamanho="pequeno">
                Próxima
              </Botao>
            </div>
          </>
        ) : (
          <p className="text-tabela text-texto-suave">As perguntas aparecem aqui.</p>
        )}
      </div>
      <p className="text-pequeno text-texto-suave">{legenda}</p>
    </section>
  )
}
