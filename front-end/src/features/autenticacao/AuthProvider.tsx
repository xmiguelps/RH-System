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

async function comTrava<T>(tarefa: () => Promise<T>): Promise<T> {
  const travas = typeof navigator === 'undefined' ? undefined : navigator.locks
  if (travas) return travas.request(CHAVE_DO_REFRESH, tarefa)
  return tarefa()
}

export function AuthProvider({ cliente, children }: Props) {
  const [usuario, setUsuario] = useState<Usuario | null>(null)
  const [carregando, setCarregando] = useState(() => lerRefresh() !== null)
  const [falhaDeConexao, setFalhaDeConexao] = useState(false)
  const renovacaoEmAndamento = useRef<Promise<boolean> | null>(null)
  // Conta as vezes que a sessão mudou por ação da pessoa (entrar, sair, sair em outra aba): uma renovação que
  // começou antes e termina depois traz uma sessão que a pessoa já deixou para trás e não pode ressuscitá-la.
  const geracao = useRef(0)

  const aplicarSessao = useCallback((sessao: Sessao) => {
    tokenDeAcesso.definir(sessao.tokenDeAcesso)
    gravarRefresh(sessao.refreshToken)
    setFalhaDeConexao(false)
    setUsuario(sessao.usuario)
  }, [])

  const esquecerSessao = useCallback((apagarRefresh: boolean) => {
    tokenDeAcesso.definir(null)
    if (apagarRefresh) limparRefresh()
    setFalhaDeConexao(false)
    setUsuario(null)
  }, [])

  /** Devolve `true` só quando a sessão renovada foi aplicada. */
  const renovarDentroDaTrava = useCallback(async (): Promise<boolean> => {
    const minha = geracao.current
    const aplicarSeAindaVale = (sessao: Sessao): boolean => {
      if (geracao.current !== minha) {
        // a pessoa saiu (ou entrou de novo) enquanto isto voltava: o refresh novo não fica com ninguém
        void cliente.sair(sessao.refreshToken).catch(() => undefined)
        return false
      }
      aplicarSessao(sessao)
      return true
    }
    // Relê dentro da trava: outra aba pode ter acabado de renovar.
    const usado = lerRefresh()
    if (!usado) {
      esquecerSessao(false)
      return false
    }
    try {
      return aplicarSeAindaVale(await cliente.renovar(usado))
    } catch (erro) {
      if (!ehNaoAutenticado(erro)) {
        // Rede ou servidor: o refresh ainda pode valer. Quem já estava logado continua logado
        // (a chamada que pediu a renovação é que falha); quem abriu o site vê "Não foi possível conectar".
        setFalhaDeConexao(true)
        return false
      }
    }
    const atual = lerRefresh()
    if (atual && atual !== usado) {
      try {
        return aplicarSeAindaVale(await cliente.renovar(atual))
      } catch (erro) {
        if (ehNaoAutenticado(erro)) esquecerSessao(true)
        else setFalhaDeConexao(true)
        return false
      }
    }
    esquecerSessao(true)
    return false
  }, [aplicarSessao, cliente, esquecerSessao])

  const renovar = useCallback((): Promise<boolean> => {
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

  const tentarDeNovo = useCallback(async () => {
    setFalhaDeConexao(false)
    setCarregando(true)
    try {
      await renovar()
    } finally {
      setCarregando(false)
    }
  }, [renovar])

  // Para o cliente HTTP: renova quando uma chamada de negócio recebe 401 e devolve o token novo
  // (ou null, quando a sessão acabou e já foi limpa).
  const renovarAcesso = useCallback(async (): Promise<string | null> => {
    const renovou = await renovar()
    return renovou ? tokenDeAcesso.obter() : null
  }, [renovar])

  useEffect(() => {
    renovadorDeAcesso.definir(renovarAcesso)
    return () => renovadorDeAcesso.definir(null)
  }, [renovarAcesso])

  useEffect(() => {
    const aoMudarArmazenamento = (evento: StorageEvent) => {
      if ((evento.key === CHAVE_DO_REFRESH || evento.key === null) && evento.newValue === null) {
        geracao.current += 1
        tokenDeAcesso.definir(null)
        setUsuario(null)
      }
    }
    window.addEventListener('storage', aoMudarArmazenamento)
    return () => window.removeEventListener('storage', aoMudarArmazenamento)
  }, [])

  const entrar = useCallback(
    async (email: string, senha: string) => {
      const nova = await cliente.entrar(email, senha)
      geracao.current += 1
      aplicarSessao(nova)
    },
    [aplicarSessao, cliente],
  )

  const sair = useCallback(async () => {
    const refresh = lerRefresh()
    geracao.current += 1
    esquecerSessao(true)
    if (!refresh) return
    try {
      await cliente.sair(refresh)
    } catch {
      // a sessão local já foi limpa; a falha da API não muda nada para a pessoa
    }
  }, [cliente, esquecerSessao])

  const valor = useMemo<ValorDoContexto>(
    () => ({ usuario, carregando, falhaDeConexao, tentarDeNovo, entrar, sair }),
    [usuario, carregando, falhaDeConexao, tentarDeNovo, entrar, sair],
  )

  return <ContextoDeAutenticacao.Provider value={valor}>{children}</ContextoDeAutenticacao.Provider>
}
