import { describe, expect, it } from 'vitest'
import { caminhosDasRotas, ROTAS_EU, ROTAS_GESTOR, ROTAS_RH } from './tabelaDeRotas'

describe('tabelaDeRotas', () => {
  it('caminhosDasRotas tem /rh/painel e /eu/inicio, e nao tem /eu/vitrine sem VITE_VITRINE', () => {
    const caminhos = caminhosDasRotas()
    expect(caminhos.has('/rh/painel')).toBe(true)
    expect(caminhos.has('/eu/inicio')).toBe(true)
    expect(caminhos.has('/eu/vitrine')).toBe(false)
  })

  it('todo caminho de ROTAS_RH, ROTAS_GESTOR e ROTAS_EU e relativo (sem barra no comeco) e sem repeticao', () => {
    for (const rotas of [ROTAS_RH, ROTAS_GESTOR, ROTAS_EU]) {
      const caminhos = rotas.map((r) => r.caminho)
      for (const caminho of caminhos) expect(caminho.startsWith('/'), caminho).toBe(false)
      expect(new Set(caminhos).size).toBe(caminhos.length)
    }
  })
})
