import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createRef } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { mascararCpf } from '../formato/documentos'
import { AreaDeTexto } from './AreaDeTexto'
import { Entrada } from './Entrada'

describe('Entrada', () => {
  it('Entrada com mascara escreve o cpf formatado enquanto digita', async () => {
    const usuario = userEvent.setup()
    const aoMudar = vi.fn()
    render(
      <Entrada aria-label="CPF" mascara={mascararCpf} onChange={(e) => aoMudar(e.target.value)} />,
    )
    await usuario.type(screen.getByLabelText('CPF'), '84639271069')
    expect(screen.getByLabelText('CPF')).toHaveValue('846.392.710-69')
    expect(aoMudar).toHaveBeenLastCalledWith('846.392.710-69')
  })

  it('Entrada aceita ref e props do register do react-hook-form', async () => {
    const usuario = userEvent.setup()
    const ref = createRef<HTMLInputElement>()
    const aoMudar = vi.fn()
    const aoSair = vi.fn()
    render(<Entrada aria-label="Nome" name="nome" ref={ref} onChange={aoMudar} onBlur={aoSair} />)
    expect(ref.current).toBeInstanceOf(HTMLInputElement)
    expect(ref.current?.name).toBe('nome')
    await usuario.type(screen.getByLabelText('Nome'), 'Ana')
    await usuario.tab()
    expect(aoMudar).toHaveBeenCalled()
    expect(aoSair).toHaveBeenCalledTimes(1)
  })
})

describe('AreaDeTexto', () => {
  it('AreaDeTexto com limite mostra o contador', async () => {
    const usuario = userEvent.setup()
    render(<AreaDeTexto aria-label="Motivo" limite={280} />)
    expect(screen.getByText('0/280')).toBeInTheDocument()
    await usuario.type(screen.getByLabelText('Motivo'), 'abc')
    expect(screen.getByText('3/280')).toBeInTheDocument()
  })
})
