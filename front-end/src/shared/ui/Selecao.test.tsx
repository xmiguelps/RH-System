import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { Selecao } from './Selecao'

const OPCOES = [
  { valor: 'analista', rotulo: 'Analista' },
  { valor: 'gerente', rotulo: 'Gerente' },
] as const

describe('Selecao', () => {
  it('Selecao mostra o placeholder, abre, escolhe e chama aoMudar com o valor', async () => {
    const usuario = userEvent.setup()
    const aoMudar = vi.fn()
    render(
      <Selecao
        rotulo="Cargo"
        opcoes={OPCOES}
        valor={null}
        aoMudar={aoMudar}
        placeholder="Escolha o cargo"
      />,
    )
    const gatilho = screen.getByRole('combobox', { name: 'Cargo' })
    expect(gatilho).toHaveTextContent('Escolha o cargo')
    await usuario.click(gatilho)
    await usuario.click(await screen.findByRole('option', { name: 'Analista' }))
    expect(aoMudar).toHaveBeenCalledWith('analista')
  })

  it('Selecao mostra o rotulo da opcao escolhida', () => {
    render(<Selecao rotulo="Cargo" opcoes={OPCOES} valor="gerente" aoMudar={() => {}} />)
    expect(screen.getByRole('combobox', { name: 'Cargo' })).toHaveTextContent('Gerente')
  })

  it('Selecao carregando mostra Carregando… e nao abre', async () => {
    const usuario = userEvent.setup()
    render(<Selecao rotulo="Cargo" opcoes={OPCOES} valor={null} aoMudar={() => {}} carregando />)
    const gatilho = screen.getByRole('combobox', { name: 'Cargo' })
    expect(gatilho).toHaveTextContent('Carregando…')
    await usuario.click(gatilho)
    expect(screen.queryByRole('option', { name: 'Analista' })).not.toBeInTheDocument()
  })

  it('Selecao desabilitado nao abre', async () => {
    const usuario = userEvent.setup()
    render(<Selecao rotulo="Cargo" opcoes={OPCOES} valor={null} aoMudar={() => {}} desabilitado />)
    await usuario.click(screen.getByRole('combobox', { name: 'Cargo' }))
    expect(screen.queryByRole('option', { name: 'Analista' })).not.toBeInTheDocument()
  })
})
