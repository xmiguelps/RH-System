import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { NotificacoesProvider } from './NotificacoesProvider'
import { useNotificacao } from './useNotificacao'

function Disparador() {
  const notificacao = useNotificacao()
  return (
    <>
      <button type="button" onClick={() => notificacao.sucesso('Pedido enviado.')}>
        sucesso
      </button>
      <button type="button" onClick={() => notificacao.erro('Não deu certo.')}>
        erro
      </button>
      <button type="button" onClick={() => notificacao.informacao('Lembrete enviado.')}>
        informacao
      </button>
    </>
  )
}

describe('Notificacoes', () => {
  it('useNotificacao().sucesso mostra a mensagem na regiao Notificacoes e o botao fecha', async () => {
    const usuario = userEvent.setup()
    render(
      <NotificacoesProvider>
        <Disparador />
      </NotificacoesProvider>,
    )
    await usuario.click(screen.getByRole('button', { name: 'sucesso' }))
    expect(await screen.findByText('Pedido enviado.')).toBeInTheDocument()
    expect(screen.getByRole('region', { name: /Notificações/ })).toHaveTextContent(
      'Pedido enviado.',
    )
    await usuario.click(screen.getByLabelText('Fechar aviso'))
    await waitFor(() => expect(screen.queryByText('Pedido enviado.')).not.toBeInTheDocument())
  })

  it('erro e informacao expoem data-tom', async () => {
    const usuario = userEvent.setup()
    render(
      <NotificacoesProvider>
        <Disparador />
      </NotificacoesProvider>,
    )
    await usuario.click(screen.getByRole('button', { name: 'erro' }))
    await usuario.click(screen.getByRole('button', { name: 'informacao' }))
    const erro = (await screen.findByText('Não deu certo.')).closest('[data-tom]')
    const info = (await screen.findByText('Lembrete enviado.')).closest('[data-tom]')
    expect(erro).toHaveAttribute('data-tom', 'erro')
    expect(info).toHaveAttribute('data-tom', 'informacao')
  })
})
