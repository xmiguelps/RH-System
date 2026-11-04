import { act, renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { ClienteApi } from '../../shared/api/clienteApi'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import type { Sessao } from '../../shared/api/tipos'
import { exemplos } from '../../shared/mocks/gerado/handlers'
import { tokenDeAcesso } from './armazenamentoDaSessao'
import { AuthProvider } from './AuthProvider'
import { useAuth } from './useAuth'

const sessaoBase = exemplos.entrar.corpo as Sessao

function sessao(refreshToken: string): Sessao {
  return { ...sessaoBase, refreshToken, usuario: { ...sessaoBase.usuario } }
}

function criarClienteFalso() {
  return {
    entrar: vi.fn<ClienteApi['entrar']>(),
    renovar: vi.fn<ClienteApi['renovar']>(),
    sair: vi.fn<ClienteApi['sair']>(),
    eu: vi.fn<ClienteApi['eu']>(),
  }
}

type ClienteFalso = ReturnType<typeof criarClienteFalso>

// StrictMode na raiz (reactStrictMode): medido em 2026-11-03 que, com o <StrictMode> só dentro do
// wrapper, o React 19.3 não repete o efeito de montagem e o teste da montagem dupla não prova nada.
function montar(cliente: ClienteFalso, estrito = false) {
  const wrapper = ({ children }: { children: ReactNode }) => (
    <AuthProvider cliente={cliente}>{children}</AuthProvider>
  )
  return renderHook(() => useAuth(), { wrapper, reactStrictMode: estrito })
}

beforeEach(() => {
  localStorage.clear()
  tokenDeAcesso.definir(null)
})

afterEach(() => {
  Reflect.deleteProperty(navigator, 'locks')
  vi.restoreAllMocks()
})

describe('AuthProvider', () => {
  it('AuthProvider renova a sessao ao montar quando ha refresh no localStorage (inclusive depois de "fechar e reabrir")', async () => {
    localStorage.setItem('rh.refresh', 'r-guardado')
    const cliente = criarClienteFalso()
    cliente.renovar.mockResolvedValueOnce(sessao('r-novo')).mockResolvedValueOnce(sessao('r-3'))
    const primeira = montar(cliente)
    expect(primeira.result.current.carregando).toBe(true)
    await waitFor(() => expect(primeira.result.current.usuario?.nome).toBe('Ana Souza'))
    expect(primeira.result.current.carregando).toBe(false)
    expect(cliente.renovar).toHaveBeenCalledWith('r-guardado')
    expect(localStorage.getItem('rh.refresh')).toBe('r-novo')

    primeira.unmount()
    tokenDeAcesso.definir(null)
    const segunda = montar(cliente)
    await waitFor(() => expect(segunda.result.current.usuario?.nome).toBe('Ana Souza'))
    expect(cliente.renovar).toHaveBeenLastCalledWith('r-novo')
  })

  it('AuthProvider sem refresh guardado nao chama a API e fica sem usuario', () => {
    const cliente = criarClienteFalso()
    const { result } = montar(cliente)
    expect(result.current.carregando).toBe(false)
    expect(result.current.usuario).toBeNull()
    expect(cliente.renovar).not.toHaveBeenCalled()
  })

  it('AuthProvider em StrictMode (montagem dupla) chama renovar uma unica vez', async () => {
    localStorage.setItem('rh.refresh', 'r-guardado')
    const cliente = criarClienteFalso()
    cliente.renovar.mockResolvedValue(sessao('r-novo'))
    const { result } = montar(cliente, true)
    await waitFor(() => expect(result.current.usuario?.nome).toBe('Ana Souza'))
    await waitFor(() => expect(result.current.carregando).toBe(false))
    await new Promise((resolver) => setTimeout(resolver, 20))
    expect(cliente.renovar).toHaveBeenCalledTimes(1)
  })

  it('AuthProvider com renovacao que falha ao montar (e o refresh nao mudou) limpa o localStorage e fica sem usuario', async () => {
    localStorage.setItem('rh.refresh', 'r-guardado')
    const cliente = criarClienteFalso()
    cliente.renovar.mockRejectedValue(new ErroDeApi(401, 'Não autenticado.'))
    const { result } = montar(cliente)
    await waitFor(() => expect(result.current.carregando).toBe(false))
    expect(result.current.usuario).toBeNull()
    expect(localStorage.getItem('rh.refresh')).toBeNull()
    expect(cliente.renovar).toHaveBeenCalledTimes(1)
  })

  it('AuthProvider com 401 na renovacao, quando outra aba ja gravou um refresh novo, tenta uma vez com o novo e continua logado', async () => {
    localStorage.setItem('rh.refresh', 'r-velho')
    const cliente = criarClienteFalso()
    cliente.renovar
      .mockImplementationOnce(async () => {
        localStorage.setItem('rh.refresh', 'r-da-outra-aba')
        throw new ErroDeApi(401, 'Não autenticado.')
      })
      .mockResolvedValueOnce(sessao('r-final'))
    const { result } = montar(cliente)
    await waitFor(() => expect(result.current.usuario?.nome).toBe('Ana Souza'))
    expect(cliente.renovar).toHaveBeenCalledTimes(2)
    expect(cliente.renovar).toHaveBeenNthCalledWith(1, 'r-velho')
    expect(cliente.renovar).toHaveBeenNthCalledWith(2, 'r-da-outra-aba')
    expect(localStorage.getItem('rh.refresh')).toBe('r-final')
  })

  it('AuthProvider com falha de rede na renovacao mantem o refresh guardado e fica sem usuario', async () => {
    localStorage.setItem('rh.refresh', 'r-guardado')
    const cliente = criarClienteFalso()
    cliente.renovar.mockRejectedValue(ErroDeApi.semConexao('x'))
    const { result } = montar(cliente)
    await waitFor(() => expect(result.current.carregando).toBe(false))
    expect(result.current.usuario).toBeNull()
    expect(localStorage.getItem('rh.refresh')).toBe('r-guardado')
  })

  it('AuthProvider pede o lock rh.refresh ao renovar quando o navegador tem navigator.locks', async () => {
    const request = vi.fn((_nome: string, tarefa: () => Promise<unknown>) => tarefa())
    Object.defineProperty(navigator, 'locks', { value: { request }, configurable: true })
    localStorage.setItem('rh.refresh', 'r-guardado')
    const cliente = criarClienteFalso()
    cliente.renovar.mockResolvedValue(sessao('r-novo'))
    const { result } = montar(cliente)
    await waitFor(() => expect(result.current.usuario?.nome).toBe('Ana Souza'))
    expect(request).toHaveBeenCalledTimes(1)
    expect(request.mock.calls[0]?.[0]).toBe('rh.refresh')
  })

  it('AuthProvider.entrar guarda o refresh no localStorage e o token de acesso so em memoria', async () => {
    const cliente = criarClienteFalso()
    cliente.entrar.mockResolvedValue(sessao('r-entrar'))
    const { result } = montar(cliente)
    await act(() => result.current.entrar('ana.souza@empresa.com.br', 'Senha#Forte123'))
    expect(Object.keys(localStorage)).toEqual(['rh.refresh'])
    expect(localStorage.getItem('rh.refresh')).toBe('r-entrar')
    expect(tokenDeAcesso.obter()).toBe(sessaoBase.tokenDeAcesso)
    expect(JSON.stringify({ ...localStorage })).not.toContain(sessaoBase.tokenDeAcesso)
    expect(result.current.usuario).toMatchObject({
      cargo: 'Gerente de RH',
      setor: 'Recursos Humanos',
    })
  })

  it('AuthProvider.entrar com erro nao guarda nada e propaga o erro', async () => {
    const cliente = criarClienteFalso()
    const falha = new ErroDeApi(401, 'Não autenticado.')
    cliente.entrar.mockRejectedValue(falha)
    const { result } = montar(cliente)
    await expect(act(() => result.current.entrar('a@b.c', 'x'))).rejects.toBe(falha)
    expect(localStorage.length).toBe(0)
    expect(result.current.usuario).toBeNull()
  })

  it('AuthProvider.sair limpa o localStorage e o usuario', async () => {
    localStorage.setItem('rh.refresh', 'r-guardado')
    const cliente = criarClienteFalso()
    cliente.renovar.mockResolvedValue(sessao('r-novo'))
    cliente.sair.mockResolvedValue()
    const { result } = montar(cliente)
    await waitFor(() => expect(result.current.usuario).not.toBeNull())
    await act(() => result.current.sair())
    expect(result.current.usuario).toBeNull()
    expect(localStorage.getItem('rh.refresh')).toBeNull()
    expect(tokenDeAcesso.obter()).toBeNull()
    expect(cliente.sair).toHaveBeenCalledWith('r-novo')
  })

  it('AuthProvider.sair limpa a sessao mesmo se a API falhar', async () => {
    localStorage.setItem('rh.refresh', 'r-guardado')
    const cliente = criarClienteFalso()
    cliente.renovar.mockResolvedValue(sessao('r-novo'))
    cliente.sair.mockRejectedValue(ErroDeApi.semConexao('x'))
    const { result } = montar(cliente)
    await waitFor(() => expect(result.current.usuario).not.toBeNull())
    await act(() => result.current.sair())
    expect(result.current.usuario).toBeNull()
    expect(localStorage.getItem('rh.refresh')).toBeNull()
  })

  it('AuthProvider desloga quando outra aba remove rh.refresh (evento storage)', async () => {
    localStorage.setItem('rh.refresh', 'r-guardado')
    const cliente = criarClienteFalso()
    cliente.renovar.mockResolvedValue(sessao('r-novo'))
    const { result } = montar(cliente)
    await waitFor(() => expect(result.current.usuario).not.toBeNull())
    act(() => {
      localStorage.removeItem('rh.refresh')
      window.dispatchEvent(
        new StorageEvent('storage', { key: 'rh.refresh', oldValue: 'r-novo', newValue: null }),
      )
    })
    expect(result.current.usuario).toBeNull()
    expect(tokenDeAcesso.obter()).toBeNull()
    expect(cliente.sair).not.toHaveBeenCalled()
  })

  it('AuthProvider nao desloga quando outra aba so troca o refresh (renovacao)', async () => {
    localStorage.setItem('rh.refresh', 'r-guardado')
    const cliente = criarClienteFalso()
    cliente.renovar.mockResolvedValue(sessao('r-novo'))
    const { result } = montar(cliente)
    await waitFor(() => expect(result.current.usuario).not.toBeNull())
    act(() => {
      localStorage.setItem('rh.refresh', 'b')
      window.dispatchEvent(
        new StorageEvent('storage', { key: 'rh.refresh', oldValue: 'r-novo', newValue: 'b' }),
      )
    })
    expect(result.current.usuario?.nome).toBe('Ana Souza')
  })

  it('useAuth fora do AuthProvider lanca erro', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    expect(() => renderHook(() => useAuth())).toThrow('AuthProvider')
  })
})
