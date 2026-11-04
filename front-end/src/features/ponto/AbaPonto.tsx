import type { PropsDaAba } from '../colaboradores/abasDaFicha'
import { $api } from '../../shared/api/api'
import { formatarMinutosComoHoras, formatarNumero } from '../../shared/formato/numeros'
import { nomeDoMes } from '../../shared/formato/datas'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { NumerosDoPonto } from './PecasDoPonto'
import { saldoComSinal, tomDoSaldo } from './rotulosDoPonto'
import { TabelaDoEspelho } from './TabelaDoEspelho'

/** Tela 04: a aba Ponto da ficha do RH (mês atual, sem as folgas). Sem "Lançar no banco de horas". */
export function AbaPonto({ ficha }: PropsDaAba) {
  const consulta = $api.useQuery('get', '/api/rh/funcionarios/{funcionarioId}/ponto', {
    params: { path: { funcionarioId: ficha.id } },
  })
  const espelho = consulta.data
  const resumo = espelho?.resumo
  return (
    <div className="flex flex-col gap-5">
      <NumerosDoPonto
        rotulo="Números do ponto"
        carregando={!resumo}
        itens={[
          {
            rotulo: 'Banco de horas',
            valor: saldoComSinal(resumo?.saldoBancoHorasMinutos ?? null),
            tom: tomDoSaldo(resumo?.saldoBancoHorasMinutos ?? null),
          },
          {
            rotulo: espelho ? `Horas em ${nomeDoMes(espelho.mes)}` : 'Horas no mês',
            valor: resumo ? formatarMinutosComoHoras(resumo.horasTrabalhadasNoMesMinutos) : '—',
          },
          {
            rotulo: 'Pedidos de ajuste em análise',
            valor: resumo ? formatarNumero(resumo.pedidosEmAnalise) : '—',
            tom: resumo && resumo.pedidosEmAnalise > 0 ? 'alerta' : 'neutro',
          },
        ]}
      />
      <TabelaDoEspelho
        rotulo="Ponto do mês"
        dias={espelho?.dias}
        semFolgas
        carregando={consulta.isLoading}
        erro={consulta.error ?? undefined}
        aoTentarDeNovo={() => void consulta.refetch()}
      />
      <div>
        <BotaoLink
          variante="secundario"
          to={
            espelho
              ? `/rh/ponto-e-feriados?funcionarioId=${ficha.id}&ano=${espelho.ano}&mes=${espelho.mes}`
              : `/rh/ponto-e-feriados?funcionarioId=${ficha.id}`
          }
        >
          Abrir espelho completo
        </BotaoLink>
      </div>
    </div>
  )
}
