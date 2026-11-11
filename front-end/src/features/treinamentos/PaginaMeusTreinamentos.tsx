import { useQueryClient } from '@tanstack/react-query'
import { GraduationCap } from 'lucide-react'
import { useNavigate, useSearchParams } from 'react-router'
import { $api, clienteHttp } from '../../shared/api/api'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { useBaixarPorLink } from '../../shared/arquivos/useBaixarPorLink'
import { plural } from '../../shared/formato/texto'
import { valorDeEnum } from '../../shared/lista/valoresDeLista'
import { Abas } from '../../shared/ui/Abas'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { CampoDeBusca } from '../../shared/ui/CampoDeBusca'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Paginacao } from '../../shared/ui/Paginacao'
import { Progresso } from '../../shared/ui/Progresso'
import { Selecao } from '../../shared/ui/Selecao'
import { Selo } from '../../shared/ui/Selo'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { diaDoInstante, formatarDuracao, seloDoTreinamento } from './rotulosDosTreinamentos'

type Cartao = Esquema<'TreinamentoMeuCartao'>
type Aba = 'todos' | 'disponiveis' | 'em-andamento' | 'concluidos'

const ABAS: readonly Aba[] = ['todos', 'disponiveis', 'em-andamento', 'concluidos']
const ABA_DA_API = {
  todos: 'TODOS',
  disponiveis: 'DISPONIVEIS',
  'em-andamento': 'EM_ANDAMENTO',
  concluidos: 'CONCLUIDOS',
} as const
const ETIQUETA = {
  DISPONIVEL: { texto: 'Disponível', tom: 'info' },
  NAO_INICIADO: { texto: 'Não iniciado', tom: 'neutro' },
  EM_ANDAMENTO: { texto: 'Em andamento', tom: 'alerta' },
  CONCLUIDO: { texto: 'Concluído', tom: 'sucesso' },
} as const
const VAZIO: Record<Aba, string> = {
  todos: 'Nenhum treinamento por aqui',
  disponiveis: 'Nenhum treinamento por aqui',
  'em-andamento': 'Você não tem treinamento em andamento',
  concluidos: 'Você ainda não concluiu nenhum treinamento',
}

function CartaoDoTreinamento({ item }: { item: Cartao }) {
  const navegar = useNavigate()
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const { baixar } = useBaixarPorLink()
  const inscrever = $api.useMutation('post', '/api/eu/treinamentos/{treinamentoId}/inscricao')
  const iniciar = $api.useMutation('post', '/api/eu/treinamentos/{treinamentoId}/iniciar')
  const params = { params: { path: { treinamentoId: item.id } } }
  const etiqueta = ETIQUETA[item.situacao]
  const duracao = formatarDuracao(item.duracaoTotalMinutos)
  const concluido = item.situacao === 'CONCLUIDO'

  async function agir() {
    try {
      if (item.situacao === 'DISPONIVEL') {
        await inscrever.mutateAsync(params)
        notificacao.sucesso('Inscrição feita.')
        await invalidarRota(queryClient, '/api/eu/treinamentos')
        return
      }
      if (item.situacao === 'NAO_INICIADO') await iniciar.mutateAsync(params)
      navegar(`/eu/treinamentos/${item.id}`)
    } catch (erro) {
      notificacao.erro(mensagemDeErro(erro))
    }
  }

  const rotulo =
    item.situacao === 'DISPONIVEL'
      ? 'Quero me inscrever →'
      : item.situacao === 'NAO_INICIADO'
        ? 'Começar →'
        : 'Continuar →'
  return (
    <li className="flex flex-col overflow-hidden rounded-cartao border border-borda bg-superficie">
      <div className="h-2 bg-primaria-clara" aria-hidden="true" />
      <article aria-label={item.titulo} className="flex flex-1 flex-col gap-3 p-5">
        <div className="flex items-center justify-between gap-2">
          <span className="flex items-center gap-2">
            <GraduationCap aria-hidden="true" className="h-4 w-4 text-texto-suave" />
            <Selo tom={item.obrigatorio ? 'alerta' : 'contorno'}>{seloDoTreinamento(item)}</Selo>
          </span>
          <Selo tom={etiqueta.tom}>{etiqueta.texto}</Selo>
        </div>
        <h2 className="text-t3 font-bold text-texto">{item.titulo}</h2>
        {item.descricao && <p className="text-tabela text-texto-suave">{item.descricao}</p>}
        {concluido && item.certificado && (
          <p className="text-tabela text-texto">
            Código do certificado: {item.certificado.numero}.
          </p>
        )}
        <p className="flex flex-wrap gap-3 text-pequeno text-texto-suave">
          <span>
            {item.totalModulos} {plural(item.totalModulos, 'módulo', 'módulos')}
          </span>
          {duracao && <span>{duracao}</span>}
          {concluido && item.concluidoEmUtc && (
            <span>Concluído em {diaDoInstante(item.concluidoEmUtc)}</span>
          )}
        </p>
        {item.encerrado && !concluido && (
          <p className="text-pequeno font-semibold text-alerta-texto">
            Encerrado pelo RH; você ainda pode terminar
          </p>
        )}
        <Progresso valor={item.progressoPercentual} rotulo={`Andamento de ${item.titulo}`} />
        <div className="mt-auto flex flex-wrap justify-end gap-2">
          {concluido ? (
            <>
              {item.certificado && (
                <Botao
                  variante="secundario"
                  tamanho="pequeno"
                  onClick={() =>
                    void baixar(() =>
                      clienteHttp.GET(
                        '/api/eu/treinamentos/{treinamentoId}/certificado/download',
                        params,
                      ),
                    )
                  }
                >
                  Ver certificado
                </Botao>
              )}
              <BotaoLink tamanho="pequeno" to={`/eu/treinamentos/${item.id}`}>
                Rever conteúdo
              </BotaoLink>
            </>
          ) : (
            <Botao
              tamanho="pequeno"
              onClick={() => void agir()}
              carregando={inscrever.isPending || iniciar.isPending}
            >
              {rotulo}
            </Botao>
          )}
        </div>
      </article>
    </li>
  )
}

