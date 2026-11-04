import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { $api, clienteHttp } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import type { Esquema } from '../../shared/api/tipos'
import { useBaixarPorLink } from '../../shared/arquivos/useBaixarPorLink'
import { formatarDataHora } from '../../shared/formato/datas'
import { FiltroDeSelecao } from '../../shared/lista/FiltroDeSelecao'
import { AreaDeTexto } from '../../shared/ui/AreaDeTexto'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { Campo } from '../../shared/ui/Campo'
import { CampoDeBusca } from '../../shared/ui/CampoDeBusca'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Janela } from '../../shared/ui/Janela'
import { JanelaDeConfirmacao } from '../../shared/ui/JanelaDeConfirmacao'
import { Paginacao } from '../../shared/ui/Paginacao'
import { Selo } from '../../shared/ui/Selo'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { periodoDoAtestado, situacaoDoAtestado } from './rotulosDoPonto'

type Atestado = Esquema<'Atestado'>
type Situacao = Esquema<'AtestadoSituacao'>
type FiltroDeSituacao = Situacao | 'TODOS'

const OPCOES_DE_SITUACAO = [
  { valor: 'ENVIADO', rotulo: 'Aguardando validação' },
  { valor: 'VALIDADO', rotulo: 'Validados' },
  { valor: 'RECUSADO', rotulo: 'Recusados' },
] as const

function lerFiltro(valor: string | null): FiltroDeSituacao {
  if (valor === 'TODOS' || valor === 'VALIDADO' || valor === 'RECUSADO') return valor
  return 'ENVIADO'
}

function DialogoDeRecusa({ atestado, aoFechar }: { atestado: Atestado; aoFechar: () => void }) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const recusar = $api.useMutation('post', '/api/rh/ponto/atestados/{atestadoId}/recusar')
  const [motivo, setMotivo] = useState('')
  const [erroDoMotivo, setErroDoMotivo] = useState<string | null>(null)
  const [erro, setErro] = useState<unknown>(null)
  async function confirmar() {
    if (recusar.isPending) return
    const texto = motivo.trim()
    if (!texto) return setErroDoMotivo('Escreva o motivo da recusa.')
    setErroDoMotivo(null)
    setErro(null)
    try {
      await recusar.mutateAsync({
        params: { path: { atestadoId: atestado.id } },
        body: { motivo: texto },
      })
      notificacao.sucesso('Atestado recusado.')
      await invalidarRota(queryClient, '/api/rh/ponto/atestados')
      aoFechar()
    } catch (e) {
      setErro(e)
      if (e instanceof ErroDeApi && e.status === 409) {
        await invalidarRota(queryClient, '/api/rh/ponto/atestados')
      }
    }
  }
  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo={`Recusar o atestado de ${atestado.funcionario.nome}?`}
      rodape={
        <>
          <Botao variante="secundario" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao variante="perigo" onClick={() => void confirmar()} carregando={recusar.isPending}>
            Recusar atestado
          </Botao>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <p className="text-corpo text-texto-suave">
          A pessoa vê o motivo e o dia segue como falta.
        </p>
        <Campo rotulo="Motivo" erro={erroDoMotivo}>
          <AreaDeTexto
            limite={500}
            rows={3}
            value={motivo}
            onChange={(e) => setMotivo(e.target.value.slice(0, 500))}
          />
        </Campo>
        {!!erro && <AvisoDeErro erro={erro} />}
      </div>
    </Janela>
  )
}

