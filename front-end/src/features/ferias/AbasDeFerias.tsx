import { useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { formatarData } from '../../shared/formato/datas'
import { primeiroNome } from '../../shared/formato/texto'
import type { PropsDaAba } from '../colaboradores/abasDaFicha'
import type { PropsDaAbaDaEquipe } from '../equipe/abasDaFichaDaEquipe'
import { Aviso } from '../../shared/ui/Aviso'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Janela } from '../../shared/ui/Janela'
import { Selo } from '../../shared/ui/Selo'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { NumerosDoPonto } from '../ponto/PecasDoPonto'
import { CamposDasFerias } from './CamposDasFerias'
import { frasesDaDecisao, periodoCompleto, SITUACAO_DAS_FERIAS, textoDeDias } from './rotulosDasFerias'
import { comandoDaConferencia, FERIAS_VAZIAS, useConferenciaDeFerias, type DadosDasFerias } from './useConferenciaDeFerias'

type Ficha = Esquema<'FeriasFicha'>
type Pedido = Esquema<'FeriasPedido'>
type Regra = Esquema<'FeriasRegraConferida'>

function ConteudoDaAba({
  dados,
  carregando,
  erro,
  aoTentarDeNovo,
  acoes,
  rodape,
  comFrases,
}: {
  dados: Ficha | undefined
  carregando: boolean
  erro: unknown
  aoTentarDeNovo: () => void
  acoes?: ReactNode
  rodape: string
  comFrases: boolean
}) {
  const r = dados?.resumo
  const colunas: readonly ColunaDaTabela<Pedido>[] = [
    {
      chave: 'periodo',
      titulo: 'Período',
      celula: (p) => (
        <span className="flex flex-col gap-1">
          <span>{periodoCompleto(p.dataInicio, p.dataFim)}</span>
          {comFrases &&
            [...frasesDaDecisao(p), ...(p.situacao === 'SUGESTAO_DE_DATA' && p.motivoNaoPodeDecidir ? [p.motivoNaoPodeDecidir] : [])].map((f) => (
              <span key={f} className="text-pequeno text-texto-suave">
                {f}
              </span>
            ))}
        </span>
      ),
    },
    { chave: 'dias', titulo: 'Dias', celula: (p) => textoDeDias(p.dias) },
    {
      chave: 'situacao',
      titulo: 'Situação',
      celula: (p) => <Selo tom={SITUACAO_DAS_FERIAS[p.situacao].tom}>{SITUACAO_DAS_FERIAS[p.situacao].texto}</Selo>,
    },
  ]
  if (erro) return <AvisoDeErro erro={erro} aoTentarDeNovo={aoTentarDeNovo} />
  return (
    <div className="flex flex-col gap-5">
      <NumerosDoPonto
        rotulo="Resumo das férias"
        carregando={!r}
        itens={[
          { rotulo: 'Dias disponíveis', valor: r ? textoDeDias(r.diasDisponiveis) : '—' },
          {
            rotulo: 'Período aquisitivo atual',
            valor: r?.periodoAquisitivoAtual ? periodoCompleto(r.periodoAquisitivoAtual.inicio, r.periodoAquisitivoAtual.fim) : '—',
          },
          { rotulo: 'Prazo para tirar o saldo', valor: r?.prazoParaTirarSaldo ? formatarData(r.prazoParaTirarSaldo) : '—' },
        ]}
      />
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-t3 font-bold text-texto">Períodos de férias</h2>
        {acoes}
      </div>
      <Tabela
        rotulo="Períodos de férias"
        colunas={colunas}
        linhas={dados?.ferias}
        chaveDaLinha={(p) => p.id}
        carregando={carregando}
        vazio={<EstadoVazio titulo="Nenhum período de férias registrado ainda." />}
      />
      <p className="text-pequeno text-texto-suave">{rodape}</p>
    </div>
  )
}

