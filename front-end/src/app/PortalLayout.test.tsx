import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { tokenDeAcesso } from '../features/autenticacao/armazenamentoDaSessao'
import { renderizarApp } from '../test/renderizarApp'
import { entrarComo } from '../test/sessao'

beforeEach(() => {
  localStorage.clear()
  tokenDeAcesso.definir(null)
})

describe('PortalLayout', () => {
  it.each([
    ['RH', 'AS', 'Ana Souza', 'Perfil RH, acesso total'],
    ['Gestor', 'CR', 'Carlos Ribeiro', 'Perfil gestor, setor Comercial'],
    ['Funcionario', 'BL', 'Bruno Lima', 'Analista pleno, Financeiro'],
  ] as const)(
    'rodape do %s mostra iniciais, nome e a linha de apoio',
    async (perfil, ini, nome, apoio) => {
      entrarComo(perfil)
      renderizarApp({ rota: '/eu/inicio' })
      const rodape = await screen.findByTestId('rodape-do-menu')
      expect(within(rodape).getByText(ini)).toBeInTheDocument()
      expect(within(rodape).getByText(nome)).toBeInTheDocument()
      expect(within(rodape).getByText(apoio)).toBeInTheDocument()
      expect(within(rodape).getByRole('button', { name: 'Sair' })).toBeInTheDocument()
    },
  )

  it('Sair limpa a sessao, o localStorage e volta a tela de entrar', async () => {
    entrarComo('RH')
    const { roteador } = renderizarApp({ rota: '/rh/painel' })
    await userEvent.setup().click(await screen.findByRole('button', { name: 'Sair' }))
    expect(await screen.findByRole('button', { name: 'Entrar' })).toBeInTheDocument()
    expect(localStorage.getItem('rh.refresh')).toBeNull()
    expect(tokenDeAcesso.obter()).toBeNull()
    expect(roteador.state.location.pathname).toBe('/entrar')
  })

  it('nao tem item de menu com icone e o menu e uma nav nomeada', async () => {
    entrarComo('Funcionario')
    renderizarApp({ rota: '/eu/inicio' })
    const nav = await screen.findByRole('navigation', { name: 'Menu principal' })
    const links = within(nav).getAllByRole('link')
    expect(links.length).toBeGreaterThan(0)
    for (const link of links) expect(link.querySelector('svg')).toBeNull()
  })
})
