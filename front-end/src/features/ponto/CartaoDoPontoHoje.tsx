import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import type { Esquema } from '../../shared/api/tipos'
import { formatarDiaMesDaData, formatarHora } from '../../shared/formato/datas'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { cn } from '../../shared/ui/estilo'
import { Saldo } from './PecasDoPonto'
import { rotuloDoDiaDaSemana } from './rotulosDoPonto'

type PontoHoje = Esquema<'PontoHoje'>
type Marcacao = Esquema<'PontoMarcacao'>

const QUADROS = ['Entrada', 'Saída almoço', 'Volta', 'Saída'] as const
const DIAS_DA_SEMANA = [
  'DOMINGO',
  'SEGUNDA',
  'TERCA',
  'QUARTA',
  'QUINTA',
  'SEXTA',
  'SABADO',
] as const

function marcacoesDoDia(hoje: PontoHoje): Marcacao[] {
  return hoje.periodos
    .flatMap((p) => [p.entrada, p.saida])
    .filter((m): m is Marcacao => m !== null)
    .sort((a, b) => a.ordem - b.ordem)
}

/** "quarta-feira 7/10", do dia de calendário (sem passar o dia por fuso). */
function diaPorExtenso(data: string): string {
  const [ano, mes, dia] = data.split('-').map(Number)
  const semana = DIAS_DA_SEMANA[new Date(Date.UTC(ano ?? 0, (mes ?? 1) - 1, dia ?? 1)).getUTCDay()]
  const nome = semana ? rotuloDoDiaDaSemana(semana) : ''
  const comFeira = semana === 'SABADO' || semana === 'DOMINGO' ? nome : `${nome}-feira`
  return `${comFeira} ${dia}/${mes}`
}

function textoDoBancoAtualizado(atualizadoAte: string | null, dataDoPonto: string): string {
  if (!atualizadoAte) return 'Banco de horas ainda não atualizado.'
  const [ano, mes, dia] = dataDoPonto.split('-').map(Number)
  const ontem = new Date(Date.UTC(ano ?? 0, (mes ?? 1) - 1, (dia ?? 1) - 1))
    .toISOString()
    .slice(0, 10)
  return atualizadoAte === ontem
    ? 'Atualizado até ontem'
    : `Atualizado até ${formatarDiaMesDaData(atualizadoAte)}`
}

/**
 * O relógio usa o `agoraUtc` do servidor: a diferença para o relógio do navegador só anima os minutos.
 * Nenhuma regra (qual batida, se pode bater) depende do relógio do navegador.
 */
function useRelogioDoServidor(agoraUtc: string | undefined): string {
  const [agora, setAgora] = useState<string | null>(null)
  useEffect(() => {
    if (!agoraUtc) return
    const diferenca = new Date(agoraUtc).getTime() - Date.now()
    const atualizar = () => setAgora(new Date(Date.now() + diferenca).toISOString())
    atualizar()
    const id = window.setInterval(atualizar, 1000)
    return () => window.clearInterval(id)
  }, [agoraUtc])
  return agora ? formatarHora(agora) : '--:--'
}