/** Tela 06: "Registrar férias de {nome}". Nasce aprovada; nenhuma regra é reimplementada. */
function ModalRegistrarFerias({ ficha, dados, aoFechar }: { ficha: { id: number; nome: string }; dados: Ficha; aoFechar: () => void }) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const registrar = $api.useMutation('post', '/api/rh/funcionarios/{funcionarioId}/ferias')
  const [valores, setValores] = useState<DadosDasFerias>(FERIAS_VAZIAS)
  const [erro, setErro] = useState<unknown>(null)
  const { conferencia, conferindo, erro: erroDaConferencia } = useConferenciaDeFerias({ tipo: 'rh', funcionarioId: ficha.id }, valores)
  const r = dados.resumo
  const regras =
    erro instanceof ErroDeApi && Array.isArray(erro.corpo?.regras) ? (erro.corpo.regras as Regra[]) : undefined

  async function salvar() {
    if (registrar.isPending || !conferencia?.podeEnviar) return
    setErro(null)
    try {
      await registrar.mutateAsync({
        params: { path: { funcionarioId: ficha.id } },
        body: {
          ...comandoDaConferencia(valores),
          adiantar13Salario: valores.adiantar13Salario,
          observacao: valores.texto.trim() || null,
        },
      })
      notificacao.sucesso('Férias registradas.')
      await invalidarRota(queryClient, '/api/rh/funcionarios/{funcionarioId}')
      aoFechar()
    } catch (e) {
      setErro(e)
    }
  }

  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo={`Registrar férias de ${primeiroNome(ficha.nome)}`}
      tamanho="media"
      rodape={
        <div className="flex w-full items-center justify-between gap-3">
          <span className="text-pequeno text-texto-suave">As férias aparecem na aba Férias e no ponto como dias de férias.</span>
          <div className="flex gap-3">
            <Botao variante="secundario" onClick={aoFechar}>
              Cancelar
            </Botao>
            <Botao onClick={() => void salvar()} disabled={!conferencia?.podeEnviar || conferindo} carregando={registrar.isPending}>
              Registrar férias
            </Botao>
          </div>
        </div>
      }
    >
      <div className="flex flex-col gap-5">
        <NumerosDoPonto
          rotulo="Saldo para registrar"
          itens={[
            { rotulo: 'Saldo disponível', valor: textoDeDias(r.diasDisponiveis) },
            { rotulo: 'Período aquisitivo', valor: r.periodoDoSaldo ? periodoCompleto(r.periodoDoSaldo.inicio, r.periodoDoSaldo.fim) : '—' },
            { rotulo: 'Prazo para tirar', valor: r.prazoParaTirarSaldo ? formatarData(r.prazoParaTirarSaldo) : '—' },
          ]}
        />
        {!!erro && (regras ? <Aviso tom="erro">{mensagemDeErro(erro)}</Aviso> : <AvisoDeErro erro={erro} />)}
        <CamposDasFerias
          valores={valores}
          aoMudar={(v) => {
            setErro(null)
            setValores(v)
          }}
          conferencia={conferencia}
          conferindo={conferindo}
          erroDaConferencia={erroDaConferencia}
          rotuloDoTexto="Observação (opcional)"
          exemploDoTexto="Ex.: combinado com a gestora em 06/10"
          comSobra={false}
          regrasDoErro={regras}
        />
      </div>
    </Janela>
  )
}

/** Tela 05: a aba Férias da ficha do RH (sem os pedidos que ainda estão com o gestor). */
export function AbaFerias({ ficha }: PropsDaAba) {
  const consulta = $api.useQuery('get', '/api/rh/funcionarios/{funcionarioId}/ferias', {
    params: { path: { funcionarioId: ficha.id } },
  })
  const [registrando, setRegistrando] = useState(false)
  return (
    <>
      <ConteudoDaAba
        dados={consulta.data}
        carregando={consulta.isLoading}
        erro={consulta.error}
        aoTentarDeNovo={() => void consulta.refetch()}
        comFrases
        rodape="Pedidos feitos pelo colaborador aparecem aqui depois que o gestor analisa."
        acoes={
          ficha.ativo !== false ? (
            <Botao onClick={() => setRegistrando(true)} disabled={!consulta.data}>
              <Plus aria-hidden="true" className="h-4 w-4" />
              Registrar férias
            </Botao>
          ) : undefined
        }
      />
      {registrando && consulta.data && (
        <ModalRegistrarFerias
          ficha={{ id: ficha.id, nome: ficha.nome ?? 'a pessoa' }}
          dados={consulta.data}
          aoFechar={() => setRegistrando(false)}
        />
      )}
    </>
  )
}

/** Tela 7.1: a aba Férias da ficha da equipe (todos os pedidos; sem registrar e sem decidir). */
export function AbaFeriasDaEquipe({ ficha }: PropsDaAbaDaEquipe) {
  const consulta = $api.useQuery('get', '/api/gestor/equipe/{funcionarioId}/ferias', {
    params: { path: { funcionarioId: ficha.id } },
  })
  return (
    <ConteudoDaAba
      dados={consulta.data}
      carregando={consulta.isLoading}
      erro={consulta.error}
      aoTentarDeNovo={() => void consulta.refetch()}
      comFrases
      rodape="Os pedidos esperando sua análise também aparecem no Painel da equipe."
    />
  )
}
