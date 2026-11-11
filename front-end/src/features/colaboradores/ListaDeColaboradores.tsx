import { keepPreviousData } from '@tanstack/react-query'
import { PanelLeft, Plus } from 'lucide-react'
import { useState } from 'react'
import { Outlet, useLocation } from 'react-router'
import { Aviso } from '../../shared/ui/Aviso'
import type { AvisoDeCadastro } from './PainelCadastrar'
import { $api } from '../../shared/api/api'
import type { Esquema } from '../../shared/api/tipos'
import { formatarData } from '../../shared/formato/datas'
import { formatarNumero } from '../../shared/formato/numeros'
import { FiltroDeSelecao } from '../../shared/lista/FiltroDeSelecao'
import { useEstadoDaLista } from '../../shared/lista/useEstadoDaLista'
import { usePainelSobreLista } from '../../shared/lista/usePainelSobreLista'
import { inteiroDaUrl, valorDeEnum } from '../../shared/lista/valoresDeLista'
import { Abas } from '../../shared/ui/Abas'
import { Avatar } from '../../shared/ui/Avatar'
import { Botao } from '../../shared/ui/Botao'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { CampoDeBusca } from '../../shared/ui/CampoDeBusca'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Paginacao } from '../../shared/ui/Paginacao'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { ModalLoteDeHolerites } from '../holerites/ModalLoteDeHolerites'
import { EtiquetaDaSituacao } from './EtiquetaDaSituacao'
import { OPCOES_DE_TIPO_DE_CONTRATO, type TipoDeContrato } from './rotulos'

const SITUACOES = ['ATIVO', 'EM_ADMISSAO', 'INATIVO'] as const
const TIPOS = OPCOES_DE_TIPO_DE_CONTRATO.map((o) => o.valor)
type Situacao = (typeof SITUACOES)[number]
type Linha = Esquema<'FuncionarioListaRespostaRh'>['itens'][number]

function numero(valor: number | undefined): string {
  return valor === undefined ? '—' : formatarNumero(valor)
}

