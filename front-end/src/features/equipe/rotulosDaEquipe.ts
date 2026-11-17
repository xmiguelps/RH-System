import type { Esquema } from '../../shared/api/tipos'
import { formatarDiaMesDaData } from '../../shared/formato/datas'

type Ponto = Esquema<'PainelPontoHoje'>

/** O "Ponto hoje" de cada pessoa, só por `situacaoHoje` (`CONVENCOES.md`, seção 10). */
export function textoDoPontoHoje(p: Ponto): string {
  switch (p.situacaoHoje) {
    case 'TRABALHANDO':
      return p.horario ? `Entrada ${p.horario}` : 'Trabalhando'
    case 'FERIAS':
      return p.feriasAte ? `De férias até ${formatarDiaMesDaData(p.feriasAte)}` : 'De férias'
    case 'SEM_REGISTRO':
      return 'Sem registro'
    case 'ATESTADO':
      return 'Atestado'
    case 'AINDA_NAO_COMECOU':
      return 'Ainda não começou'
    case 'SEM_EXPEDIENTE':
      return 'Sem expediente'
  }
}
