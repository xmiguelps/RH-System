import { describe, expect, it } from 'vitest'
import { capturar, problema } from './capturar'

const BASE = 'https://api.exemplo.test'

describe('capturar', () => {
  it('capturar guarda a consulta, o corpo e os parametros do caminho e devolve o exemplo do contrato', async () => {
    const ficha = capturar('obterFichaDoFuncionario')
    const resposta = await fetch(BASE + '/api/rh/funcionarios/12')
    expect(ficha.quantas()).toBe(1)
    expect(ficha.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '12' })
    expect((await resposta.json()).nome).toBe('Bruno Lima')
  })

  it('conforme falha quando a tela manda uma chave a mais ao lado das certas', async () => {
    const lista = capturar('listarFuncionarios')
    await fetch(BASE + '/api/rh/funcionarios?pagina=1&tamanhoPagin=20')
    expect(() => lista.conforme()).toThrow(/tamanhoPagin/)
  })

  it('problema monta um problem+json com os campos extras e capturar o devolve', async () => {
    capturar(
      'criarFuncionario',
      problema(409, { codigo: 'X', cadastroExistente: { funcionarioId: 1 } }),
    )
    const resposta = await fetch(BASE + '/api/rh/funcionarios', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: '{}',
    })
    expect(resposta.status).toBe(409)
    expect(resposta.headers.get('content-type')).toContain('application/problem+json')
    expect(await resposta.json()).toMatchObject({
      status: 409,
      codigo: 'X',
      cadastroExistente: { funcionarioId: 1 },
    })
  })
})
