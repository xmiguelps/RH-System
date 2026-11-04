import { $api } from '../../shared/api/api'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'

/**
 * A caixa "Incluir no treinamento de onboarding" do cadastro (02), da importação e do "Readmitir".
 * As opções são os treinamentos de onboarding em vigor; sem nenhum, a caixa fica desligada.
 * Marcada, o cadastro manda `incluirNoOnboarding: true` e a API inscreve a pessoa por dentro.
 */
export function CaixaDoOnboarding({
  marcada,
  aoMudar,
  desabilitada = false,
}: {
  marcada: boolean
  aoMudar: (marcada: boolean) => void
  desabilitada?: boolean
}) {
  const consulta = $api.useQuery('get', '/api/rh/treinamentos/onboarding')
  const itens = consulta.data?.itens ?? []
  const nenhum = consulta.data !== undefined && itens.length === 0
  const rotulo =
    itens.length === 1
      ? `Incluir no treinamento ${itens[0]!.titulo}`
      : itens.length > 1
        ? 'Incluir nos treinamentos de onboarding'
        : 'Incluir no treinamento de onboarding'
  return (
    <CaixaDeSelecao
      rotulo={rotulo}
      descricao={nenhum ? 'Nenhum treinamento de onboarding publicado' : undefined}
      marcada={marcada && !nenhum}
      desabilitada={desabilitada || nenhum || consulta.data === undefined}
      aoMudar={aoMudar}
    />
  )
}
