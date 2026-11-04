import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { Janela } from './Janela'

describe('Janela', () => {
  it('Janela aberta tem role dialog nomeado pelo titulo, mostra descricao, corpo e rodape', () => {
    render(
      <Janela
        aberta
        aoFechar={() => {}}
        titulo="Adicionar documento"
        descricao="Envie um arquivo PDF."
        rodape={<button type="button">Salvar</button>}
      >
        <p>corpo da janela</p>
      </Janela>,
    )
    const janela = screen.getByRole('dialog', { name: 'Adicionar documento' })
    expect(janela).toHaveAccessibleDescription('Envie um arquivo PDF.')
    expect(screen.getByText('corpo da janela')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Salvar' })).toBeInTheDocument()
  })

  it('Janela fecha com Escape, com o X e clicando fora, e nao fecha clicando fora com fecharAoClicarFora false', async () => {
    const usuario = userEvent.setup()
    const aoFechar = vi.fn()
    const { unmount } = render(
      <Janela aberta aoFechar={aoFechar} titulo="Teste">
        <p>corpo</p>
      </Janela>,
    )
    await usuario.keyboard('{Escape}')
    expect(aoFechar).toHaveBeenCalledTimes(1)
    await usuario.click(screen.getByRole('button', { name: 'Fechar' }))
    expect(aoFechar).toHaveBeenCalledTimes(2)
    await usuario.click(screen.getByTestId('janela-fundo'))
    expect(aoFechar).toHaveBeenCalledTimes(3)
    unmount()

    const outro = vi.fn()
    render(
      <Janela aberta aoFechar={outro} titulo="Teste" fecharAoClicarFora={false}>
        <p>corpo</p>
      </Janela>,
    )
    await usuario.click(screen.getByTestId('janela-fundo'))
    expect(outro).not.toHaveBeenCalled()
  })

  it('Janela fechada nao escreve nada', () => {
    render(
      <Janela aberta={false} aoFechar={() => {}} titulo="Teste">
        <p>corpo</p>
      </Janela>,
    )
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.queryByText('corpo')).not.toBeInTheDocument()
  })

  it('Janela expoe data-posicao centro e direita', () => {
    const { unmount } = render(
      <Janela aberta aoFechar={() => {}} titulo="Centro">
        <p>a</p>
      </Janela>,
    )
    expect(screen.getByRole('dialog', { name: 'Centro' })).toHaveAttribute('data-posicao', 'centro')
    unmount()
    render(
      <Janela aberta aoFechar={() => {}} titulo="Lateral" posicao="direita" largura="larga">
        <p>b</p>
      </Janela>,
    )
    expect(screen.getByRole('dialog', { name: 'Lateral' })).toHaveAttribute(
      'data-posicao',
      'direita',
    )
  })
})
