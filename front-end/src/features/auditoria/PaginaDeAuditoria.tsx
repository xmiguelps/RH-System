import { keepPreviousData } from '@tanstack/react-query'
import { Download } from 'lucide-react'
import { useState } from 'react'
import { $api, clienteHttp } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { baixarExportacao } from '../../shared/arquivos/baixar'
import { formatarInstanteRelativo, hojeEmSaoPaulo } from '../../shared/formato/datas'
import { formatarNumero } from '../../shared/formato/numeros'
import { FiltroDeSelecao } from '../../shared/lista/FiltroDeSelecao'
import { useEstadoDaLista } from '../../shared/lista/useEstadoDaLista'
import { valorDeEnum } from '../../shared/lista/valoresDeLista'
import { Aviso } from '../../shared/ui/Aviso'
import { Botao } from '../../shared/ui/Botao'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { Campo } from '../../shared/ui/Campo'
import { CampoDeBusca } from '../../shared/ui/CampoDeBusca'
import { Entrada } from '../../shared/ui/Entrada'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Paginacao } from '../../shared/ui/Paginacao'
import { Selecao } from '../../shared/ui/Selecao'
import { Selo } from '../../shared/ui/Selo'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { DetalheDoEvento } from './DetalheDoEvento'
import {
  intervaloDoPeriodo,
  PERIODOS,
  textoDoPeriodo,
  type PeriodoDaAuditoria,
} from './periodoDaAuditoria'
import {
  OPCOES_DE_ACAO,
  OPCOES_DE_AREA,
  rotuloDaAcao,
  rotuloDaArea,
  rotuloDoQuem,
  tomDaAcao,
} from './rotulosDeAuditoria'

type Evento = Esquema<'AuditoriaEvento'>
type Area = Esquema<'AuditoriaArea'>
type Acao = Esquema<'AuditoriaAcao'>

const ROTULOS_DO_PERIODO: Record<PeriodoDaAuditoria, string> = {
  hoje: 'Hoje',
  '7': 'Últimos 7 dias',
  '30': 'Últimos 30 dias',
  personalizado: 'Período personalizado',
}

