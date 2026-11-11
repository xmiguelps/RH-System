import { CircleCheck, TriangleAlert } from 'lucide-react'
import type { Esquema } from '../../shared/api/tipos'
import { formatarDiaMesDaData } from '../../shared/formato/datas'
import { AreaDeTexto } from '../../shared/ui/AreaDeTexto'
import { Aviso } from '../../shared/ui/Aviso'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { textoDeDias } from './rotulosDasFerias'
import type { DadosDasFerias } from './useConferenciaDeFerias'

type Conferencia = Esquema<'FeriasConferencia'>
type Regra = Esquema<'FeriasRegraConferida'>

/** A lista de verificações: todas as regras que a API mandar, na ordem dela. */
export function ListaDeRegras({ regras }: { regras: readonly Regra[] }) {
  return (
    <ul aria-label="Verificações" className="flex flex-col gap-2">
      {regras.map((r) => (
        <li key={r.codigo} data-atendida={r.atendida} className="flex items-start gap-2 text-tabela text-texto">
          {r.atendida ? (
            <CircleCheck aria-label="Atendida" className="mt-0.5 h-4 w-4 shrink-0 text-primaria-media" />
          ) : (
            <TriangleAlert aria-label="Não atendida" className="mt-0.5 h-4 w-4 shrink-0 text-alerta-ponto" />
          )}
          <span>{r.mensagem}</span>
        </li>
      ))}
    </ul>
  )
}

/** Os campos das telas 35 e 06 (muda só a rota da conferência, no componente que usa este). */
export function CamposDasFerias({
  valores,
  aoMudar,
  conferencia,
  conferindo,
  erroDaConferencia,
  rotuloDoTexto,
  exemploDoTexto,
  comSobra,
  regrasDoErro,
}: {
  valores: DadosDasFerias
  aoMudar: (v: DadosDasFerias) => void
  conferencia: Conferencia | null
  conferindo: boolean
  erroDaConferencia: string | null
  rotuloDoTexto: string
  exemploDoTexto: string
  comSobra: boolean
  regrasDoErro?: readonly Regra[]
}) {
  const fimAntes = !!valores.dataInicio && !!valores.dataFim && valores.dataFim < valores.dataInicio
  const sobra = conferencia?.diasQueSobram ?? 0
  return (
    <div className="flex flex-col gap-5">
      <div className="grid grid-cols-2 gap-4">
        <Campo rotulo="Primeiro dia">
          <Entrada type="date" lang="pt-BR" value={valores.dataInicio} onChange={(e) => aoMudar({ ...valores, dataInicio: e.target.value })} />
        </Campo>
        <Campo rotulo="Último dia" erro={fimAntes ? 'O último dia não pode ser antes do primeiro.' : null}>
          <Entrada type="date" lang="pt-BR" value={valores.dataFim} onChange={(e) => aoMudar({ ...valores, dataFim: e.target.value })} />
        </Campo>
      </div>
      <div className="flex flex-col gap-1" aria-live="polite">
        <span className="text-tabela font-semibold text-texto">Total</span>
        {conferindo ? (
          <Esqueleto className="h-5 w-48" />
        ) : conferencia ? (
          <span className="text-corpo text-texto">
            {textoDeDias(conferencia.totalDias)} corridos.
            {comSobra &&
              (sobra >= 0 ? ` Sobram ${textoDeDias(sobra)}.` : ` Passa ${textoDeDias(-sobra)} do saldo.`)}
          </span>
        ) : (
          <span className="text-corpo text-texto-suave">Escolha o primeiro e o último dia.</span>
        )}
      </div>
      <CaixaDeSelecao
        marcada={valores.abonoPecuniario}
        aoMudar={(abonoPecuniario) => aoMudar({ ...valores, abonoPecuniario, diasAbono: abonoPecuniario ? valores.diasAbono || '10' : '' })}
        rotulo="Vender parte das férias (abono de até 10 dias)"
      />
      {valores.abonoPecuniario && (
        <div className="ml-8 w-40">
          <Campo rotulo="Quantos dias">
            <Entrada type="number" min={1} max={10} value={valores.diasAbono} onChange={(e) => aoMudar({ ...valores, diasAbono: e.target.value })} />
          </Campo>
        </div>
      )}
      <CaixaDeSelecao
        marcada={valores.adiantar13Salario}
        aoMudar={(adiantar13Salario) => aoMudar({ ...valores, adiantar13Salario })}
        rotulo="Adiantar a 1ª parcela do 13º salário"
      />
      {erroDaConferencia && <Aviso tom="erro">{erroDaConferencia}</Aviso>}
      {regrasDoErro && regrasDoErro.length > 0 ? (
        <ListaDeRegras regras={regrasDoErro} />
      ) : (
        conferencia && <ListaDeRegras regras={conferencia.regras} />
      )}
      {conferencia?.pagamentoAte && (
        <Aviso tom="alerta">
          O pagamento das férias precisa sair até 2 dias antes do início, ou seja, até{' '}
          {formatarDiaMesDaData(conferencia.pagamentoAte)}.
        </Aviso>
      )}
      <Campo rotulo={rotuloDoTexto}>
        <AreaDeTexto
          rows={3}
          limite={500}
          placeholder={exemploDoTexto}
          value={valores.texto}
          onChange={(e) => aoMudar({ ...valores, texto: e.target.value })}
        />
      </Campo>
    </div>
  )
}
