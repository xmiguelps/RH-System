import type { Esquema } from '../../shared/api/tipos'
import { Botao } from '../../shared/ui/Botao'

type Dada = Esquema<'RespostaFormularioDada'>

/** As respostas de uma pessoa, na ordem das perguntas (RH e "Minhas respostas"). */
export function RespostasDadas({
  respostas,
  aoBaixarArquivo,
}: {
  respostas: readonly Dada[]
  /** Sem esta função o arquivo aparece só pelo nome ("não há download de volta"). */
  aoBaixarArquivo?: (respostaId: number) => void
}) {
  const ordenadas = [...respostas].sort((a, b) => a.ordem - b.ordem)
  return (
    <ol aria-label="Respostas" className="flex flex-col gap-4">
      {ordenadas.map((r) => (
        <li
          key={r.perguntaId}
          className="flex flex-col gap-1 rounded-cartao bg-superficie-suave p-4"
        >
          <span className="text-tabela font-semibold text-texto">
            {r.ordem}. {r.perguntaTexto}
          </span>
          <Resposta resposta={r} aoBaixarArquivo={aoBaixarArquivo} />
        </li>
      ))}
    </ol>
  )
}

function Resposta({
  resposta: r,
  aoBaixarArquivo,
}: {
  resposta: Dada
  aoBaixarArquivo?: (respostaId: number) => void
}) {
  const vazia = <span className="text-corpo text-texto-suave">Sem resposta</span>
  if (r.tipo === 'MULTIPLA_ESCOLHA')
    return r.alternativas.length ? (
      <span className="text-corpo text-texto">{r.alternativas.map((a) => a.texto).join(', ')}</span>
    ) : (
      vazia
    )
  if (r.tipo === 'ESCALA')
    return r.valorEscala !== null ? (
      <span className="text-corpo text-texto">{r.valorEscala} de 5</span>
    ) : (
      vazia
    )
  if (r.tipo === 'ARQUIVO') {
    const arquivo = r.arquivo
    if (!arquivo) return vazia
    return (
      <span className="flex items-center gap-3 text-corpo text-texto">
        {arquivo.nome}
        {aoBaixarArquivo && (
          <Botao
            variante="secundario"
            tamanho="pequeno"
            aria-label={`Baixar ${arquivo.nome}`}
            onClick={() => aoBaixarArquivo(arquivo.respostaId)}
          >
            Baixar
          </Botao>
        )}
      </span>
    )
  }
  return r.texto ? (
    <span className="whitespace-pre-line text-corpo text-texto">{r.texto}</span>
  ) : (
    vazia
  )
}
