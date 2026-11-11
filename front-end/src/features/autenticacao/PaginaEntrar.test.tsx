import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { exemplos } from '../../shared/mocks/gerado/handlers'
import { servidor } from '../../shared/mocks/servidor'
import { renderizarApp } from '../../test/renderizarApp'
import { tokenDeAcesso } from './armazenamentoDaSessao'

function problema(status: number, title: string, detail?: string) {
  return HttpResponse.json(
    { title, status, ...(detail ? { detail } : {}) },
    { status, headers: { 'content-type': 'application/problem+json' } },
  )
}

async function preencherEEntrar(senha = 'Senha#Forte123') {
  const usuario = userEvent.setup()
  await usuario.type(screen.getByLabelText('E-mail'), 'ana.souza@empresa.com.br')
  await usuario.type(screen.getByLabelText('Senha'), senha)
  await usuario.click(screen.getByRole('button', { name: 'Entrar' }))
  return usuario
}

beforeEach(() => {
  localStorage.clear()
  tokenDeAcesso.definir(null)
})

describe('PaginaEntrar', () => {
  it.each([
    [401, 'E-mail ou senha inválidos.'],
    [403, 'Seu acesso está desativado. Fale com o RH.'],
    [400, 'Preencha o e-mail e a senha.'],
    [429, 'Muitas tentativas. Aguarde um minuto e tente de novo.'],
    [500, 'Erro no servidor. Tente novamente em instantes.'],
  ])('PaginaEntrar mostra a mensagem certa para %i', async (status, mensagem) => {
    servidor.use(http.post('*/api/auth/entrar', () => problema(status, 'Titulo do servidor.')))
    renderizarApp()
    await preencherEEntrar()
    expect(await screen.findByRole('alert')).toHaveTextContent(mensagem)
  })

  it('PaginaEntrar mostra a mensagem certa para falha de rede', async () => {
    servidor.use(http.post('*/api/auth/entrar', () => HttpResponse.error()))
    renderizarApp()
    await preencherEEntrar()
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Não foi possível conectar. Tente novamente.',
    )
  })

  it('PaginaEntrar no 403 de perfil nao configurado mostra o detail do contrato', async () => {
    const detail = 'Seu perfil de acesso não está configurado. Fale com o RH.'
    servidor.use(http.post('*/api/auth/entrar', () => problema(403, 'Acesso negado.', detail)))
    renderizarApp()
    await preencherEEntrar()
    expect(await screen.findByRole('alert')).toHaveTextContent(detail)
  })

  it('PaginaEntrar com campos vazios mostra a mensagem e nao chama a API', async () => {
    let chamadas = 0
    servidor.use(
      http.post('*/api/auth/entrar', () => {
        chamadas += 1
        return HttpResponse.json(exemplos.entrar.corpo as Record<string, unknown>)
      }),
    )
    renderizarApp()
    await userEvent.setup().click(screen.getByRole('button', { name: 'Entrar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Preencha o e-mail e a senha.')
    expect(chamadas).toBe(0)
  })

  it('PaginaEntrar desabilita o botao enquanto envia', async () => {
    let liberar: () => void = () => {}
    const espera = new Promise<void>((resolver) => {
      liberar = resolver
    })
    servidor.use(
      http.post('*/api/auth/entrar', async () => {
        await espera
        return problema(401, 'Não autenticado.')
      }),
    )
    renderizarApp()
    await preencherEEntrar()
    await waitFor(() => expect(screen.getByRole('button', { name: 'Entrar' })).toBeDisabled())
    liberar()
    expect(await screen.findByRole('alert')).toHaveTextContent('E-mail ou senha inválidos.')
    expect(screen.getByRole('button', { name: 'Entrar' })).toBeEnabled()
  })

  it('PaginaEntrar envia a senha exatamente como digitada (espacos nas pontas contam)', async () => {
    let corpo: { senha?: string } = {}
    servidor.use(
      http.post('*/api/auth/entrar', async ({ request }) => {
        corpo = (await request.json()) as { senha?: string }
        return problema(401, 'Não autenticado.')
      }),
    )
    renderizarApp()
    await preencherEEntrar(' Senha#Forte123 ')
    await screen.findByRole('alert')
    expect(corpo.senha).toBe(' Senha#Forte123 ')
  })

  it('PaginaEntrar com clique duplo em Entrar envia uma unica requisicao', async () => {
    let chamadas = 0
    servidor.use(
      http.post('*/api/auth/entrar', async () => {
        chamadas += 1
        await new Promise((resolver) => setTimeout(resolver, 30))
        return problema(401, 'Não autenticado.')
      }),
    )
    renderizarApp()
    const usuario = userEvent.setup()
    await usuario.type(screen.getByLabelText('E-mail'), 'ana.souza@empresa.com.br')
    await usuario.type(screen.getByLabelText('Senha'), 'Senha#Forte123')
    await usuario.dblClick(screen.getByRole('button', { name: 'Entrar' }))
    await screen.findByRole('alert')
    expect(chamadas).toBe(1)
  })

  it('PaginaEntrar com sucesso troca o login pelo shell', async () => {
    renderizarApp()
    await preencherEEntrar()
    expect(await screen.findByText('Portal de RH')).toBeInTheDocument()
    expect(screen.queryByLabelText('Senha')).not.toBeInTheDocument()
  })
})
