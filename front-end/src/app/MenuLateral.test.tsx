import { QueryClient } from '@tanstack/react-query'
import { act, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { tokenDeAcesso } from '../features/autenticacao/armazenamentoDaSessao'
import { AuthProvider } from '../features/autenticacao/AuthProvider'
import { criarClienteApi } from '../shared/api/clienteApi'
import type { Perfil } from '../shared/api/tipos'
import { servidor } from '../shared/mocks/servidor'
import { renderizarComProvedores } from '../test/renderizar'
import { entrarComo } from '../test/sessao'
import { MenuLateral } from './MenuLateral'
import { todosOsCaminhosDoMenu } from './menu'
import { invalidarContadoresDoMenu } from './useContadoresDoMenu'

function montar(perfil: Perfil, rota = '/', queryClient?: QueryClient) {
  entrarComo(perfil)
  return renderizarComProvedores(
    <AuthProvider cliente={criarClienteApi('https://api.exemplo.test')}>
      <MenuLateral caminhosExistentes={todosOsCaminhosDoMenu(perfil)} />
    </AuthProvider>,
    { rota, queryClient },
  )
}

const problema = (status: number, corpo: Record<string, unknown>) =>
  HttpResponse.json(corpo, { status, headers: { 'content-type': 'application/problem+json' } })

beforeEach(() => {
  localStorage.clear()
  tokenDeAcesso.definir(null)
})

describe('MenuLateral', () => {
  it('mostra a marca, o nome da empresa de obterEmpresa e some a segunda linha com 404 EMPRESA_NAO_CONFIGURADA', async () => {
    const { unmount } = montar('RH')
    expect(await screen.findByText('Comercial Aurora Ltda')).toBeInTheDocument()
    expect(screen.getByText('Portal de RH')).toBeInTheDocument()
    unmount()
    servidor.use(
      http.get('*/api/eu/empresa', () =>
        problema(404, {
          title: 'Empresa não configurada.',
          status: 404,
          codigo: 'EMPRESA_NAO_CONFIGURADA',
        }),
      ),
    )
    montar('RH')
    expect(await screen.findByText('Portal de RH')).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByText('Comercial Aurora Ltda')).not.toBeInTheDocument())
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it.each([
    [
      'RH',
      [
        ['Ponto e feriados', '4'],
        ['Meus formulários', '1'],
        ['Meus documentos', '1'],
      ],
    ],
    [
      'Funcionario',
      [
        ['Avisos', '2'],
        ['Formulários', '1'],
      ],
    ],
    [
      'Gestor',
      [
        ['Painel da equipe', '3'],
        ['Avisos', '2'],
        ['Meus formulários', '1'],
      ],
    ],
  ] as const)('bolinhas do %s com os exemplos do contrato', async (perfil, esperadas) => {
    montar(perfil)
    for (const [rotulo, numero] of esperadas) {
      const link = await screen.findByRole('link', { name: new RegExp(`^${rotulo}`) })
      await waitFor(() => expect(within(link).getByText(numero)).toBeInTheDocument())
    }
  })

  it('0 nao mostra bolinha', async () => {
    servidor.use(
      http.get('*/api/eu/contadores-do-menu', () =>
        HttpResponse.json({ avisosNaoLidos: 0, formulariosPendentes: 0, documentosPendentes: 0 }),
      ),
    )
    montar('Funcionario')
    const avisos = await screen.findByRole('link', { name: /^Avisos/ })
    await new Promise((r) => setTimeout(r, 30))
    expect(avisos).toHaveTextContent(/^Avisos$/)
  })

  it('falha ao buscar os contadores nao mostra mensagem nem bolinha', async () => {
    servidor.use(
      http.get('*/api/eu/contadores-do-menu', () => problema(500, { title: 'Erro.', status: 500 })),
    )
    montar('Funcionario')
    const avisos = await screen.findByRole('link', { name: /^Avisos/ })
    await new Promise((r) => setTimeout(r, 30))
    expect(avisos).toHaveTextContent(/^Avisos$/)
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('o item da pagina atual tem aria-current=page', async () => {
    montar('Funcionario', '/eu/ferias')
    expect(await screen.findByRole('link', { name: /^Férias/ })).toHaveAttribute(
      'aria-current',
      'page',
    )
    expect(screen.getByRole('link', { name: /^Início/ })).not.toHaveAttribute('aria-current')
  })

  it('invalidarContadoresDoMenu refaz as tres consultas', async () => {
    const chamadas: string[] = []
    servidor.use(
      http.get('*/api/eu/contadores-do-menu', () => {
        chamadas.push('eu')
        return HttpResponse.json({
          avisosNaoLidos: 1,
          formulariosPendentes: 0,
          documentosPendentes: 0,
        })
      }),
      http.get('*/api/rh/contadores-do-menu', () => {
        chamadas.push('rh')
        return HttpResponse.json({ pedidosDeAjusteDePonto: 1 })
      }),
    )
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    montar('RH', '/', qc)
    await waitFor(() => expect(chamadas).toEqual(expect.arrayContaining(['eu', 'rh'])))
    const antes = chamadas.length
    await act(() => invalidarContadoresDoMenu(qc))
    await waitFor(() => expect(chamadas.length).toBeGreaterThanOrEqual(antes + 2))
  })
})