export function ListaDeColaboradores() {
  const lista = useEstadoDaLista({ filtros: ['situacao', 'setorId', 'tipoContrato'] as const })
  const { paraPainel } = usePainelSobreLista()
  const estado = useLocation().state as { cadastrado?: AvisoDeCadastro } | null
  const cadastrado = estado?.cadastrado
  const [loteAberto, setLoteAberto] = useState(false)
  const situacao: Situacao = valorDeEnum(lista.filtros.situacao, SITUACOES) ?? 'ATIVO'
  const setorId = inteiroDaUrl(lista.filtros.setorId)
  const tipoContrato = valorDeEnum<TipoDeContrato>(lista.filtros.tipoContrato, TIPOS)

  const consulta = $api.useQuery(
    'get',
    '/api/rh/funcionarios',
    {
      params: {
        query: {
          pagina: lista.consulta.pagina,
          tamanhoPagina: lista.consulta.tamanhoPagina,
          busca: lista.consulta.busca,
          situacao,
          setorId,
          tipoContrato,
        },
      },
    },
    { placeholderData: keepPreviousData },
  )
  const setores = $api.useQuery('get', '/api/rh/setores', {
    params: { query: { pagina: 1, tamanhoPagina: 100 } },
  })

  const dados = consulta.data
  const totais = dados?.totais
  const comFiltro = !!(lista.busca || setorId || tipoContrato)

  const colunas: ColunaDaTabela<Linha>[] = [
    {
      chave: 'nome',
      titulo: 'Nome',
      celula: (l) => (
        <div className="flex items-center gap-3">
          <Avatar nome={l.nome} />
          <div className="flex flex-col">
            <span className="font-semibold text-texto">{l.nome}</span>
            {l.emailCorporativo && (
              <span className="text-pequeno text-texto-suave">{l.emailCorporativo}</span>
            )}
          </div>
        </div>
      ),
    },
    { chave: 'cargo', titulo: 'Cargo', celula: (l) => l.cargo ?? '—' },
    { chave: 'setor', titulo: 'Setor', celula: (l) => l.setor ?? '—' },
    { chave: 'admissao', titulo: 'Admissão', celula: (l) => formatarData(l.dataAdmissao) },
    {
      chave: 'situacao',
      titulo: 'Situação hoje',
      celula: (l) => <EtiquetaDaSituacao situacao={l.situacao} situacaoHoje={l.situacaoHoje} />,
    },
    {
      chave: 'acoes',
      titulo: 'Ações',
      alinhamento: 'direita',
      celula: (l) => (
        <BotaoLink to={paraPainel(String(l.id))} variante="secundario" tamanho="pequeno">
          <PanelLeft size={16} strokeWidth={1.75} aria-hidden="true" />
          Abrir ficha
        </BotaoLink>
      ),
    },
  ]

  return (
    <>
      <CabecalhoDePagina
        titulo="Colaboradores"
        subtitulo={`${numero(totais?.ativos)} ativos, ${numero(totais?.emAdmissao)} em admissão e ${numero(totais?.inativos)} inativos`}
        acoes={
          <>
            <Botao variante="secundario" onClick={() => setLoteAberto(true)}>
              Enviar holerites em lote
            </Botao>
            <BotaoLink to={paraPainel('novo')}>
              <Plus size={16} strokeWidth={1.75} aria-hidden="true" />
              Adicionar colaborador
            </BotaoLink>
          </>
        }
      />
      {cadastrado && (
        <div className="mb-4">
          <Aviso
            tom="sucesso"
            acao={
              <BotaoLink
                to={paraPainel(String(cadastrado.funcionarioId))}
                variante="secundario"
                tamanho="pequeno"
              >
                Abrir ficha
              </BotaoLink>
            }
          >
            {cadastrado.emailDoConvite
              ? `${cadastrado.nome} foi cadastrado. O convite foi enviado para ${cadastrado.emailDoConvite}.`
              : `${cadastrado.nome} foi cadastrado.`}
          </Aviso>
        </div>
      )}
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <Abas
          rotulo="Situação"
          itens={[
            { valor: 'ATIVO', rotulo: 'Ativos', contagem: totais?.ativos },
            { valor: 'EM_ADMISSAO', rotulo: 'Em admissão', contagem: totais?.emAdmissao },
            { valor: 'INATIVO', rotulo: 'Inativos', contagem: totais?.inativos },
          ]}
          valor={situacao}
          aoMudar={(nova) => lista.definirFiltro('situacao', nova === 'ATIVO' ? null : nova)}
        />
      </div>
      <div className="mb-4 grid grid-cols-[1fr_220px_220px] gap-3">
        <CampoDeBusca
          valor={lista.busca}
          aoMudar={(texto) => lista.definirBusca(texto.slice(0, 100))}
          placeholder="Buscar por nome, CPF ou matrícula"
          rotulo="Buscar colaborador"
        />
        <FiltroDeSelecao
          rotulo="Setor"
          todos="Todos os setores"
          opcoes={(setores.data?.itens ?? []).map((s) => ({ valor: String(s.id), rotulo: s.nome }))}
          valor={setorId ? String(setorId) : undefined}
          aoMudar={(valor) => lista.definirFiltro('setorId', valor)}
          carregando={setores.isLoading}
        />
        <FiltroDeSelecao
          rotulo="Contrato"
          todos="Todos os contratos"
          opcoes={OPCOES_DE_TIPO_DE_CONTRATO}
          valor={tipoContrato}
          aoMudar={(valor) => lista.definirFiltro('tipoContrato', valor)}
        />
      </div>
      <Tabela
        rotulo="Colaboradores"
        colunas={colunas}
        linhas={consulta.isError ? undefined : dados?.itens}
        chaveDaLinha={(l) => l.id}
        carregando={consulta.isFetching}
        erro={consulta.error ?? undefined}
        aoTentarDeNovo={() => void consulta.refetch()}
        vazio={
          <EstadoVazio
            titulo={comFiltro ? 'Nenhum colaborador encontrado.' : 'Nenhum colaborador nesta aba.'}
          />
        }
        rodape={
          dados && dados.total > 0 ? (
            <Paginacao dados={dados} aoMudar={lista.definirPagina} />
          ) : undefined
        }
      />
      {loteAberto && <ModalLoteDeHolerites aoFechar={() => setLoteAberto(false)} />}
      <Outlet />
    </>
  )
}
