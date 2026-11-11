import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { abrirLinkTemporario, baixarExportacao } from '../../shared/arquivos/baixar'
import { renderizarComProvedores } from '../../test/renderizar'
import Vitrine from './Vitrine'

vi.mock('../../shared/arquivos/baixar', () => ({
  abrirLinkTemporario: vi.fn(),
  baixarExportacao: vi.fn(async (chamada: Promise<unknown>) => {
    await chamada
  }),
}))

declare global {
  interface Window {
    __xss?: number
  }
}

afterEach(() => {
  vi.clearAllMocks()
})

describe('Vitrine', () => {
  it('a vitrine renderiza todos os componentes sem erro e sem HTML cru', () => {
    renderizarComProvedores(<Vitrine />)
    expect(
      screen.getByRole('heading', { level: 1, name: 'Vitrine de componentes' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: 'Cargo (vitrine)' })).toBeInTheDocument()
    expect(screen.getByRole('table', { name: 'Colaboradores (vitrine)' })).toBeInTheDocument()
    const markdown = screen.getByTestId('markdown-de-ataque')
    expect(within(markdown).queryByRole('img')).toBeNull()
    expect(markdown.querySelector('script')).toBeNull()
    expect(markdown.querySelector('img')).toBeNull()
    expect(window.__xss).toBeUndefined()
  })

  it('Abrir janela, Abrir painel e Abrir confirmacao abrem os dialogos e Escape os fecha', async () => {
    const usuario = userEvent.setup()
    renderizarComProvedores(<Vitrine />)
    for (const [botao, papel] of [
      ['Abrir janela', 'dialog'],
      ['Abrir painel', 'dialog'],
      ['Abrir confirmação', 'alertdialog'],
    ] as const) {
      await usuario.click(screen.getByRole('button', { name: botao }))
      expect(await screen.findByRole(papel)).toBeInTheDocument()
      await usuario.keyboard('{Escape}')
      await waitFor(() => expect(screen.queryByRole(papel)).not.toBeInTheDocument())
    }
  })

  it('Mostrar notificacao mostra a mensagem', async () => {
    const usuario = userEvent.setup()
    renderizarComProvedores(<Vitrine />)
    await usuario.click(screen.getByRole('button', { name: 'Mostrar notificação' }))
    expect(await screen.findByText('Alterações salvas.')).toBeInTheDocument()
  })

  it('Baixar CSV de teste e Abrir link de teste chamam baixarExportacao e abrirLinkTemporario', async () => {
    const usuario = userEvent.setup()
    renderizarComProvedores(<Vitrine />)
    await usuario.click(screen.getByRole('button', { name: 'Baixar CSV de teste' }))
    await waitFor(() => expect(baixarExportacao).toHaveBeenCalledTimes(1))
    expect(vi.mocked(baixarExportacao).mock.calls[0]?.[1]).toBe('auditoria.csv')
    await usuario.click(screen.getByRole('button', { name: 'Abrir link de teste' }))
    await waitFor(() => expect(abrirLinkTemporario).toHaveBeenCalledTimes(1))
    expect(vi.mocked(abrirLinkTemporario).mock.calls[0]?.[0]).toHaveProperty('url')
  })
})
