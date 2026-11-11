import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { esperarRequisicaoConforme } from '../../test/contrato'
import { exemplos } from '../mocks/gerado/handlers'
import { servidor } from '../mocks/servidor'
import { criarClienteApi } from './clienteApi'
import { ErroDeApi } from './erroDeApi'
import { ehSessao, type Esquema } from './tipos'

const exemploDeSessao = exemplos.entrar.corpo as Record<string, unknown> & {
  usuario: Record<string, unknown>
}

describe('tipos do contrato', () => {
  it('ehSessao aceita o example do contrato e rejeita um objeto sem usuario', () => {
    expect(ehSessao(exemploDeSessao)).toBe(true)
    expect(ehSessao({ ...exemploDeSessao, usuario: undefined })).toBe(false)
    expect(ehSessao(null)).toBe(false)
  })

  it('ehSessao rejeita um perfil que o contrato nao conhece', () => {
    expect(
      ehSessao({ ...exemploDeSessao, usuario: { ...exemploDeSessao.usuario, perfil: 'Admin' } }),
    ).toBe(false)
  })

  it('ErroDeApi tem os campos do problem+json do contrato (os nomes sao os do contrato)', () => {
    const problema: Esquema<'ProblemaDeApi'> = new ErroDeApi(429, 'Muitas tentativas.', {
      detail: 'x',
    })
    expect(problema).toMatchObject({ status: 429, title: 'Muitas tentativas.', detail: 'x' })
  })

  it('entrar, renovar e sair enviam exatamente as chaves do contrato', async () => {
    const corpos: Record<string, Record<string, unknown>> = {}
    servidor.use(
      http.post('*/api/auth/entrar', async ({ request }) => {
        corpos.entrar = (await request.json()) as Record<string, unknown>
        return HttpResponse.json(exemplos.entrar.corpo as Record<string, unknown>)
      }),
      http.post('*/api/auth/renovar', async ({ request }) => {
        corpos.renovarSessao = (await request.json()) as Record<string, unknown>
        return HttpResponse.json(exemplos.renovarSessao.corpo as Record<string, unknown>)
      }),
      http.post('*/api/auth/sair', async ({ request }) => {
        corpos.sair = (await request.json()) as Record<string, unknown>
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const cliente = criarClienteApi('https://api.exemplo.test')
    await cliente.entrar('ana.souza@empresa.com.br', 'Senha#Forte123')
    await cliente.renovar('r1')
    await cliente.sair('r1')
    for (const operationId of ['entrar', 'renovarSessao', 'sair']) {
      esperarRequisicaoConforme(operationId, { corpo: corpos[operationId] })
    }
  })
})
