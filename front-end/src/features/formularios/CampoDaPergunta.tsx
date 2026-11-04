import { Radio } from '@base-ui/react/radio'
import { RadioGroup } from '@base-ui/react/radio-group'
import { useId } from 'react'
import type { Esquema } from '../../shared/api/tipos'
import { AreaDeTexto } from '../../shared/ui/AreaDeTexto'
import { Entrada } from '../../shared/ui/Entrada'
import { cn } from '../../shared/ui/estilo'
import { Markdown } from '../../shared/ui/Markdown'
import { SeletorDeArquivo } from '../../shared/ui/SeletorDeArquivo'
import { ARQUIVOS_ACEITOS, LIMITE_DA_RESPOSTA } from './rotulosDosFormularios'
import type { RespostaLocal } from './respostasLocais'

type Pergunta = Esquema<'PerguntaFormulario'>

/**
 * Uma pergunta com o campo do tipo dela (responder, página pública e prévia do assistente).
 * O texto da pergunta é texto ou markdown, nunca HTML.
 */
export function CampoDaPergunta({
  pergunta,
  valor,
  aoMudar,
  desabilitado = false,
  erro,
}: {
  pergunta: Pergunta
  valor: RespostaLocal
  aoMudar: (valor: RespostaLocal) => void
  desabilitado?: boolean
  erro?: string | null
}) {
  const id = useId()
  const idDoTitulo = `${id}-titulo`
  return (
    <fieldset className="flex flex-col gap-4" aria-labelledby={idDoTitulo} disabled={desabilitado}>
      <legend id={idDoTitulo} className="text-t3 font-bold text-texto">
        <Markdown texto={pergunta.texto} />
        {!pergunta.obrigatoria && (
          <span className="text-pequeno font-normal text-texto-suave"> (opcional)</span>
        )}
      </legend>
      {(pergunta.tipo === 'RESPOSTA_CURTA' || pergunta.tipo === 'RESPOSTA_LONGA') &&
        (pergunta.tipo === 'RESPOSTA_CURTA' ? (
          <Entrada
            aria-labelledby={idDoTitulo}
            maxLength={LIMITE_DA_RESPOSTA.RESPOSTA_CURTA}
            value={valor.texto ?? ''}
            onChange={(e) => aoMudar({ ...valor, texto: e.target.value })}
            aria-invalid={erro ? true : undefined}
          />
        ) : (
          <AreaDeTexto
            aria-labelledby={idDoTitulo}
            rows={5}
            limite={LIMITE_DA_RESPOSTA.RESPOSTA_LONGA}
            value={valor.texto ?? ''}
            onChange={(e) => aoMudar({ ...valor, texto: e.target.value })}
            aria-invalid={erro ? true : undefined}
          />
        ))}
      {pergunta.tipo === 'MULTIPLA_ESCOLHA' && (
        <RadioGroup
          aria-labelledby={idDoTitulo}
          value={valor.alternativaId !== undefined ? String(valor.alternativaId) : null}
          onValueChange={(v) => aoMudar({ ...valor, alternativaId: Number(v) })}
          disabled={desabilitado}
          className="flex flex-col gap-2"
        >
          {pergunta.alternativas.map((alt) => (
            <label
              key={alt.id}
              className="flex cursor-pointer items-center gap-3 rounded-cartao border border-borda bg-superficie p-3 has-[[data-checked]]:border-primaria"
            >
              <Radio.Root
                value={String(alt.id)}
                className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full border border-[#8fa9a6] bg-superficie data-checked:border-primaria data-checked:bg-primaria"
              >
                <Radio.Indicator className="block h-2 w-2 rounded-full bg-superficie" />
              </Radio.Root>
              <span className="text-corpo text-texto">{alt.texto}</span>
            </label>
          ))}
        </RadioGroup>
      )}
      {pergunta.tipo === 'ESCALA' && (
        <div role="radiogroup" aria-labelledby={idDoTitulo} className="flex gap-2">
          {[1, 2, 3, 4, 5].map((n) => (
            <button
              key={n}
              type="button"
              role="radio"
              aria-checked={valor.valorEscala === n}
              disabled={desabilitado}
              onClick={() => aoMudar({ ...valor, valorEscala: n })}
              className={cn(
                'h-12 w-12 rounded-campo border text-corpo font-semibold focus-visible:outline-2 focus-visible:outline-primaria-media',
                valor.valorEscala === n
                  ? 'border-primaria bg-primaria text-pagina'
                  : 'border-borda-campo bg-superficie text-texto',
              )}
            >
              {n}
            </button>
          ))}
        </div>
      )}
      {pergunta.tipo === 'ARQUIVO' && (
        <SeletorDeArquivo
          arquivo={valor.arquivo ?? null}
          aoMudar={(arquivo) => aoMudar({ ...valor, arquivo })}
          tiposAceitos={ARQUIVOS_ACEITOS}
          tamanhoMaximoMb={5}
          textoDeAjuda="PDF ou Word, até 5 MB"
        />
      )}
      {erro && (
        <p role="alert" className="text-pequeno text-erro-texto">
          {erro}
        </p>
      )}
    </fieldset>
  )
}
