import { screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { tokenDeAcesso } from '../../features/autenticacao/armazenamentoDaSessao'
import { renderizarApp } from '../../test/renderizarApp'
import { entrarComo } from '../../test/sessao'

beforeEach(() => {
  localStorage.clear()
  tokenDeAcesso.definir(null)
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
})

afterEach(() => {
  vi.useRealTimers()
})

describe('inicio minimo', () => {
  it('inicio do RH escreve "Bom dia, Ana" e "Quarta-feira, 7 de outubro"', async () => {
    entrarComo('RH')
    renderizarApp({ rota: '/rh/painel' })
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Bom dia, Ana' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Quarta-feira, 7 de outubro')).toBeInTheDocument()
  })

  it('inicio do funcionario escreve "Olá, Bruno"', async () => {
    entrarComo('Funcionario')
    renderizarApp({ rota: '/eu/inicio' })
    expect(await screen.findByRole('heading', { level: 1, name: 'Olá, Bruno' })).toBeInTheDocument()
    expect(screen.getByText('Quarta-feira, 7 de outubro')).toBeInTheDocument()
  })
})
