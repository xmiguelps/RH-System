import { Selecao, type OpcaoDeSelecao } from '../ui/Selecao'

const TODOS = '__TODOS__'

export function FiltroDeSelecao<V extends string>({
  rotulo,
  todos,
  opcoes,
  valor,
  aoMudar,
  carregando,
}: {
  rotulo: string
  todos: string
  opcoes: readonly OpcaoDeSelecao<V>[]
  valor: V | undefined
  aoMudar: (valor: V | null) => void
  carregando?: boolean
}) {
  const comTodos: OpcaoDeSelecao<string>[] = [{ valor: TODOS, rotulo: todos }, ...opcoes]
  return (
    <Selecao
      rotulo={rotulo}
      opcoes={comTodos}
      valor={valor ?? TODOS}
      aoMudar={(novo) => aoMudar(!novo || novo === TODOS ? null : (novo as V))}
      carregando={carregando}
    />
  )
}