export function PaginaDeAuditoria() {
  const notificacao = useNotificacao()
  const lista = useEstadoDaLista({ filtros: ['periodo', 'de', 'ate', 'area', 'acao'] as const })
  const periodo = valorDeEnum(lista.filtros.periodo, PERIODOS) ?? '7'
  const area = valorDeEnum<Area>(
    lista.filtros.area,
    OPCOES_DE_AREA.map((o) => o.valor),
  )
  const acao = valorDeEnum<Acao>(
    lista.filtros.acao,
    OPCOES_DE_ACAO.map((o) => o.valor),
  )
  const intervalo = intervaloDoPeriodo(
    periodo,
    lista.filtros.de,
    lista.filtros.ate,
    hojeEmSaoPaulo(),
  )
  const [aberto, setAberto] = useState<number | null>(null)
  const [baixando, setBaixando] = useState(false)

  const filtrosDaApi = { busca: lista.consulta.busca, area, acao, ...(intervalo ?? {}) }
  const consulta = $api.useQuery(
    'get',
    '/api/rh/auditoria',
    {
      params: {
        query: {
          pagina: lista.consulta.pagina,
          tamanhoPagina: lista.consulta.tamanhoPagina,
          ...filtrosDaApi,
        },
      },
    },
    { placeholderData: keepPreviousData, enabled: intervalo !== null },
  )

  const exportar = async () => {
    setBaixando(true)
    try {
      await baixarExportacao(
        clienteHttp.GET('/api/rh/auditoria/exportacao', {
          params: { query: filtrosDaApi },
          parseAs: 'blob',
        }),
        `auditoria-${hojeEmSaoPaulo()}.csv`,
      )
    } catch (erro) {
      notificacao.erro(mensagemDeErro(erro))
    } finally {
      setBaixando(false)
    }
  }

  const errosDoPeriodo = consulta.error instanceof ErroDeApi ? consulta.error.errors : {}
  const erroDaLista =
    consulta.error && !errosDoPeriodo.de && !errosDoPeriodo.ate ? consulta.error : undefined

  const colunas: ColunaDaTabela<Evento>[] = [
    { chave: 'quando', titulo: 'Quando', celula: (e) => formatarInstanteRelativo(e.quandoUtc) },
    {
      chave: 'quem',
      titulo: 'Quem',
      celula: (e) => (
        <div className="flex flex-col">
          <span className="font-semibold text-texto">{e.ator.nome}</span>
          <span className="text-pequeno text-texto-suave">{rotuloDoQuem(e.ator)}</span>
        </div>
      ),
    },
    { chave: 'area', titulo: 'Área', celula: (e) => rotuloDaArea(e.area) },
    {
      chave: 'acao',
      titulo: 'Ação',
      celula: (e) => <Selo tom={tomDaAcao(e.acao)}>{rotuloDaAcao(e.acao)}</Selo>,
    },
    { chave: 'descricao', titulo: 'O que aconteceu', celula: (e) => e.descricao },
    {
      chave: 'detalhes',
      titulo: 'Detalhes',
      alinhamento: 'direita',
      celula: (e) => (
        <Botao
          variante="secundario"
          tamanho="pequeno"
          aria-expanded={aberto === e.id}
          onClick={() => setAberto((atual) => (atual === e.id ? null : e.id))}
        >
          {aberto === e.id ? 'Fechar' : 'Detalhes'}
        </Botao>
      ),
    },
  ]

  const dados = consulta.data
  return (
    <>
      <CabecalhoDePagina
        titulo="Log de auditoria"
        subtitulo="Tudo o que foi criado, alterado ou excluído no portal: quem fez, quando e o que mudou. Nenhuma tela do sistema edita ou apaga estes registros."
        acoes={
          <Botao
            variante="secundario"
            icone={Download}
            carregando={baixando}
            onClick={() => void exportar()}
          >
            Exportar CSV
          </Botao>
        }
      />
      <div className="mb-4 flex flex-wrap items-end gap-3">
        <div className="w-[220px]">
          <Selecao
            rotulo="Período"
            opcoes={PERIODOS.map((p) => ({ valor: p, rotulo: ROTULOS_DO_PERIODO[p] }))}
            valor={periodo}
            aoMudar={(novo) => lista.definirFiltro('periodo', !novo || novo === '7' ? null : novo)}
          />
        </div>
        {periodo === 'personalizado' && (
          <>
            <Campo rotulo="De" erro={errosDoPeriodo.de?.join(' ')}>
              <Entrada
                type="date"
                lang="pt-BR"
                value={lista.filtros.de ?? ''}
                onChange={(e) => lista.definirFiltro('de', e.target.value || null)}
              />
            </Campo>
            <Campo rotulo="Até" erro={errosDoPeriodo.ate?.join(' ')}>
              <Entrada
                type="date"
                lang="pt-BR"
                value={lista.filtros.ate ?? ''}
                onChange={(e) => lista.definirFiltro('ate', e.target.value || null)}
              />
            </Campo>
          </>
        )}
        <div className="min-w-[260px] flex-1">
          <CampoDeBusca
            valor={lista.busca}
            aoMudar={lista.definirBusca}
            placeholder="Buscar pessoa ou registro"
            rotulo="Buscar pessoa ou registro"
          />
        </div>
        <div className="w-[200px]">
          <FiltroDeSelecao
            rotulo="Área"
            todos="Todas as áreas"
            opcoes={OPCOES_DE_AREA}
            valor={area}
            aoMudar={(v) => lista.definirFiltro('area', v)}
          />
        </div>
        <div className="w-[200px]">
          <FiltroDeSelecao
            rotulo="Ação"
            todos="Todas as ações"
            opcoes={OPCOES_DE_ACAO}
            valor={acao}
            aoMudar={(v) => lista.definirFiltro('acao', v)}
          />
        </div>
      </div>
      {intervalo === null ? (
        <Aviso tom="info">Escolha o período.</Aviso>
      ) : (
        <Tabela
          rotulo="Log de auditoria"
          colunas={colunas}
          linhas={erroDaLista ? undefined : dados?.itens}
          chaveDaLinha={(e) => e.id}
          carregando={consulta.isFetching}
          erro={erroDaLista}
          aoTentarDeNovo={() => void consulta.refetch()}
          vazio={<EstadoVazio titulo="Nenhum registro neste período." />}
          detalhe={(e) =>
            e.id === aberto ? (
              <DetalheDoEvento eventoId={e.id} aoSumir={() => setAberto(null)} />
            ) : null
          }
          rodape={
            dados && dados.itens.length > 0 ? (
              <Paginacao
                dados={dados}
                aoMudar={lista.definirPagina}
                resumo={(_de, _ate, total) =>
                  `Mostrando ${formatarNumero(dados.itens.length)} de ${formatarNumero(total)} registros ${textoDoPeriodo(periodo, lista.filtros.de, lista.filtros.ate)}`
                }
              />
            ) : undefined
          }
        />
      )}
    </>
  )
}
