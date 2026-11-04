import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { MenuDeAcoes } from './MenuDeAcoes'

describe('MenuDeAcoes com dica', () => {
  it('item desabilitado com dica mostra a dica como descricao acessivel', async () => {
    const user = userEvent.setup()
    render(
      <MenuDeAcoes
        rotulo="Mais ações"
        acoes={[
          {
            chave: 'convite',
            rotulo: 'Reenviar convite',
            aoEscolher: () => {},
            desabilitada: true,
            dica: 'Esta pessoa não tem e-mail corporativo cadastrado.',
          },
        ]}
      />,
    )
    await user.click(screen.getByRole('button', { name: 'Mais ações' }))
    const item = await screen.findByRole('menuitem', { name: 'Reenviar convite' })
    expect(item).toHaveAccessibleDescription('Esta pessoa não tem e-mail corporativo cadastrado.')
    expect(item).toHaveAttribute('aria-disabled', 'true')
  })
})
