import type { Esquema } from '../../shared/api/tipos'

type Tipo = Esquema<'PainelPendenciaTipo'>

export const TITULO_DA_PENDENCIA: Record<Tipo, string> = {
  AJUSTE_DE_PONTO: 'Pedidos de ajuste de ponto',
  FERIAS: 'Pedidos de férias',
  ATESTADO: 'Atestados para validar',
  ADMISSAO_COM_DOCUMENTOS_PENDENTES: 'Admissão com documentos pendentes',
  BENEFICIOS: 'Pedidos de benefícios',
}

export const OPCOES_DE_TIPO_DE_PENDENCIA = (Object.keys(TITULO_DA_PENDENCIA) as Tipo[]).map(
  (valor) => ({ valor, rotulo: TITULO_DA_PENDENCIA[valor] }),
)

/** Para onde "Revisar" leva (`05-caminhos-de-tela.md`, "De onde cada tela abre"). */
export function destinoDaPendencia(pendencia: {
  tipo: Tipo
  referenciaId: number
  funcionarioId: number
}): string {
  switch (pendencia.tipo) {
    case 'AJUSTE_DE_PONTO':
      return `/rh/ponto-e-feriados?pedido=${pendencia.referenciaId}`
    case 'FERIAS':
      return `/rh/pedidos-de-ferias?pedido=${pendencia.referenciaId}`
    case 'ATESTADO':
      return '/rh/ponto-e-feriados/atestados'
    case 'ADMISSAO_COM_DOCUMENTOS_PENDENTES':
      return `/rh/colaboradores/${pendencia.funcionarioId}?aba=documentos`
    case 'BENEFICIOS':
      return '/rh/pedidos-de-beneficios'
  }
}
