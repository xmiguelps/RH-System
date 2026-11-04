import { Toast } from '@base-ui/react/toast'
import { useMemo } from 'react'

export function useNotificacao(): {
  sucesso(mensagem: string): void
  erro(mensagem: string): void
  informacao(mensagem: string): void
} {
  // Só a função `add`: o gerente muda a cada notificação, e depender dele faria quem usa
  // o retorno num efeito notificar de novo (medido: laço "Maximum update depth").
  const { add } = Toast.useToastManager()
  return useMemo(
    () => ({
      sucesso: (mensagem: string) => void add({ title: mensagem, type: 'sucesso' }),
      erro: (mensagem: string) => void add({ title: mensagem, type: 'erro' }),
      informacao: (mensagem: string) => void add({ title: mensagem, type: 'informacao' }),
    }),
    [add],
  )
}
