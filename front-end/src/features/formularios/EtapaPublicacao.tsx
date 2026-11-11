import { X } from 'lucide-react'
import { $api } from '../../shared/api/api'
import type { Esquema } from '../../shared/api/tipos'
import { hojeEmSaoPaulo } from '../../shared/formato/datas'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'
import { Campo } from '../../shared/ui/Campo'
import { ComboDeBusca } from '../../shared/ui/ComboDeBusca'
import { Entrada } from '../../shared/ui/Entrada'
import { OpcoesEmCartao } from '../../shared/ui/OpcoesEmCartao'
import { Selecao } from '../../shared/ui/Selecao'
import { useBuscaDeColaborador } from '../ponto/useBuscaDeColaborador'
import type { Publicacao } from './publicacao'

type Alvo = Esquema<'FormularioAlvo'>
type Formulario = Esquema<'FormularioDetalheRh'>

export type ErrosDaPublicacao = Partial<
  Record<'setorIds' | 'funcionarioIds' | 'prazo' | 'agendarPara' | 'recorrencia', string>
>

function SetoresEscolhidos({
  valor,
  aoMudar,
  desabilitado,
}: {
  valor: readonly number[]
  aoMudar: (ids: number[]) => void
  desabilitado: boolean
}) {
  const setores = $api.useQuery('get', '/api/rh/setores', {
    params: { query: { pagina: 1, tamanhoPagina: 100 } },
  })
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="text-tabela font-semibold text-texto">Setores</legend>
      {(setores.data?.itens ?? []).map((s) => (
        <CaixaDeSelecao
          key={s.id}
          rotulo={s.nome}
          marcada={valor.includes(s.id)}
          desabilitada={desabilitado}
          aoMudar={(marcada) =>
            aoMudar(marcada ? [...valor, s.id] : valor.filter((id) => id !== s.id))
          }
        />
      ))}
    </fieldset>
  )
}

function GestoresEscolhidos({
  valor,
  aoMudar,
  desabilitado,
}: {
  valor: readonly { id: number; nome: string }[]
  aoMudar: (pessoas: { id: number; nome: string }[]) => void
  desabilitado: boolean
}) {
  const gestores = $api.useQuery('get', '/api/rh/gestores')
  const comSetor = (gestores.data?.itens ?? []).filter((g) => !g.soDecidePedidosDeGestores)
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="text-tabela font-semibold text-texto">
        Gestores que avaliam a equipe
      </legend>
      {comSetor.map((g) => (
        <CaixaDeSelecao
          key={g.funcionario.id}
          rotulo={g.funcionario.nome}
          descricao={g.setor ? `Setor ${g.setor.nome}` : undefined}
          marcada={valor.some((p) => p.id === g.funcionario.id)}
          desabilitada={desabilitado}
          aoMudar={(marcada) =>
            aoMudar(
              marcada
                ? [...valor, { id: g.funcionario.id, nome: g.funcionario.nome }]
                : valor.filter((p) => p.id !== g.funcionario.id),
            )
          }
        />
      ))}
    </fieldset>
  )
}

