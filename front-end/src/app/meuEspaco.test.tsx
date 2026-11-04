import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { renderizarTela } from '../test/telas'

/** O grupo do menu com este rótulo (o rótulo é um <p>; o grupo, o elemento que o contém). */
async function grupoDoMenu(rotulo: string): Promise<HTMLElement> {
  const menu = await screen.findByRole('navigation', { name: 'Menu principal' })
  const titulo = await within(menu).findByText(rotulo, { selector: 'p' })
  return titulo.parentElement!
}

describe('"Meu espaço" (decisão do dono, 2026-11-02; ficha 4, item 13)', { timeout: 15000 }, () => {
  it('RH: "Holerites" e "Minhas advertências" levam as telas pessoais, com o menu do RH a vista', async () => {
    const { user, roteador } = renderizarTela('/rh/painel')
    const meuEspaco = await grupoDoMenu('Meu espaço')
    await user.click(within(meuEspaco).getByRole('link', { name: 'Holerites' }))
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/eu/holerites'))
    expect(await screen.findByRole('heading', { level: 1, name: 'Holerites' })).toBeInTheDocument()
    const menu = screen.getByRole('navigation', { name: 'Menu principal' })
    expect(within(menu).getByRole('link', { name: 'Colaboradores' })).toBeInTheDocument()

    await user.click(
      within(await grupoDoMenu('Meu espaço')).getByRole('link', { name: 'Minhas advertências' }),
    )
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/eu/advertencias'))
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Minhas advertências' }),
    ).toBeInTheDocument()
    expect(within(menu).getByRole('link', { name: 'Cadastros' })).toBeInTheDocument()
  })

  it('Gestor: "Minhas advertências" no "Meu espaço"; "Holerites" e "Avisos" continuam nos grupos do Figma', async () => {
    const { user, roteador } = renderizarTela('/eu/inicio', { perfil: 'Gestor' })
    const meuEspaco = await grupoDoMenu('Meu espaço')
    expect(within(meuEspaco).queryByRole('link', { name: 'Holerites' })).toBeNull()
    expect(
      within(await grupoDoMenu('Seu dia a dia')).getByRole('link', { name: 'Holerites' }),
    ).toHaveAttribute('href', '/eu/holerites')
    expect(
      within(await grupoDoMenu('Comunicação')).getByRole('link', { name: /^Avisos/ }),
    ).toHaveAttribute('href', '/eu/avisos')
    await user.click(within(meuEspaco).getByRole('link', { name: 'Minhas advertências' }))
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/eu/advertencias'))
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Minhas advertências' }),
    ).toBeInTheDocument()
    expect(
      within(await grupoDoMenu('Seu setor')).getByRole('link', { name: 'Advertências' }),
    ).toHaveAttribute('href', '/gestor/advertencias')
  })

  it.each(['RH', 'Gestor'] as const)(
    '%s: "Meus formulários" no "Meu espaço" leva a /eu/formularios com o menu do perfil a vista (ficha 5)',
    async (perfil) => {
      const { user, roteador } = renderizarTela(perfil === 'RH' ? '/rh/painel' : '/eu/inicio', {
        perfil,
      })
      await user.click(
        within(await grupoDoMenu('Meu espaço')).getByRole('link', { name: /^Meus formulários/ }),
      )
      await waitFor(() => expect(roteador.state.location.pathname).toBe('/eu/formularios'))
      expect(
        await screen.findByRole('heading', { level: 1, name: 'Formulários' }),
      ).toBeInTheDocument()
      const menu = screen.getByRole('navigation', { name: 'Menu principal' })
      expect(
        within(menu).getByRole('link', {
          name: perfil === 'RH' ? 'Colaboradores' : /^Painel da equipe|^Ponto da equipe/,
        }),
      ).toBeInTheDocument()
    },
  )

  it.each(['RH', 'Gestor'] as const)(
    '%s: "Meus treinamentos" no "Meu espaço" leva a /eu/treinamentos (ficha 6)',
    async (perfil) => {
      const { user, roteador } = renderizarTela(perfil === 'RH' ? '/rh/painel' : '/eu/inicio', {
        perfil,
      })
      await user.click(
        within(await grupoDoMenu('Meu espaço')).getByRole('link', { name: 'Meus treinamentos' }),
      )
      await waitFor(() => expect(roteador.state.location.pathname).toBe('/eu/treinamentos'))
      expect(
        await screen.findByRole('heading', { level: 1, name: 'Meus treinamentos' }),
      ).toBeInTheDocument()
      expect(screen.getByRole('navigation', { name: 'Menu principal' })).toBeInTheDocument()
    },
  )

  it('Funcionario: "Treinamentos" em "Você na empresa"', async () => {
    renderizarTela('/eu/inicio', { perfil: 'Funcionario' })
    expect(
      within(await grupoDoMenu('Você na empresa')).getByRole('link', { name: 'Treinamentos' }),
    ).toHaveAttribute('href', '/eu/treinamentos')
  })

  it('Funcionario: "Formulários" em "Comunicação" e sem "Meu espaço"', async () => {
    renderizarTela('/eu/inicio', { perfil: 'Funcionario' })
    expect(
      within(await grupoDoMenu('Comunicação')).getByRole('link', { name: /^Formulários/ }),
    ).toHaveAttribute('href', '/eu/formularios')
  })

  it('Funcionario nao ganha o grupo "Meu espaço"', async () => {
    renderizarTela('/eu/inicio', { perfil: 'Funcionario' })
    const menu = await screen.findByRole('navigation', { name: 'Menu principal' })
    await within(menu).findByRole('link', { name: 'Holerites' })
    expect(within(menu).queryByText('Meu espaço', { selector: 'p' })).toBeNull()
  })
})
