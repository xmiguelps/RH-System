import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import type { ClienteApi } from '../../shared/api/clienteApi'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { renovadorDeAcesso } from '../../shared/api/renovadorDeAcesso'
import type { Sessao, Usuario } from '../../shared/api/tipos'
import {
  CHAVE_DO_REFRESH,
  gravarRefresh,
  lerRefresh,
  limparRefresh,
  tokenDeAcesso,
} from './armazenamentoDaSessao'
import { ContextoDeAutenticacao, type ValorDoContexto } from './contextoDeAutenticacao'

type Props = { cliente: ClienteApi; children: ReactNode }

function ehNaoAutenticado(erro: unknown): boolean {
  return erro instanceof ErroDeApi && erro.status === 401
}

async function comTrava(tarefa: () => Promise<void>): Promise<void> {
  const travas = typeof navigator === 'undefined' ? undefined : navigator.locks
  if (travas) {
    await travas.request(CHAVE_DO_REFRESH, tarefa)
    return
  }
  await tarefa()
}

export function AuthProvider({ cliente, children }: Props) {
  const [usuario, setUsuario] = useState<Usuario | null>(null)
  const [carregando, setCarregando] = useState(() => lerRefresh() !== null)
  const renovacaoEmAndamento = useRef<Promise<void> | null>(null)

  const aplicarSessao = useCallback((sessao: Sessao) => {
    tokenDeAcesso.definir(sessao.tokenDeAcesso)
    gravarRefresh(sessao.refreshToken)
    setUsuario(sessao.usuario)
  }, [])

  const esquecerSessao = useCallback((apagarRefresh: boolean) => {
    tokenDeAcesso.definir(null)
    if (apagarRefresh) limparRefresh()
    setUsuario(null)
  }, [])

  const renovarDentroDaTrava = useCallback(async () => {
    // Relê dentro da trava: outra aba pode ter acabado de renovar.
    const usado = lerRefresh()
    if (!usado) {
      esquecerSessao(false)
      return
    }
    try {
      aplicarSessao(await cliente.renovar(usado))
      return
    } catch (erro) {
      if (!ehNaoAutenticado(erro)) {
        // Rede ou servidor: o refresh ainda pode valer; fica sem usuário e não o apaga.
        esquecerSessao(false)
        return
      }
    }
    const atual = lerRefresh()
    if (atual && atual !== usado) {
      try {
        aplicarSessao(await cliente.renovar(atual))
        return
      } catch (erro) {
        esquecerSessao(ehNaoAutenticado(erro))
        return
      }
    }
    esquecerSessao(true)
  }, [aplicarSessao, cliente, esquecerSessao])

  const renovar = useCallback((): Promise<void> => {
    if (!renovacaoEmAndamento.current) {
      renovacaoEmAndamento.current = comTrava(renovarDentroDaTrava).finally(() => {
        renovacaoEmAndamento.current = null
      })
    }
    return renovacaoEmAndamento.current
  }, [renovarDentroDaTrava])

  useEffect(() => {
    if (lerRefresh() === null) return
    void renovar().finally(() => setCarregando(false))
  }, [renovar])

  // Para o cliente HTTP: renova quando uma chamada de negócio recebe 401 e devolve o token novo
  // (ou null, quando a sessão acabou e já foi limpa).
  const renovarAcesso = useCallback(async (): Promise<string | null> => {
    await renovar()
    return tokenDeAcesso.obter()
  }, [renovar])

  useEffect(() => {
    renovadorDeAcesso.definir(renovarAcesso)
    return () => renovadorDeAcesso.definir(null)
  }, [renovarAcesso])

  useEffect(() => {
    const aoMudarArmazenamento = (evento: StorageEvent) => {
      if ((evento.key === CHAVE_DO_REFRESH || evento.key === null) && evento.newValue === null) {
        tokenDeAcesso.definir(null)
        setUsuario(null)
      }
    }
    window.addEventListener('storage', aoMudarArmazenamento)
    return () => window.removeEventListener('storage', aoMudarArmazenamento)
  }, [])

  const entrar = useCallback(
    async (email: string, senha: string) => {
      aplicarSessao(await cliente.entrar(email, senha))
    },
    [aplicarSessao, cliente],
  )

  const sair = useCallback(async () => {
    const refresh = lerRefresh()
    esquecerSessao(true)
    if (!refresh) return
    try {
      await cliente.sair(refresh)
    } catch {
      // a sessão local já foi limpa; a falha da API não muda nada para a pessoa
    }
  }, [cliente, esquecerSessao])

  const valor = useMemo<ValorDoContexto>(
    () => ({ usuario, carregando, entrar, sair }),
    [usuario, carregando, entrar, sair],
  )

  return <ContextoDeAutenticacao.Provider value={valor}>{children}</ContextoDeAutenticacao.Provider>
}
