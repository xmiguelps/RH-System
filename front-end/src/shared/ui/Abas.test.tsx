import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { Abas } from './Abas'

const ITENS = [
  { valor: 'ativos', rotulo: 'Ativos', contagem: 101 },
  { valor: 'inativos', rotulo: 'Inativos', contagem: 7 },
  { valor: 'todos', rotulo: 'Todos' },
] as const

type Aba = (typeof ITENS)[number]['valor']

describe('Abas', () => {
  it('Abas mostra o rotulo com a contagem, marca a atual e chama aoMudar', async () => {
    const usuario = userEvent.setup()
    const aoMudar = vi.fn()
    render(<Abas rotulo="Situação" itens={ITENS} valor="ativos" aoMudar={aoMudar} />)
    expect(screen.getByRole('tab', { name: 'Ativos 101' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('tab', { name: 'Inativos 7' })).toHaveAttribute(
      'aria-selected',
      'false',
    )
    await usuario.click(screen.getByRole('tab', { name: 'Inativos 7' }))
    expect(aoMudar).toHaveBeenCalledWith('inativos')
  })

  it('Abas troca de aba com as setas e expoe data-variante', async () => {
    const usuario = userEvent.setup()
    function Teste() {
      const [valor, setValor] = useState<Aba>('ativos')
      return (
        <Abas
          rotulo="Situação"
          itens={ITENS}
          valor={valor}
          aoMudar={setValor}
          variante="sublinhada"
        />
      )
    }
    render(<Teste />)
    expect(screen.getByRole('tablist', { name: 'Situação' })).toHaveAttribute(
      'data-variante',
      'sublinhada',
    )
    await usuario.click(screen.getByRole('tab', { name: 'Ativos 101' }))
    await usuario.keyboard('{ArrowRight}')
    expect(screen.getByRole('tab', { name: 'Inativos 7' })).toHaveAttribute('aria-selected', 'true')
  })

  it('Abas com renderizarPainel mostra so o painel da aba atual', () => {
    render(
      <Abas
        rotulo="Ficha"
        itens={ITENS}
        valor="inativos"
        aoMudar={() => {}}
        renderizarPainel={(valor) => <p>painel {valor}</p>}
      />,
    )
    expect(screen.getByText('painel inativos')).toBeInTheDocument()
    expect(screen.queryByText('painel ativos')).not.toBeInTheDocument()
  })
})
