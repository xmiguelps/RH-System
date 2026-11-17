import { Link } from 'react-router'
import { $api } from '../../shared/api/api'
import {
  formatarData,
  formatarDiaMesDaData,
  formatarInstanteRelativo,
} from '../../shared/formato/datas'
import { plural } from '../../shared/formato/texto'
import { Avatar } from '../../shared/ui/Avatar'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { textoDeDias } from '../ferias/rotulosDasFerias'

const CARTAO = 'flex flex-col gap-3 rounded-cartao border border-borda bg-superficie p-6'

/** "Férias disponíveis" (`obterPainelDoFuncionario.ferias`). */
export function CartaoFeriasDisponiveis({ dias, prazo }: { dias: number; prazo: string | null }) {
  return (
    <section aria-label="Férias disponíveis" className={CARTAO}>
      <h2 className="text-t3 font-bold text-texto">Férias disponíveis</h2>
      <span className="text-numero font-bold text-texto">{textoDeDias(dias)}</span>
      {prazo && (
        <span className="text-pequeno text-texto-suave">Vencem em {formatarData(prazo)}</span>
      )}
      <Link to="/eu/ferias/pedir" className="text-corpo font-semibold text-primaria">
        Pedir férias
      </Link>
    </section>
  )
}

/** "Seus benefícios": nome e, quando o contrato traz, o detalhe (dependentes aprovados ou as linhas do trajeto). */
export function CartaoSeusBeneficios({
  beneficios,
}: {
  beneficios: readonly { adesaoId: number; nome: string; detalhe: string | null }[]
}) {
  return (
    <section aria-label="Seus benefícios" className={CARTAO}>
      <h2 className="text-t3 font-bold text-texto">Seus benefícios</h2>
      {beneficios.length === 0 ? (
        <p className="text-corpo text-texto-suave">Você ainda não tem benefícios ativos.</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {beneficios.map((b) => (
            <li key={b.adesaoId} className="flex flex-col">
              <span className="text-corpo font-semibold text-texto">{b.nome}</span>
              {b.detalhe && <span className="text-tabela text-texto-suave">{b.detalhe}</span>}
            </li>
          ))}
        </ul>
      )}
      <Link to="/eu/beneficios" className="text-corpo font-semibold text-primaria">
        Ver detalhes dos benefícios
      </Link>
    </section>
  )
}

/** "Avisos": os dois mais recentes (`listarMeusAvisos`, página de 2). */
export function CartaoAvisosRecentes() {
  const consulta = $api.useQuery('get', '/api/eu/avisos', {
    params: { query: { pagina: 1, tamanhoPagina: 2 } },
  })
  return (
    <section aria-label="Avisos" className={CARTAO}>
      <h2 className="text-t3 font-bold text-texto">Avisos</h2>
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      ) : !consulta.data ? (
        <Esqueleto className="h-20 w-full" />
      ) : consulta.data.itens.length === 0 ? (
        <p className="text-corpo text-texto-suave">Nenhum aviso por enquanto.</p>
      ) : (
        <ul className="flex flex-col gap-4">
          {consulta.data.itens.slice(0, 2).map((a) => (
            <li key={a.id} className="flex flex-col gap-1">
              <span className="text-corpo font-semibold text-texto">{a.titulo}</span>
              <span className="text-tabela text-texto-suave">{a.previa}</span>
              <span className="text-pequeno text-texto-suave">
                {formatarInstanteRelativo(a.publicadoEmUtc)}
                {a.autorNome ? `, publicado por ${a.autorNome}` : ', publicado pelo RH'}
              </span>
              <Link to={`/eu/avisos/${a.id}`} className="text-tabela font-semibold text-primaria">
                Ler aviso completo
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

/** "Aniversariantes": quem faz hoje e a lista dos próximos dias (`listarAniversariantes`). */
export function CartaoAniversariantes() {
  const consulta = $api.useQuery('get', '/api/eu/aniversariantes')
  const hoje = consulta.data?.itens.filter((i) => i.diasAte === 0) ?? []
  const proximos = consulta.data?.itens.filter((i) => i.diasAte > 0) ?? []
  return (
    <section aria-label="Aniversariantes" className={CARTAO}>
      <h2 className="text-t3 font-bold text-texto">Aniversariantes</h2>
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      ) : !consulta.data ? (
        <Esqueleto className="h-20 w-full" />
      ) : (
        <>
          <p className="text-tabela text-texto-suave">
            {hoje.length > 0
              ? `Hoje: ${hoje.map((i) => i.funcionario.nome).join(', ')}.`
              : 'Ninguém faz aniversário hoje.'}{' '}
            {proximos.length > 0 &&
              `Nos próximos ${consulta.data.dias} ${plural(consulta.data.dias, 'dia', 'dias')}:`}
          </p>
          {proximos.length > 0 && (
            <ul className="flex flex-col gap-2">
              {proximos.map((i) => (
                <li key={i.funcionario.id} className="flex items-center gap-3">
                  <Avatar nome={i.funcionario.nome} />
                  <span className="flex flex-1 flex-col">
                    <span className="text-corpo text-texto">{i.funcionario.nome}</span>
                    {i.funcionario.setor && (
                      <span className="text-pequeno text-texto-suave">{i.funcionario.setor}</span>
                    )}
                  </span>
                  <span className="text-tabela font-semibold text-texto">
                    {formatarDiaMesDaData(i.aniversarioEm)}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  )
}

/** A faixa do gestor: pedidos de ajuste de ponto do setor esperando a análise dele (`obterContadoresDoMenuDoGestor`). */
export function FaixaDeAjustesDoSetor() {
  const contadores = $api.useQuery('get', '/api/gestor/contadores-do-menu', {}, { retry: false })
  const n = contadores.data?.pedidosDeAjusteDePonto ?? 0
  if (n <= 0) return null
  return (
    <div
      role="status"
      className="flex items-center justify-between gap-4 rounded-cartao bg-alerta-fundo px-5 py-4 text-corpo text-alerta-forte"
    >
      <span>
        Você tem {n} {plural(n, 'pedido de ajuste de ponto', 'pedidos de ajuste de ponto')} do setor
        esperando sua análise.
      </span>
      <Link to="/gestor/equipe" className="font-semibold underline">
        Analisar agora
      </Link>
    </div>
  )
}