/** Atestados do RH (sem print): painel sobre a tela 12, em /rh/ponto-e-feriados/atestados. */
export function PainelDeAtestadosRh() {
  const navegar = useNavigate()
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const [busca, setBusca] = useSearchParams()
  const situacao = lerFiltro(busca.get('situacao'))
  const texto = busca.get('busca') ?? ''
  const [pagina, setPagina] = useState(1)
  const [validando, setValidando] = useState<Atestado | null>(null)
  const [recusando, setRecusando] = useState<Atestado | null>(null)
  const [erroDaValidacao, setErroDaValidacao] = useState<unknown>(null)
  const { baixar } = useBaixarPorLink()
  const validar = $api.useMutation('post', '/api/rh/ponto/atestados/{atestadoId}/validar')
  const consulta = $api.useQuery('get', '/api/rh/ponto/atestados', {
    params: {
      query: {
        ...(situacao === 'TODOS' ? {} : { situacao }),
        ...(texto ? { busca: texto } : {}),
        ...(pagina > 1 ? { pagina } : {}),
      },
    },
  })
  const dados = consulta.data

  function mudar(chave: string, valor: string | null) {
    setPagina(1)
    setBusca((p) => {
      const novo = new URLSearchParams(p)
      if (valor) novo.set(chave, valor)
      else novo.delete(chave)
      return novo
    })
  }

  const colunas: readonly ColunaDaTabela<Atestado>[] = [
    { chave: 'pessoa', titulo: 'Colaborador', celula: (a) => a.funcionario.nome },
    { chave: 'periodo', titulo: 'Período', celula: (a) => periodoDoAtestado(a) },
    { chave: 'arquivo', titulo: 'Arquivo', celula: (a) => a.nomeArquivo ?? '—' },
    { chave: 'enviado', titulo: 'Enviado em', celula: (a) => formatarDataHora(a.enviadoEmUtc) },
    {
      chave: 'situacao',
      titulo: 'Situação',
      celula: (a) => {
        const { texto: rotulo, tom } = situacaoDoAtestado(a.situacao, true)
        return (
          <span className="flex flex-col items-start gap-1">
            <Selo tom={tom}>{rotulo}</Selo>
            {a.decididoPorNome && (
              <span className="text-pequeno text-texto-suave">
                {a.decididoPorNome}, {formatarDataHora(a.decididoEmUtc)}
              </span>
            )}
            {a.motivoRecusa && (
              <span className="text-pequeno text-texto-suave">{a.motivoRecusa}</span>
            )}
          </span>
        )
      },
    },
    {
      chave: 'acoes',
      titulo: 'Ações',
      celula: (a) => (
        <span className="flex flex-wrap items-center gap-2">
          <Botao
            variante="texto"
            tamanho="pequeno"
            aria-label={`Ver arquivo do atestado de ${a.funcionario.nome}`}
            onClick={() =>
              void baixar(() =>
                clienteHttp.GET('/api/rh/ponto/atestados/{atestadoId}/download', {
                  params: { path: { atestadoId: a.id } },
                }),
              )
            }
          >
            Ver arquivo
          </Botao>
          {a.situacao === 'ENVIADO' && (
            <>
              <Botao
                variante="secundario"
                tamanho="pequeno"
                aria-label={`Validar o atestado de ${a.funcionario.nome}`}
                onClick={() => {
                  setErroDaValidacao(null)
                  setValidando(a)
                }}
              >
                Validar
              </Botao>
              <Botao
                variante="perigo"
                tamanho="pequeno"
                aria-label={`Recusar o atestado de ${a.funcionario.nome}`}
                onClick={() => setRecusando(a)}
              >
                Recusar
              </Botao>
            </>
          )}
        </span>
      ),
    },
  ]

  return (
    <Janela
      aberta
      aoFechar={() => navegar({ pathname: '/rh/ponto-e-feriados' })}
      titulo="Atestados"
      posicao="direita"
      largura="larga"
    >
      <div className="flex flex-col gap-4">
        <div className="grid grid-cols-2 items-end gap-4">
          <FiltroDeSelecao
            rotulo="Situação"
            todos="Todos"
            opcoes={OPCOES_DE_SITUACAO}
            valor={situacao === 'TODOS' ? undefined : situacao}
            aoMudar={(v) => mudar('situacao', v ?? 'TODOS')}
          />
          <CampoDeBusca
            rotulo="Buscar por nome"
            valor={texto}
            aoMudar={(v) => mudar('busca', v || null)}
          />
        </div>
        <p className="text-pequeno text-texto-suave">O acesso a este arquivo fica registrado.</p>
        <Tabela
          rotulo="Atestados"
          colunas={colunas}
          linhas={dados?.itens}
          chaveDaLinha={(a) => a.id}
          carregando={consulta.isFetching && !dados}
          erro={consulta.error ?? undefined}
          aoTentarDeNovo={() => void consulta.refetch()}
          vazio={
            <EstadoVazio
              titulo={
                situacao === 'ENVIADO'
                  ? 'Nenhum atestado aguardando validação.'
                  : 'Nenhum atestado.'
              }
            />
          }
          rodape={
            dados && dados.totalPaginas > 1 ? (
              <Paginacao dados={dados} aoMudar={setPagina} />
            ) : undefined
          }
        />
      </div>
      <JanelaDeConfirmacao
        aberta={validando !== null}
        titulo="Validar este atestado?"
        descricao="O dia deixa de contar como falta e não desconta do banco de horas."
        rotuloDeConfirmar="Validar"
        confirmando={validar.isPending}
        erro={erroDaValidacao ?? undefined}
        aoCancelar={() => setValidando(null)}
        aoConfirmar={async () => {
          if (!validando) return
          try {
            await validar.mutateAsync({ params: { path: { atestadoId: validando.id } } })
            notificacao.sucesso('Atestado validado.')
            setValidando(null)
            await invalidarRota(queryClient, '/api/rh/ponto/atestados')
          } catch (e) {
            setErroDaValidacao(e)
            if (e instanceof ErroDeApi && e.status === 409) {
              await invalidarRota(queryClient, '/api/rh/ponto/atestados')
            }
          }
        }}
      />
      {recusando && <DialogoDeRecusa atestado={recusando} aoFechar={() => setRecusando(null)} />}
    </Janela>
  )
}
