import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { invalidarContadoresDoMenu } from '../../app/useContadoresDoMenu'
import { clienteHttp } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { AreaDeTexto } from '../../shared/ui/AreaDeTexto'
import { Aviso } from '../../shared/ui/Aviso'
import { Botao } from '../../shared/ui/Botao'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { cn } from '../../shared/ui/estilo'
import { Janela } from '../../shared/ui/Janela'
import { Selo } from '../../shared/ui/Selo'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { ListaDeRegras } from './CamposDasFerias'
import { extrasDoPedido, frasesDaDecisao, periodoCurto, SITUACAO_DAS_FERIAS, textoDeDias } from './rotulosDasFerias'

type Pedido = Esquema<'FeriasPedidoAnalise'>
type Regra = Esquema<'FeriasRegraConferida'>
export type QuemDecide = 'gestor' | 'rh'

function regrasDe(e: unknown): Regra[] | undefined {
  return e instanceof ErroDeApi && Array.isArray(e.corpo?.regras) ? (e.corpo.regras as Regra[]) : undefined
}

/** Mostra o `title` e o `detail` nos 422 do RH (PEDIDO_PROPRIO e PEDIDO_RH_FORA_DE_GESTORES). */
function textoDoErro(e: unknown): string {
  if (e instanceof ErroDeApi && (e.codigo === 'PEDIDO_PROPRIO' || e.codigo === 'PEDIDO_RH_FORA_DE_GESTORES'))
    return `${e.title} ${e.detail ?? ''}`.trim()
  return mensagemDeErro(e)
}

function ModalRecusar({
  nome,
  aoFechar,
  aoConfirmar,
}: {
  nome: string
  aoFechar: () => void
  aoConfirmar: (motivo: string) => Promise<unknown>
}) {
  const [motivo, setMotivo] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  async function confirmar() {
    const texto = motivo.trim()
    if (texto.length < 5) return setErro('Escreva o motivo com 5 a 500 caracteres.')
    setEnviando(true)
    try {
      const falha = await aoConfirmar(texto)
      if (falha) setErro(textoDoErro(falha))
    } finally {
      setEnviando(false)
    }
  }
  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo="Recusar pedido de férias"
      descricao={`${nome} vê o motivo no histórico e no e-mail.`}
      rodape={
        <>
          <Botao variante="secundario" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao variante="perigo" onClick={() => void confirmar()} carregando={enviando}>
            Recusar
          </Botao>
        </>
      }
    >
      <Campo rotulo="Motivo" erro={erro}>
        <AreaDeTexto rows={4} limite={500} value={motivo} onChange={(e) => setMotivo(e.target.value)} />
      </Campo>
    </Janela>
  )
}

function ModalSugerir({ pedido, aoFechar }: { pedido: Pedido; aoFechar: () => void }) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const [inicio, setInicio] = useState('')
  const [fim, setFim] = useState('')
  const [erro, setErro] = useState<unknown>(null)
  const [enviando, setEnviando] = useState(false)
  async function enviar() {
    if (!inicio || !fim || fim < inicio) {
      setErro(new ErroDeApi(400, 'Datas', { detail: 'Escolha o primeiro e o último dia (o último não pode ser antes).' }))
      return
    }
    setEnviando(true)
    setErro(null)
    try {
      await clienteHttp.POST('/api/gestor/ferias/{feriasId}/sugerir-outra-data', {
        params: { path: { feriasId: pedido.id } },
        body: { dataInicio: inicio, dataFim: fim },
      })
      notificacao.sucesso(`Sugestão enviada. ${pedido.funcionario.nome} responde.`)
      await Promise.all([
        invalidarRota(queryClient, '/api/gestor/'),
        invalidarContadoresDoMenu(queryClient),
      ])
      aoFechar()
    } catch (e) {
      setErro(e)
      if (e instanceof ErroDeApi && e.status === 409) await invalidarRota(queryClient, '/api/gestor/')
    } finally {
      setEnviando(false)
    }
  }
  const regras = regrasDe(erro)
  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo="Sugerir outra data"
      descricao={`${pedido.funcionario.nome} pediu ${periodoCurto(pedido.dataInicio, pedido.dataFim)}. Os dias sugeridos não ficam reservados enquanto a pessoa não aceita.`}
      rodape={
        <>
          <Botao variante="secundario" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao onClick={() => void enviar()} carregando={enviando}>
            Sugerir
          </Botao>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <div className="grid grid-cols-2 gap-4">
          <Campo rotulo="Primeiro dia">
            <Entrada type="date" lang="pt-BR" value={inicio} onChange={(e) => setInicio(e.target.value)} />
          </Campo>
          <Campo rotulo="Último dia">
            <Entrada type="date" lang="pt-BR" value={fim} onChange={(e) => setFim(e.target.value)} />
          </Campo>
        </div>
        {!!erro && <Aviso tom="erro">{mensagemDeErro(erro)}</Aviso>}
        {regras && <ListaDeRegras regras={regras} />}
      </div>
    </Janela>
  )
}

