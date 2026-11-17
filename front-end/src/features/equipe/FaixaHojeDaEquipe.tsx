import type { Esquema } from '../../shared/api/tipos'
import { formatarNumero } from '../../shared/formato/numeros'
import { Esqueleto } from '../../shared/ui/Esqueleto'

type Equipe = Esquema<'FuncionarioEquipeResposta'>

/**
 * A faixa "Hoje" do Painel da equipe: as contagens vêm de `listarEquipeDoGestor` (não do painel) e
 * os nomes de quem está sem registro são os itens com `situacaoHoje` `SEM_REGISTRO`. Só mostra o que
 * é maior que zero (menos "trabalhando", que é o número que sempre aparece).
 */
export function FaixaHojeDaEquipe({
  equipe,
  carregando,
}: {
  equipe: Equipe | undefined
  carregando: boolean
}) {
  if (carregando || !equipe) {
    return <Esqueleto className="h-12 w-full" />
  }
  if (!equipe.diaUtil) {
    return (
      <section
        aria-label="Hoje"
        className="rounded-cartao border border-borda bg-superficie px-5 py-4 text-corpo text-texto"
      >
        Hoje não há expediente.
      </section>
    )
  }
  const c = equipe.contagensHoje
  const semRegistro = equipe.itens
    .filter((p) => p.situacaoHoje === 'SEM_REGISTRO')
    .map((p) => p.nome)
  const partes = [
    `${formatarNumero(c.trabalhando)} trabalhando`,
    c.ferias > 0 && `${formatarNumero(c.ferias)} de férias`,
    c.atestado > 0 && `${formatarNumero(c.atestado)} com atestado`,
    c.aindaNaoComecou > 0 && `${formatarNumero(c.aindaNaoComecou)} ainda não começaram`,
    c.semRegistro > 0 &&
      `${formatarNumero(c.semRegistro)} sem registro${semRegistro.length > 0 ? `: ${semRegistro.join(', ')}` : ''}`,
  ].filter((p): p is string => typeof p === 'string')
  return (
    <section
      aria-label="Hoje"
      className="flex flex-wrap items-center gap-x-6 gap-y-1 rounded-cartao border border-borda bg-superficie px-5 py-4"
    >
      <h2 className="text-corpo font-bold text-texto">Hoje</h2>
      {partes.map((texto) => (
        <span key={texto} className="text-corpo text-texto">
          {texto}
        </span>
      ))}
    </section>
  )
}
