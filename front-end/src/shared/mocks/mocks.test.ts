// @vitest-environment node
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { parse } from 'yaml'
import { exemplos, handlers, versaoDoContrato } from './gerado/handlers'

const BASE = 'https://api.exemplo.test'
const METODOS = ['get', 'post', 'put', 'patch', 'delete']

type Contrato = { info: { version: string }; paths: Record<string, Record<string, unknown>> }

describe('dados simulados gerados do contrato', () => {
  it('ha um handler por operacao do contrato e a versao e a do contrato', () => {
    const contrato = parse(readFileSync('../contrato/openapi.yaml', 'utf8')) as Contrato
    let n = 0
    for (const operacoes of Object.values(contrato.paths)) {
      for (const metodo of Object.keys(operacoes)) {
        if (METODOS.includes(metodo)) n += 1
      }
    }
    expect(n).toBeGreaterThan(0)
    expect(Object.keys(exemplos)).toHaveLength(n)
    expect(handlers).toHaveLength(n)
    expect(versaoDoContrato).toBe(contrato.info.version)
  })

  it('as rotas de autenticacao devolvem o example do contrato', async () => {
    const entrar = await fetch(BASE + '/api/auth/entrar', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ email: 'ana.souza@empresa.com.br', senha: 'x' }),
    })
    const sessao = await entrar.json()
    expect(sessao.usuario).toMatchObject({
      nome: 'Ana Souza',
      perfil: 'RH',
      cargo: 'Gerente de RH',
      setor: 'Recursos Humanos',
    })

    const sair = await fetch(BASE + '/api/auth/sair', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ refreshToken: 'r1' }),
    })
    expect(sair.status).toBe(204)

    const saude = await fetch(BASE + '/health')
    expect(await saude.json()).toMatchObject({ status: 'ok' })
  })

  it('rota literal vence a rota com parametro (resumo antes de {avisoId})', async () => {
    const resumo = await (await fetch(BASE + '/api/rh/avisos/resumo')).json()
    expect(resumo).toHaveProperty('contagens')
    const aviso = await (await fetch(BASE + '/api/rh/avisos/409')).json()
    expect(aviso.id).toBe(409)
  })

  it('exportacao devolve o tipo de arquivo do contrato', async () => {
    const resposta = await fetch(BASE + exemplos.exportarAuditoria.caminho)
    expect(resposta.headers.get('content-type')).toContain('text/csv')
  })
})