/** Telas 41 e 42 (a mesma página para os três perfis). Sem "Em breve" e sem prazo. */
export function PaginaMeusTreinamentos() {
  const [parametros, definirParametros] = useSearchParams()
  const aba = valorDeEnum(parametros.get('aba') ?? undefined, ABAS) ?? 'todos'
  const busca = parametros.get('busca') ?? ''
  const obrigatorio = parametros.get('obrigatorio')
  const pagina = Number(parametros.get('pagina') ?? '1') || 1
  const consulta = $api.useQuery('get', '/api/eu/treinamentos', {
    params: {
      query: {
        aba: ABA_DA_API[aba],
        ...(busca ? { busca } : {}),
        ...(obrigatorio === 'sim'
          ? { obrigatorio: true }
          : obrigatorio === 'nao'
            ? { obrigatorio: false }
            : {}),
        ...(pagina > 1 ? { pagina } : {}),
      },
    },
  })
  const dados = consulta.data
  const c = dados?.contagens

  function mudar(chave: string, valor: string | null, substituir = false) {
    definirParametros(
      (atual) => {
        const novo = new URLSearchParams(atual)
        if (valor) novo.set(chave, valor)
        else novo.delete(chave)
        if (chave !== 'pagina') novo.delete('pagina')
        return novo
      },
      { replace: substituir },
    )
  }

  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo="Meus treinamentos"
        subtitulo="Os obrigatórios aparecem primeiro. Ao concluir, o certificado fica guardado aqui."
      />
      <div className="flex flex-wrap items-end justify-between gap-4">
        <Abas<Aba>
          rotulo="Situação dos treinamentos"
          itens={[
            { valor: 'todos', rotulo: 'Todos', contagem: c?.todos },
            { valor: 'disponiveis', rotulo: 'Disponíveis', contagem: c?.disponiveis },
            { valor: 'em-andamento', rotulo: 'Em andamento', contagem: c?.emAndamento },
            { valor: 'concluidos', rotulo: 'Concluídos', contagem: c?.concluidos },
          ]}
          valor={aba}
          aoMudar={(v) => mudar('aba', v === 'todos' ? null : v)}
        />
        <div className="flex items-end gap-3">
          <div className="w-64">
            <CampoDeBusca
              rotulo="Buscar treinamento"
              placeholder="Buscar treinamento"
              valor={busca}
              aoMudar={(t) => mudar('busca', t.trim() || null, true)}
            />
          </div>
          <div className="w-60">
            <Selecao
              rotulo="Obrigatoriedade"
              valor={obrigatorio === 'sim' || obrigatorio === 'nao' ? obrigatorio : 'todos'}
              opcoes={[
                { valor: 'todos', rotulo: 'Obrigatórios e opcionais' },
                { valor: 'sim', rotulo: 'Só obrigatórios' },
                { valor: 'nao', rotulo: 'Só opcionais' },
              ]}
              aoMudar={(v) => mudar('obrigatorio', v === 'todos' ? null : v)}
            />
          </div>
        </div>
      </div>
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      ) : !dados ? (
        <div
          role="status"
          aria-label="Carregando os treinamentos"
          className="grid grid-cols-3 gap-4"
        >
          <Esqueleto className="h-64" />
          <Esqueleto className="h-64" />
          <Esqueleto className="h-64" />
        </div>
      ) : dados.itens.length === 0 ? (
        <EstadoVazio titulo={VAZIO[aba]} />
      ) : (
        <>
          <ul aria-label="Meus treinamentos" className="grid grid-cols-3 gap-4">
            {dados.itens.map((item) => (
              <CartaoDoTreinamento key={item.id} item={item} />
            ))}
          </ul>
          {dados.totalPaginas > 1 && (
            <Paginacao dados={dados} aoMudar={(p) => mudar('pagina', p > 1 ? String(p) : null)} />
          )}
        </>
      )}
      <p className="text-tabela text-texto-suave">
        {aba === 'concluidos'
          ? 'Mostrando os treinamentos que você já concluiu.'
          : 'Os treinamentos que você já concluiu ficam na aba Concluídos.'}
      </p>
    </div>
  )
}