function PessoasEscolhidas({
  valor,
  aoMudar,
  desabilitado,
}: {
  valor: readonly { id: number; nome: string }[]
  aoMudar: (pessoas: { id: number; nome: string }[]) => void
  desabilitado: boolean
}) {
  const busca = useBuscaDeColaborador()
  return (
    <div className="flex flex-col gap-2">
      <Campo rotulo="Pessoas">
        <ComboDeBusca
          rotulo="Pessoas"
          placeholder="Buscar pessoa"
          opcoes={busca.opcoes}
          valor={null}
          aoDigitar={busca.aoDigitar}
          carregando={busca.carregando}
          aoMudar={(v) => {
            const opcao = busca.opcoes.find((o) => o.valor === v)
            if (opcao && !valor.some((p) => p.id === Number(opcao.valor)))
              aoMudar([...valor, { id: Number(opcao.valor), nome: opcao.rotulo }])
          }}
        />
      </Campo>
      {valor.length > 0 && (
        <ul aria-label="Pessoas escolhidas" className="flex flex-wrap gap-2">
          {valor.map((p) => (
            <li
              key={p.id}
              className="flex items-center gap-1 rounded-full bg-superficie-destaque px-3 py-1 text-tabela text-texto"
            >
              {p.nome}
              <button
                type="button"
                disabled={desabilitado}
                aria-label={`Tirar ${p.nome}`}
                onClick={() => aoMudar(valor.filter((x) => x.id !== p.id))}
                className="flex h-5 w-5 items-center justify-center rounded-full text-texto-suave hover:bg-borda"
              >
                <X size={12} aria-hidden="true" />
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

/** Tela 20: a etapa Publicação (sem nota mínima, sem e-mail do resultado e sem convite dos aprovados). */
export function EtapaPublicacao({
  formulario,
  valores,
  aoMudar,
  erros,
}: {
  formulario: Formulario
  valores: Publicacao
  aoMudar: (v: Publicacao) => void
  erros: ErrosDaPublicacao
}) {
  const travado =
    formulario.situacao === 'ENCERRADO' ||
    (formulario.anonimo && formulario.situacao === 'PUBLICADO')
  const ehSugestao = formulario.tipo === 'OUTRO'
  const linkPossivel = formulario.tipo === 'ENTREVISTA' && !formulario.anonimo
  const dicaDoLink = formulario.anonimo
    ? 'Formulário anônimo não tem link público'
    : 'Só a entrevista usa link'
  const hoje = hojeEmSaoPaulo()
  const mostraLembrete = !!valores.prazo && valores.alvo !== 'LINK' && !ehSugestao
  const gestorAvaliaEquipe = formulario.sentidoAvaliacao === 'GESTOR_AVALIA_EQUIPE'
  return (
    <div className="flex flex-col gap-5">
      <h2 className="text-t3 font-bold text-texto">Publicação</h2>
      {travado && (
        <p className="text-pequeno text-texto-suave">
          {formulario.situacao === 'ENCERRADO'
            ? 'Formulário encerrado não muda.'
            : 'Num formulário anônimo publicado, quem responde não muda.'}
        </p>
      )}
      <OpcoesEmCartao<Alvo>
        rotulo="Quem responde"
        colunas={3}
        valor={valores.alvo}
        aoMudar={(alvo) => aoMudar({ ...valores, alvo })}
        opcoes={[
          {
            valor: 'LINK',
            titulo: 'Candidatos, por link',
            descricao: linkPossivel ? 'Qualquer pessoa com o link responde' : dicaDoLink,
            desabilitada: travado || !linkPossivel,
          },
          {
            valor: 'SETORES',
            titulo: 'Setores',
            descricao:
              formulario.sentidoAvaliacao === 'EQUIPE_AVALIA_GESTOR'
                ? 'Quem é do setor avalia o gestor'
                : 'Todos de um ou mais setores',
            desabilitada: travado || gestorAvaliaEquipe,
          },
          {
            valor: 'PESSOAS',
            titulo: gestorAvaliaEquipe ? 'Gestores escolhidos' : 'Pessoas escolhidas',
            descricao: gestorAvaliaEquipe
              ? 'Cada gestor avalia a equipe dele'
              : 'Você marca quem responde',
            desabilitada: travado,
          },
        ]}
      />
      {valores.alvo === 'SETORES' && (
        <SetoresEscolhidos
          valor={valores.setorIds}
          desabilitado={travado}
          aoMudar={(setorIds) => aoMudar({ ...valores, setorIds })}
        />
      )}
      {valores.alvo === 'PESSOAS' &&
        (gestorAvaliaEquipe ? (
          <GestoresEscolhidos
            valor={valores.pessoas}
            desabilitado={travado}
            aoMudar={(pessoas) => aoMudar({ ...valores, pessoas })}
          />
        ) : (
          <PessoasEscolhidas
            valor={valores.pessoas}
            desabilitado={travado}
            aoMudar={(pessoas) => aoMudar({ ...valores, pessoas })}
          />
        ))}
      {(erros.setorIds || erros.funcionarioIds) && (
        <p className="text-pequeno text-erro-texto">{erros.setorIds ?? erros.funcionarioIds}</p>
      )}
      {formulario.anonimo && (
        <p className="text-pequeno text-texto-suave">
          Num formulário anônimo, quem responde é quem estiver no público no dia da publicação: quem
          entrar depois não recebe.
        </p>
      )}
      <OpcoesEmCartao<'AGORA' | 'AGENDAR'>
        rotulo="Quando publicar"
        valor={valores.quando}
        aoMudar={(quando) => aoMudar({ ...valores, quando })}
        opcoes={[
          { valor: 'AGORA', titulo: 'Publicar agora', desabilitada: travado },
          { valor: 'AGENDAR', titulo: 'Agendar para uma data', desabilitada: travado },
        ]}
      />
      <div className="grid grid-cols-2 gap-4">
        {valores.quando === 'AGENDAR' && (
          <Campo rotulo="Publicar no dia" erro={erros.agendarPara}>
            <Entrada
              type="date"
              lang="pt-BR"
              min={hoje}
              value={valores.agendarPara}
              disabled={travado}
              onChange={(e) => aoMudar({ ...valores, agendarPara: e.target.value })}
            />
          </Campo>
        )}
        {!ehSugestao && (
          <Campo rotulo="Aceitar respostas até" erro={erros.prazo}>
            <Entrada
              type="date"
              lang="pt-BR"
              min={valores.agendarPara || hoje}
              value={valores.prazo}
              disabled={formulario.situacao === 'ENCERRADO'}
              onChange={(e) => aoMudar({ ...valores, prazo: e.target.value })}
            />
          </Campo>
        )}
      </div>
      {mostraLembrete && (
        <CaixaDeSelecao
          marcada={valores.lembreteAutomatico}
          aoMudar={(lembreteAutomatico) => aoMudar({ ...valores, lembreteAutomatico })}
          desabilitada={formulario.situacao === 'ENCERRADO'}
          rotulo="Lembrar quem não respondeu 7 dias antes do prazo"
        />
      )}
      {!ehSugestao && (
        <div className="flex flex-col gap-3 rounded-cartao bg-superficie-suave p-4">
          <CaixaDeSelecao
            marcada={valores.repetir}
            aoMudar={(repetir) => aoMudar({ ...valores, repetir })}
            desabilitada={travado}
            rotulo="Repetir"
            descricao="Ao reabrir, as respostas da rodada anterior são apagadas. Baixe as respostas antes."
          />
          {valores.repetir && (
            <div className="grid grid-cols-3 gap-4">
              <Campo rotulo="A cada" erro={erros.recorrencia}>
                <Entrada
                  type="number"
                  min={1}
                  max={365}
                  value={valores.intervalo}
                  disabled={travado}
                  onChange={(e) => aoMudar({ ...valores, intervalo: e.target.value })}
                />
              </Campo>
              <Campo rotulo="Unidade">
                <Selecao
                  rotulo="Unidade"
                  valor={valores.unidade}
                  desabilitado={travado}
                  opcoes={[
                    { valor: 'DIA', rotulo: 'dias' },
                    { valor: 'SEMANA', rotulo: 'semanas' },
                    { valor: 'MES', rotulo: 'meses' },
                  ]}
                  aoMudar={(v) => v && aoMudar({ ...valores, unidade: v })}
                />
              </Campo>
              <Campo rotulo="Repetir até">
                <Entrada
                  type="date"
                  lang="pt-BR"
                  value={valores.ate}
                  disabled={travado}
                  onChange={(e) => aoMudar({ ...valores, ate: e.target.value })}
                />
              </Campo>
            </div>
          )}
        </div>
      )}
      {valores.alvo !== 'LINK' && (
        <p className="text-pequeno text-texto-suave">
          Ninguém recebe e-mail ao publicar. Se quiser avisar, publique um Aviso.
        </p>
      )}
    </div>
  )
}
