import { formatarData, hojeEmSaoPaulo } from '../../shared/formato/datas'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { OpcoesEmCartao } from '../../shared/ui/OpcoesEmCartao'
import { Selecao } from '../../shared/ui/Selecao'
import { anosDoHolerite, useDiaDoPagamento } from './diaDoPagamento'
import { OPCOES_DE_MES, OPCOES_DE_TIPO, type QuandoPublicar } from './rotulosDosHolerites'
import type { Esquema } from '../../shared/api/tipos'

type Tipo = Esquema<'HoleriteTipo'>

/** "Quando publicar" do modal 08, do "Substituir" e do lote (a terceira opção é a dúvida D-F4-1). */
export function CampoQuandoPublicar({
  quando,
  aoMudarQuando,
  data,
  aoMudarData,
  erroDaData,
}: {
  quando: QuandoPublicar
  aoMudarQuando: (q: QuandoPublicar) => void
  data: string
  aoMudarData: (d: string) => void
  erroDaData?: string
}) {
  const pagamento = useDiaDoPagamento()
  const semDia = !pagamento.carregando && pagamento.data === null
  return (
    <div className="flex flex-col gap-3">
      <OpcoesEmCartao<QuandoPublicar>
        rotulo="Quando publicar"
        colunas={3}
        valor={quando}
        aoMudar={aoMudarQuando}
        opcoes={[
          { valor: 'AGORA', titulo: 'Publicar agora' },
          {
            valor: 'DIA_DO_PAGAMENTO',
            titulo: pagamento.data
              ? `Agendar para o dia do pagamento (${formatarData(pagamento.data)})`
              : 'Agendar para o dia do pagamento',
            descricao: semDia ? 'Configure o dia do pagamento em Cadastros, Empresa.' : undefined,
            desabilitada: semDia,
          },
          { valor: 'DATA', titulo: 'Agendar para uma data' },
        ]}
      />
      {quando === 'DATA' && (
        <div className="w-60">
          <Campo rotulo="Publicar no dia" erro={erroDaData}>
            <Entrada
              type="date"
              lang="pt-BR"
              min={hojeEmSaoPaulo()}
              value={data}
              onChange={(e) => aoMudarData(e.target.value)}
            />
          </Campo>
        </div>
      )}
    </div>
  )
}

/** Mês, ano e tipo (modal 08 e lote). */
export function CamposDaCompetencia({
  mes,
  ano,
  tipo,
  aoMudar,
}: {
  mes: string
  ano: string
  tipo: Tipo
  aoMudar: (campo: 'mes' | 'ano' | 'tipo', valor: string) => void
}) {
  const hoje = hojeEmSaoPaulo()
  const futura = `${ano}-${mes.padStart(2, '0')}` > hoje.slice(0, 7)
  return (
    <div className="flex flex-col gap-2">
      <div className="grid grid-cols-3 gap-4">
        <Campo rotulo="Mês">
          <Selecao
            rotulo="Mês"
            opcoes={OPCOES_DE_MES}
            valor={mes}
            aoMudar={(v) => v && aoMudar('mes', v)}
          />
        </Campo>
        <Campo rotulo="Ano">
          <Selecao
            rotulo="Ano"
            opcoes={anosDoHolerite()}
            valor={ano}
            aoMudar={(v) => v && aoMudar('ano', v)}
          />
        </Campo>
        <Campo rotulo="Tipo">
          <Selecao
            rotulo="Tipo"
            opcoes={OPCOES_DE_TIPO}
            valor={tipo}
            aoMudar={(v) => v && aoMudar('tipo', v)}
          />
        </Campo>
      </div>
      {futura && tipo !== 'FERIAS' && (
        <p className="text-pequeno text-alerta-texto">
          Só o holerite de Férias pode ter competência futura.
        </p>
      )}
    </div>
  )
}
