import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { CampoDeBusca } from './CampoDeBusca'
import { ComboDeBusca } from './ComboDeBusca'

const SETORES = [
  { valor: '1', rotulo: 'Logística' },
  { valor: '2', rotulo: 'Financeiro' },
  { valor: '3', rotulo: 'Comercial', apoio: '8 pessoas' },
]

describe('ComboDeBusca', () => {
  it('ComboDeBusca filtra digitando, sem diferenca de acento, e escolhe', async () => {
    const usuario = userEvent.setup()
    const aoMudar = vi.fn()
    render(<ComboDeBusca rotulo="Setor" opcoes={SETORES} valor={null} aoMudar={aoMudar} />)
    await usuario.type(screen.getByRole('combobox', { name: 'Setor' }), 'logis')
    expect(await screen.findByRole('option', { name: /Logística/ })).toBeInTheDocument()
    expect(screen.queryByRole('option', { name: /Financeiro/ })).not.toBeInTheDocument()
    await usuario.click(screen.getByRole('option', { name: /Logística/ }))
    expect(aoMudar).toHaveBeenCalledWith('1')
  })

  it('ComboDeBusca mostra Nenhum resultado. e, com aoDigitar, chama a funcao e nao filtra por conta propria', async () => {
    const usuario = userEvent.setup()
    const { unmount } = render(
      <ComboDeBusca rotulo="Setor" opcoes={SETORES} valor={null} aoMudar={() => {}} />,
    )
    await usuario.type(screen.getByRole('combobox', { name: 'Setor' }), 'xyz')
    expect(await screen.findByText('Nenhum resultado.')).toBeInTheDocument()
    unmount()

    const aoDigitar = vi.fn()
    render(
      <ComboDeBusca
        rotulo="Setor"
        opcoes={SETORES}
        valor={null}
        aoMudar={() => {}}
        aoDigitar={aoDigitar}
      />,
    )
    await usuario.type(screen.getByRole('combobox', { name: 'Setor' }), 'xyz')
    expect(aoDigitar).toHaveBeenLastCalledWith('xyz')
    expect(await screen.findByRole('option', { name: /Financeiro/ })).toBeInTheDocument()
  })
})

describe('CampoDeBusca', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('CampoDeBusca chama aoMudar uma vez, 300 ms depois da ultima tecla (Review Focus 5)', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const usuario = userEvent.setup({ advanceTimers: vi.advanceTimersByTime, delay: null })
    const aoMudar = vi.fn()
    render(<CampoDeBusca valor="" aoMudar={aoMudar} rotulo="Buscar colaborador" />)
    await usuario.type(screen.getByRole('searchbox', { name: 'Buscar colaborador' }), 'ana')
    expect(aoMudar).toHaveBeenCalledTimes(0)
    vi.advanceTimersByTime(300)
    expect(aoMudar).toHaveBeenCalledTimes(1)
    expect(aoMudar).toHaveBeenCalledWith('ana')
  })

  it('CampoDeBusca com Enter chama na hora', async () => {
    const usuario = userEvent.setup()
    const aoMudar = vi.fn()
    render(<CampoDeBusca valor="" aoMudar={aoMudar} rotulo="Buscar" esperaMs={5000} />)
    await usuario.type(screen.getByRole('searchbox', { name: 'Buscar' }), 'bruno{Enter}')
    expect(aoMudar).toHaveBeenCalledTimes(1)
    expect(aoMudar).toHaveBeenCalledWith('bruno')
  })

  it('CampoDeBusca acompanha o valor que muda por fora', () => {
    const { rerender } = render(<CampoDeBusca valor="ana" aoMudar={() => {}} rotulo="Buscar" />)
    expect(screen.getByRole('searchbox', { name: 'Buscar' })).toHaveValue('ana')
    rerender(<CampoDeBusca valor="" aoMudar={() => {}} rotulo="Buscar" />)
    expect(screen.getByRole('searchbox', { name: 'Buscar' })).toHaveValue('')
  })
})
