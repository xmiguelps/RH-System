import { ClipboardList, Lightbulb, MessageCircle, Users } from 'lucide-react'
import { useSearchParams } from 'react-router'
import { $api } from '../../shared/api/api'
import type { Esquema } from '../../shared/api/tipos'
import { formatarDataDoInstante, formatarDiaMesDaData } from '../../shared/formato/datas'
import { plural } from '../../shared/formato/texto'
import { FiltroDeSelecao } from '../../shared/lista/FiltroDeSelecao'
import { valorDeEnum } from '../../shared/lista/valoresDeLista'
import { Abas } from '../../shared/ui/Abas'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { CampoDeBusca } from '../../shared/ui/CampoDeBusca'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { cn } from '../../shared/ui/estilo'
import { Paginacao } from '../../shared/ui/Paginacao'
import { Progresso } from '../../shared/ui/Progresso'
import { Selo } from '../../shared/ui/Selo'
import { rotuloDoTipo } from './rotulosDosFormularios'

type Item = Esquema<'FormularioMeuItem'>
type Aba = 'todos' | 'para-responder' | 'disponiveis' | 'respondidos'

const ABAS: readonly Aba[] = ['todos', 'para-responder', 'disponiveis', 'respondidos']
const SITUACAO_DA_ABA = {
  todos: undefined,
  'para-responder': 'PARA_RESPONDER',
  disponiveis: 'DISPONIVEL',
  respondidos: 'RESPONDIDO',
} as const
const TIPOS = ['ADMISSAO', 'AVALIACAO', 'PESQUISA', 'OUTRO'] as const
const OPCOES_DE_TIPO = [
  { valor: 'ADMISSAO', rotulo: 'Formulário de admissão' },
  { valor: 'AVALIACAO', rotulo: 'Avaliação' },
  { valor: 'PESQUISA', rotulo: 'Pesquisa' },
  { valor: 'OUTRO', rotulo: 'Sugestão' },
] as const
const ETIQUETA = {
  PARA_RESPONDER: { texto: 'Para responder', tom: 'alerta' },
  DISPONIVEL: { texto: 'Disponível', tom: 'info' },
  RESPONDIDO: { texto: 'Respondido', tom: 'sucesso' },
} as const
const VAZIO: Record<Aba, string> = {
  todos: 'Nenhum formulário para você agora',
  'para-responder': 'Você não tem formulários para responder agora',
  disponiveis: 'Nenhum formulário aberto no momento',
  respondidos: 'Você ainda não respondeu nenhum formulário',
}

function IconeDoTipo({ tipo }: { tipo: Item['tipo'] }) {
  const classes = 'h-4 w-4'
  if (tipo === 'AVALIACAO') return <Users aria-hidden="true" className={classes} />
  if (tipo === 'PESQUISA') return <MessageCircle aria-hidden="true" className={classes} />
  if (tipo === 'OUTRO') return <Lightbulb aria-hidden="true" className={classes} />
  return <ClipboardList aria-hidden="true" className={classes} />
}

