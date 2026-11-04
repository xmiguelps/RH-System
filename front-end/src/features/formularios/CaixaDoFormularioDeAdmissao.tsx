import { $api } from '../../shared/api/api'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'
import { Selecao } from '../../shared/ui/Selecao'

/**
 * A caixa "Enviar o formulário de admissão" do cadastro (02), da importação e do "Readmitir" (03).
 * As opções são os formulários de admissão publicados; sem nenhum, a caixa fica desligada.
 * Marcada, o cadastro manda `formularioAdmissaoId` e a API inclui a pessoa por dentro.
 */
export function CaixaDoFormularioDeAdmissao({
  valor,
  aoMudar,
  desabilitada = false,
}: {
  valor: number | null
  aoMudar: (formularioId: number | null) => void
  desabilitada?: boolean
}) {
  const consulta = $api.useQuery('get', '/api/rh/formularios', {
    params: { query: { tipo: 'ADMISSAO', situacao: 'PUBLICADO', tamanhoPagina: 50 } },
  })
  // A consulta já filtra; o filtro aqui só protege a lista de qualquer outro item.
  const opcoes = (consulta.data?.itens ?? []).filter(
    (f) => f.tipo === 'ADMISSAO' && f.situacao === 'PUBLICADO',
  )
  const nenhum = consulta.data !== undefined && opcoes.length === 0
  return (
    <div className="flex flex-col gap-2">
      <CaixaDeSelecao
        rotulo="Enviar o formulário de admissão"
        descricao={
          nenhum
            ? 'Crie e publique um formulário de admissão em Formulários'
            : 'A pessoa vê o formulário ao entrar no portal. Nenhum e-mail sai.'
        }
        marcada={valor !== null}
        desabilitada={desabilitada || nenhum || consulta.data === undefined}
        aoMudar={(marcada) => aoMudar(marcada ? (opcoes[0]?.id ?? null) : null)}
      />
      {valor !== null && opcoes.length > 1 && (
        <div className="ml-8 w-80">
          <Selecao
            rotulo="Formulário de admissão"
            valor={String(valor)}
            opcoes={opcoes.map((f) => ({ valor: String(f.id), rotulo: f.titulo }))}
            aoMudar={(v) => v && aoMudar(Number(v))}
          />
        </div>
      )}
      {valor !== null && opcoes.length === 1 && (
        <p className="ml-8 text-pequeno text-texto-suave">{opcoes[0]?.titulo}</p>
      )}
    </div>
  )
}
