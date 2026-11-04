import type { Esquema } from '../../../shared/api/tipos'
import type { TomDoSelo } from '../../../shared/ui/Selo'

type Situacao = Esquema<'UsuarioSistemaSituacao'>

const ROTULOS: Record<Situacao, { texto: string; tom: TomDoSelo }> = {
  SEM_CONVITE: { texto: 'Sem convite', tom: 'neutro' },
  CONVITE_ENVIADO: { texto: 'Convite enviado', tom: 'info' },
  CONVITE_EXPIRADO: { texto: 'Convite vencido', tom: 'alerta' },
  SENHA_DEFINIDA: { texto: 'Senha definida', tom: 'sucesso' },
}

export function rotuloDaSituacaoDoLogin(situacao: Situacao): { texto: string; tom: TomDoSelo } {
  return ROTULOS[situacao] ?? { texto: situacao, tom: 'neutro' }
}

export const OPCOES_DE_SITUACAO_DO_LOGIN = (Object.keys(ROTULOS) as Situacao[]).map((valor) => ({
  valor,
  rotulo: ROTULOS[valor].texto,
}))
