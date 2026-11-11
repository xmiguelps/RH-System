import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  CHAVE_DO_REFRESH,
  gravarRefresh,
  lerRefresh,
  limparRefresh,
  tokenDeAcesso,
} from './armazenamentoDaSessao'

beforeEach(() => {
  localStorage.clear()
  tokenDeAcesso.definir(null)
})

afterEach(() => {
  vi.restoreAllMocks()
})

describe('armazenamento da sessao', () => {
  it('grava, le e limpa o refresh na chave rh.refresh', () => {
    expect(CHAVE_DO_REFRESH).toBe('rh.refresh')
    expect(lerRefresh()).toBeNull()
    gravarRefresh('r1')
    expect(localStorage.getItem('rh.refresh')).toBe('r1')
    expect(lerRefresh()).toBe('r1')
    limparRefresh()
    expect(localStorage.getItem('rh.refresh')).toBeNull()
    expect(lerRefresh()).toBeNull()
  })

  it('nao lanca quando o localStorage lanca (navegacao privada, dados bloqueados)', () => {
    const bloqueado = () => {
      throw new DOMException('bloqueado', 'SecurityError')
    }
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(bloqueado)
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(bloqueado)
    vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(bloqueado)
    expect(lerRefresh()).toBeNull()
    expect(() => gravarRefresh('r1')).not.toThrow()
    expect(() => limparRefresh()).not.toThrow()
  })

  it('o token de acesso fica so em memoria', () => {
    tokenDeAcesso.definir('tok')
    expect(tokenDeAcesso.obter()).toBe('tok')
    expect(localStorage.length).toBe(0)
  })
})