function CartaoDoMeuFormulario({ item }: { item: Item }) {
  const etiqueta = ETIQUETA[item.situacao]
  const respondido = item.situacao === 'RESPONDIDO'
  const prazo = item.prazo
    ? item.situacao === 'PARA_RESPONDER'
      ? `Até ${formatarDiaMesDaData(item.prazo)}`
      : `Aberta até ${formatarDiaMesDaData(item.prazo)}`
    : 'Sempre aberto'
  const continuar = item.respondidos > 0 && item.respondidos < item.total
  return (
    <li className="flex flex-col overflow-hidden rounded-cartao border border-borda bg-superficie">
      <div className="h-2 bg-primaria-clara" aria-hidden="true" />
      <article aria-label={item.titulo} className="flex flex-1 flex-col gap-3 p-5">
        <div className="flex items-center justify-between gap-2">
          <span className="flex items-center gap-2 text-pequeno font-semibold text-texto-suave">
            <IconeDoTipo tipo={item.tipo} />
            {rotuloDoTipo(item.tipo, item.sentidoAvaliacao)}
          </span>
          <Selo tom={etiqueta.tom}>{etiqueta.texto}</Selo>
        </div>
        <h2 className="text-t3 font-bold text-texto">{item.titulo}</h2>
        {item.descricao && <p className="text-tabela text-texto-suave">{item.descricao}</p>}
        <p className="flex flex-wrap gap-3 text-pequeno text-texto-suave">
          <span>
            {item.totalPerguntas} {plural(item.totalPerguntas, 'pergunta', 'perguntas')}
          </span>
          <span>{item.minutosEstimados} min</span>
          {!respondido && (
            <span
              className={cn(
                item.situacao === 'PARA_RESPONDER' && 'font-semibold text-alerta-texto',
              )}
            >
              {prazo}
            </span>
          )}
        </p>
        {respondido && item.respondidoEmUtc && (
          <p className="text-pequeno text-texto-suave">
            Respondido em {formatarDataDoInstante(item.respondidoEmUtc)}
          </p>
        )}
        <Progresso
          valor={respondido ? item.total : item.respondidos}
          maximo={Math.max(item.total, 1)}
          rotulo={`Andamento de ${item.titulo}`}
        />
        <div className="mt-auto flex justify-end">
          {respondido ? (
            item.podeVerMinhasRespostas && (
              <BotaoLink variante="secundario" tamanho="pequeno" to={`${item.id}/minhas-respostas`}>
                Ver minhas respostas
              </BotaoLink>
            )
          ) : (
            <BotaoLink
              tamanho="pequeno"
              to={`${item.id}/responder`}
              aria-label={`${continuar ? 'Continuar' : 'Responder'} ${item.titulo}`}
            >
              {continuar ? 'Continuar' : 'Responder'} →
            </BotaoLink>
          )}
        </div>
      </article>
    </li>
  )
}

/** Telas 39 e 40 (a mesma página para os três perfis). */
export function PaginaMeusFormularios() {
  const [parametros, definirParametros] = useSearchParams()
  const aba = valorDeEnum(parametros.get('aba') ?? undefined, ABAS) ?? 'todos'
  const tipo = valorDeEnum(parametros.get('tipo') ?? undefined, TIPOS)
  const busca = parametros.get('busca') ?? ''
  const pagina = Number(parametros.get('pagina') ?? '1') || 1
  const situacao = SITUACAO_DA_ABA[aba]
  const consulta = $api.useQuery('get', '/api/eu/formularios', {
    params: {
      query: {
        ...(situacao ? { situacao } : {}),
        ...(tipo ? { tipo } : {}),
        ...(busca ? { busca } : {}),
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
        titulo="Formulários"
        subtitulo="Pedidos do RH e formulários abertos para você responder"
      />
      <div className="flex flex-wrap items-end justify-between gap-4">
        <Abas<Aba>
          rotulo="Situação dos formulários"
          itens={[
            { valor: 'todos', rotulo: 'Todos', contagem: c?.todos },
            { valor: 'para-responder', rotulo: 'Para responder', contagem: c?.paraResponder },
            { valor: 'disponiveis', rotulo: 'Disponíveis', contagem: c?.disponiveis },
            { valor: 'respondidos', rotulo: 'Respondidos', contagem: c?.respondidos },
          ]}
          valor={aba}
          aoMudar={(v) => mudar('aba', v === 'todos' ? null : v)}
        />
        <div className="flex items-end gap-3">
          <div className="w-64">
            <CampoDeBusca
              rotulo="Buscar formulário"
              placeholder="Buscar formulário"
              valor={busca}
              aoMudar={(t) => mudar('busca', t.trim() || null, true)}
            />
          </div>
          <div className="w-56">
            <FiltroDeSelecao
              rotulo="Tipo"
              todos="Todos os tipos"
              opcoes={OPCOES_DE_TIPO}
              valor={tipo}
              aoMudar={(v) => mudar('tipo', v)}
            />
          </div>
        </div>
      </div>
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      ) : !dados ? (
        <div
          role="status"
          aria-label="Carregando os formulários"
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
          <ul aria-label="Meus formulários" className="grid grid-cols-3 gap-4">
            {dados.itens.map((item) => (
              <CartaoDoMeuFormulario key={item.id} item={item} />
            ))}
          </ul>
          {dados.totalPaginas > 1 && (
            <Paginacao dados={dados} aoMudar={(p) => mudar('pagina', p > 1 ? String(p) : null)} />
          )}
        </>
      )}
      <p className="text-tabela text-texto-suave">
        {aba === 'respondidos'
          ? 'Mostrando os formulários que você já respondeu.'
          : 'Os formulários que você já respondeu ficam na aba Respondidos.'}
      </p>
    </div>
  )
}