/** O cartão de um pedido de férias para decidir (tela 51, gestor; tela 7.2, RH). */
export function CartaoDeFeriasParaDecidir({
  pedido,
  quem,
  destacado = false,
  comCargo = false,
}: {
  pedido: Pedido
  quem: QuemDecide
  destacado?: boolean
  comCargo?: boolean
}) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const [modal, setModal] = useState<'recusar' | 'sugerir' | null>(null)
  const [aprovando, setAprovando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const p = pedido
  const prefixo = quem === 'gestor' ? '/api/gestor/' : '/api/rh/'

  async function atualizar() {
    await Promise.all([invalidarRota(queryClient, prefixo), invalidarContadoresDoMenu(queryClient)])
  }

  /** Devolve o erro (para o modal mostrar) ou undefined. */
  async function decidir(acao: 'aprovar' | 'recusar', motivo?: string): Promise<unknown> {
    setErro(null)
    const params = { params: { path: { feriasId: p.id } } }
    try {
      if (quem === 'gestor') {
        if (acao === 'aprovar') await clienteHttp.POST('/api/gestor/ferias/{feriasId}/aprovar', params)
        else await clienteHttp.POST('/api/gestor/ferias/{feriasId}/recusar', { ...params, body: { motivo: motivo! } })
      } else if (acao === 'aprovar') await clienteHttp.POST('/api/rh/ferias/{feriasId}/confirmar', params)
      else await clienteHttp.POST('/api/rh/ferias/{feriasId}/recusar', { ...params, body: { motivo: motivo! } })
      notificacao.sucesso(
        acao === 'recusar'
          ? 'Pedido recusado.'
          : quem === 'gestor'
            ? 'Pedido aprovado. Agora o RH confirma.'
            : 'Férias confirmadas.',
      )
      setModal(null)
      await atualizar()
      return undefined
    } catch (e) {
      if (e instanceof ErroDeApi && (e.status === 409 || e.status === 404)) {
        notificacao.erro(mensagemDeErro(e))
        setModal(null)
        await atualizar()
        return undefined
      }
      if (acao === 'aprovar') setErro(textoDoErro(e))
      return e
    }
  }

  const desligado = !p.podeDecidir
  return (
    <li
      aria-label={`Férias de ${p.funcionario.nome}`}
      className={cn(
        'flex flex-col gap-3 rounded-cartao border bg-superficie p-5',
        destacado ? 'border-2 border-primaria' : 'border-borda',
      )}
    >
      <div className="flex flex-wrap items-center justify-between gap-2">
        <Selo tom="info">Férias</Selo>
        {quem === 'rh' && <Selo tom={SITUACAO_DAS_FERIAS[p.situacao].tom}>{SITUACAO_DAS_FERIAS[p.situacao].texto}</Selo>}
      </div>
      <p className="text-corpo text-texto">
        {p.funcionario.nome}
        {comCargo && (p.funcionario.cargo || p.funcionario.setor)
          ? ` (${[p.funcionario.cargo, p.funcionario.setor].filter(Boolean).join(', ')})`
          : ''}{' '}
        pediu {textoDeDias(p.dias)}, de {periodoCurto(p.dataInicio, p.dataFim)}.
        {extrasDoPedido(p).map((x) => ` ${x}`)}
      </p>
      {p.observacao && <p className="whitespace-pre-line text-tabela text-texto-suave">“{p.observacao}”</p>}
      {p.conflitos.map((c) => (
        <Aviso key={`${c.funcionario.id}-${c.dataInicio}`} tom="alerta">
          Coincide {textoDeDias(c.diasEmComum)} com as férias já aprovadas de {c.funcionario.nome} (
          {periodoCurto(c.dataInicio, c.dataFim)}).
          {p.equipeDisponivel !== null ? ` A equipe fica com ${p.equipeDisponivel} pessoas nesse período.` : ''}
        </Aviso>
      ))}
      {quem === 'rh' &&
        frasesDaDecisao(p).map((f) => (
          <p key={f} className="text-pequeno text-texto-suave">
            {f}
          </p>
        ))}
      {erro && <Aviso tom="erro">{erro}</Aviso>}
      <div className="flex flex-wrap items-center gap-2">
        <Botao tamanho="pequeno" disabled={desligado} carregando={aprovando} onClick={() => {
          setAprovando(true)
          void decidir('aprovar').finally(() => setAprovando(false))
        }}>
          {quem === 'gestor' ? 'Aprovar' : 'Confirmar'}
        </Botao>
        {quem === 'gestor' && (
          <Botao variante="secundario" tamanho="pequeno" disabled={desligado} onClick={() => setModal('sugerir')}>
            Sugerir outra data
          </Botao>
        )}
        <Botao variante="perigo" tamanho="pequeno" disabled={desligado} onClick={() => setModal('recusar')}>
          Recusar
        </Botao>
        {desligado && p.motivoNaoPodeDecidir && (
          <span className="text-pequeno text-texto-suave">{p.motivoNaoPodeDecidir}</span>
        )}
      </div>
      {modal === 'recusar' && (
        <ModalRecusar nome={p.funcionario.nome} aoFechar={() => setModal(null)} aoConfirmar={(motivo) => decidir('recusar', motivo)} />
      )}
      {modal === 'sugerir' && <ModalSugerir pedido={p} aoFechar={() => setModal(null)} />}
    </li>
  )
}
