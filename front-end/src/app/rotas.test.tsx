import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { tokenDeAcesso } from '../features/autenticacao/armazenamentoDaSessao'
import { exemplos } from '../shared/mocks/gerado/handlers'
import { servidor } from '../shared/mocks/servidor'
import { renderizarApp } from '../test/renderizarApp'
import { entrarComo, sessaoDeTeste } from '../test/sessao'

beforeEach(() => {
  localStorage.clear()
  tokenDeAcesso.definir(null)
})

async function entrarPelaTela(perfil: 'RH' | 'Funcionario' | 'Gestor' = 'RH') {
  servidor.use(http.post('*/api/auth/entrar', () => HttpResponse.json(sessaoDeTeste(perfil))))
  const usuario = userEvent.setup()
  await usuario.type(await screen.findByLabelText('E-mail'), 'ana.souza@empresa.com.br')
  await usuario.type(screen.getByLabelText('Senha'), 'Senha#Forte123')
  await usuario.click(screen.getByRole('button', { name: 'Entrar' }))
}

describe('rotas', () => {
  it('sem sessao em /rh/painel vai a /entrar?voltar=%2Frh%2Fpainel e, depois de entrar, volta a /rh/painel', async () => {
    const { roteador } = renderizarApp({ rota: '/rh/painel' })
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/entrar'))
    expect(roteador.state.location.search).toBe('?voltar=%2Frh%2Fpainel')
    await entrarPelaTela('RH')
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/rh/painel'))
    expect(await screen.findByRole('heading', { level: 1, name: /, Ana$/ })).toBeInTheDocument()
  })

  it('voltar malicioso (//exemplo.com) cai no inicio do perfil', async () => {
    const { roteador } = renderizarApp({ rota: '/entrar?voltar=%2F%2Fexemplo.com' })
    await entrarPelaTela('Funcionario')
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/eu/inicio'))
  })

  it.each([
    ['RH', '/rh/painel'],
    ['Funcionario', '/eu/inicio'],
    ['Gestor', '/eu/inicio'],
  ] as const)('/ leva ao inicio do perfil (%s)', async (perfil, destino) => {
    entrarComo(perfil)
    const { roteador } = renderizarApp({ rota: '/' })
    await waitFor(() => expect(roteador.state.location.pathname).toBe(destino))
  })

  it('/entrar com sessao vai ao inicio', async () => {
    entrarComo('RH')
    const { roteador } = renderizarApp({ rota: '/entrar' })
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/rh/painel'))
  })

  it('gestor em /rh/painel ve a mensagem de 403 com o menu do gestor e o endereco mantido (Review Focus 4)', async () => {
    entrarComo('Gestor')
    const { roteador } = renderizarApp({ rota: '/rh/painel' })
    expect(
      await screen.findByText('Você não tem permissão para ver esta página.'),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: /Bom dia|Boa tarde|Boa noite/ }),
    ).not.toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Menu principal' })).toBeInTheDocument()
    expect(screen.getByText('Perfil gestor, setor Comercial')).toBeInTheDocument()
    expect(roteador.state.location.pathname).toBe('/rh/painel')
    expect(screen.getByRole('link', { name: 'Ir para o início' })).toHaveAttribute(
      'href',
      '/eu/inicio',
    )
  })

  it('funcionario em /gestor/equipe ve 403; qualquer perfil abre /eu/inicio', async () => {
    entrarComo('Funcionario')
    const { unmount } = renderizarApp({ rota: '/gestor/equipe' })
    expect(
      await screen.findByText('Você não tem permissão para ver esta página.'),
    ).toBeInTheDocument()
    unmount()
    for (const perfil of ['Funcionario', 'Gestor', 'RH'] as const) {
      tokenDeAcesso.definir(null)
      entrarComo(perfil)
      const r = renderizarApp({ rota: '/eu/inicio' })
      expect(await screen.findByRole('heading', { level: 1, name: /^Olá, / })).toBeInTheDocument()
      r.unmount()
    }
  })

  it('endereco desconhecido logado mostra "Página não encontrada." dentro do layout; deslogado vai a /entrar', async () => {
    entrarComo('RH')
    const logado = renderizarApp({ rota: '/colaboradores-que-nao-existe' })
    expect(await screen.findByText('Página não encontrada.')).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Menu principal' })).toBeInTheDocument()
    logado.unmount()
    localStorage.clear()
    tokenDeAcesso.definir(null)
    const { roteador } = renderizarApp({ rota: '/rh/qualquer' })
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/entrar'))
  })

  it('com refresh guardado mostra Carregando… (role status) sem piscar a tela de entrar e depois o inicio', async () => {
    let liberar: () => void = () => {}
    const espera = new Promise<void>((resolver) => {
      liberar = resolver
    })
    localStorage.setItem('rh.refresh', 'r-guardado')
    servidor.use(
      http.post('*/api/auth/renovar', async () => {
        await espera
        return HttpResponse.json(exemplos.renovarSessao.corpo as Record<string, unknown>)
      }),
    )
    renderizarApp({ rota: '/' })
    expect(screen.getByRole('status')).toHaveTextContent('Carregando')
    expect(screen.queryByRole('button', { name: 'Entrar' })).not.toBeInTheDocument()
    liberar()
    expect(await screen.findByText('Portal de RH')).toBeInTheDocument()
  })

  it('sem VITE_VITRINE a rota /eu/vitrine nao existe (404)', async () => {
    entrarComo('RH')
    renderizarApp({ rota: '/eu/vitrine' })
    expect(await screen.findByText('Página não encontrada.')).toBeInTheDocument()
  })
})