export function CartaoDoPontoHoje({
  variante,
  aoPedirAjuste,
  aoEnviarAtestado,
}: {
  variante: 'completo' | 'reduzido'
  aoPedirAjuste?: () => void
  aoEnviarAtestado?: () => void
}) {
  const queryClient = useQueryClient()
  const consulta = $api.useQuery('get', '/api/eu/ponto/hoje')
  const registrar = $api.useMutation('post', '/api/eu/ponto/marcacoes')
  const [devolvido, setDevolvido] = useState<PontoHoje | null>(null)
  const [erro, setErro] = useState<unknown>(null)
  const hoje = devolvido ?? consulta.data
  const relogio = useRelogioDoServidor(hoje?.agoraUtc)
  // isPending só muda depois de renderizar: o clique duplo precisa de uma trava síncrona.
  const batendo = useRef(false)

  async function bater() {
    if (!hoje || batendo.current) return
    batendo.current = true
    setErro(null)
    try {
      const novo = await registrar.mutateAsync({
        body: { tipoEsperado: hoje.proximaMarcacao.tipo },
      })
      setDevolvido(novo)
      await invalidarRota(queryClient, '/api/eu/ponto')
    } catch (e) {
      setErro(e)
      if (e instanceof ErroDeApi && e.status === 409) {
        setDevolvido(null)
        await consulta.refetch()
      }
    } finally {
      batendo.current = false
    }
  }

  if (consulta.error && !hoje) {
    return (
      <section aria-label="Seu ponto hoje" className="rounded-cartao bg-primaria p-6 text-white">
        <AvisoDeErro
          titulo="Não foi possível carregar o seu ponto."
          erro={consulta.error}
          aoTentarDeNovo={() => void consulta.refetch()}
        />
      </section>
    )
  }

  const marcacoes = hoje ? marcacoesDoDia(hoje) : []
  // A chave troca o botão a cada batida: o 2º clique de um clique duplo cai no botão antigo.
  const botao = (
    <Botao
      key={`${hoje?.proximaMarcacao.tipo ?? 'nada'}-${marcacoes.length}`}
      variante="claro"
      onClick={() => void bater()}
      carregando={registrar.isPending}
      disabled={!hoje}
    >
      {hoje?.proximaMarcacao.textoDoBotao ?? 'Registrar'}
    </Botao>
  )
  const avisoDeErro = !!erro && (
    <div className="text-texto">
      <AvisoDeErro erro={erro} />
    </div>
  )

  if (variante === 'reduzido') {
    const entrada = marcacoes.find((m) => m.ordem === 1)
    const saida = marcacoes.length >= 4 ? marcacoes.at(-1) : undefined
    return (
      <section
        aria-label="Seu ponto hoje"
        className="flex items-center justify-between gap-6 rounded-cartao bg-primaria p-6 text-white"
      >
        <div className="flex flex-col gap-2">
          <span className="text-corpo">
            {hoje ? `Hoje, ${diaPorExtenso(hoje.dataDoPonto)}` : 'Hoje'}
          </span>
          <span aria-label="Agora" className="text-relogio font-bold tabular-nums">
            {relogio}
          </span>
          <div className="flex flex-wrap gap-2">
            <span className="rounded-full bg-white/15 px-3 py-1 text-pequeno">
              Entrada {entrada ? formatarHora(entrada.instanteUtc) : '—'}
            </span>
            <span className="rounded-full bg-white/15 px-3 py-1 text-pequeno">
              {saida ? `Saída ${formatarHora(saida.instanteUtc)}` : 'Saída: aguardando registro'}
            </span>
          </div>
        </div>
        <div className="flex flex-col items-end gap-3">
          {botao}
          {avisoDeErro}
        </div>
      </section>
    )
  }

  const proximaOrdem = marcacoes.length + 1
  return (
    <div className="grid grid-cols-[2fr_1fr] gap-5">
      <section
        aria-label="Seu ponto hoje"
        className="flex flex-col gap-5 rounded-cartao bg-primaria p-6 text-white"
      >
        <div className="flex items-baseline justify-between gap-3">
          <h2 className="text-t3 font-bold">Seu ponto hoje</h2>
          {hoje?.jornada.horarioEntrada && hoje.jornada.horarioSaida && (
            <span className="text-pequeno">
              Jornada das {hoje.jornada.horarioEntrada} às {hoje.jornada.horarioSaida}
            </span>
          )}
        </div>
        <span aria-label="Agora" className="text-relogio font-bold tabular-nums">
          {relogio}
        </span>
        <ol className="grid grid-cols-4 gap-3">
          {QUADROS.map((rotulo, i) => {
            const marcacao = marcacoes.find((m) => m.ordem === i + 1)
            const proxima = !!hoje && i + 1 === proximaOrdem
            return (
              <li
                key={rotulo}
                aria-current={proxima ? 'step' : undefined}
                className={cn(
                  'flex flex-col gap-1 rounded-campo border border-white/20 p-3',
                  proxima && 'border-white',
                )}
              >
                <span className="text-pequeno">{rotulo}</span>
                <span className="text-t3 font-bold tabular-nums">
                  {marcacao ? formatarHora(marcacao.instanteUtc) : '—'}
                </span>
              </li>
            )
          })}
        </ol>
        <div className="flex flex-wrap items-center gap-4">
          {botao}
          {aoPedirAjuste && (
            <Botao variante="texto" className="text-white" onClick={aoPedirAjuste}>
              Pedir ajuste de ponto
            </Botao>
          )}
          {aoEnviarAtestado && (
            <Botao variante="texto" className="text-white" onClick={aoEnviarAtestado}>
              Enviar atestado
            </Botao>
          )}
        </div>
        {avisoDeErro}
      </section>
      <section
        aria-label="Banco de horas"
        className="flex flex-col gap-2 rounded-cartao border border-borda bg-superficie p-6"
      >
        <h2 className="text-t3 font-bold text-texto">Banco de horas</h2>
        {hoje ? (
          <>
            <Saldo minutos={hoje.bancoHoras.saldoMinutos} className="text-numero font-bold" />
            <span className="text-pequeno text-texto-suave">
              {textoDoBancoAtualizado(hoje.bancoHoras.atualizadoAte, hoje.dataDoPonto)}
            </span>
          </>
        ) : (
          <span className="text-texto-suave">—</span>
        )}
        <Link to="/eu/meu-ponto/extrato" className="text-corpo font-semibold text-primaria">
          Ver extrato
        </Link>
      </section>
    </div>
  )
}
