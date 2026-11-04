import { $api, clienteHttp } from '../../shared/api/api'
import type { Esquema } from '../../shared/api/tipos'
import { useBaixarPorLink } from '../../shared/arquivos/useBaixarPorLink'
import { formatarDataHora } from '../../shared/formato/datas'
import { formatarMinutosComoHoras, formatarNumero } from '../../shared/formato/numeros'
import { Botao } from '../../shared/ui/Botao'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Selo } from '../../shared/ui/Selo'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { NumerosDoPonto } from './PecasDoPonto'
import { periodoDoAtestado, saldoComSinal, situacaoDoAtestado, tomDoSaldo } from './rotulosDoPonto'
import { TabelaDoEspelho } from './TabelaDoEspelho'

type Atestado = Esquema<'Atestado'>

/** Lista de atestados da equipe (gestor): só vê o arquivo; validar e recusar é do RH. */
export function AtestadosDaEquipe({
  funcionarioId,
  comPessoa = false,
}: {
  funcionarioId?: number
  comPessoa?: boolean
}) {
  const { baixar } = useBaixarPorLink()
  const consulta = $api.useQuery('get', '/api/gestor/ponto/atestados', {
    params: { query: funcionarioId === undefined ? {} : { funcionarioId } },
  })
  const colunas: ColunaDaTabela<Atestado>[] = [
    { chave: 'periodo', titulo: 'Período', celula: (a) => periodoDoAtestado(a) },
    { chave: 'enviado', titulo: 'Enviado em', celula: (a) => formatarDataHora(a.enviadoEmUtc) },
    {
      chave: 'situacao',
      titulo: 'Situação',
      celula: (a) => {
        const { texto, tom } = situacaoDoAtestado(a.situacao)
        return <Selo tom={tom}>{texto}</Selo>
      },
    },
    {
      chave: 'acao',
      titulo: 'Ação',
      celula: (a) => (
        <Botao
          variante="texto"
          tamanho="pequeno"
          aria-label={`Ver arquivo do atestado de ${a.funcionario.nome}`}
          onClick={() =>
            void baixar(() =>
              clienteHttp.GET('/api/gestor/ponto/atestados/{atestadoId}/download', {
                params: { path: { atestadoId: a.id } },
              }),
            )
          }
        >
          Ver arquivo
        </Botao>
      ),
    },
  ]
  if (comPessoa) {
    colunas.unshift({ chave: 'pessoa', titulo: 'Pessoa', celula: (a) => a.funcionario.nome })
  }
  return (
    <section aria-labelledby="titulo-atestados-equipe" className="flex flex-col gap-3">
      <h3 id="titulo-atestados-equipe" className="text-t3 font-bold text-texto">
        Atestados
      </h3>
      <p className="text-pequeno text-texto-suave">O acesso a este arquivo fica registrado.</p>
      <Tabela
        rotulo="Atestados da equipe"
        colunas={colunas}
        linhas={consulta.data?.itens}
        chaveDaLinha={(a) => a.id}
        carregando={consulta.isFetching && !consulta.data}
        erro={consulta.error ?? undefined}
        aoTentarDeNovo={() => void consulta.refetch()}
        vazio={<EstadoVazio titulo="Nenhum atestado." />}
      />
    </section>
  )
}

/** Números, tabela, "Baixar PDF" e atestados da pessoa (tela 56 e aba Ponto da ficha da equipe). */
export function EspelhoDaPessoaDaEquipe({
  funcionarioId,
  ano,
  mes,
  semFolgas = false,
}: {
  funcionarioId: number
  ano?: number
  mes?: number
  semFolgas?: boolean
}) {
  const { baixar, baixando } = useBaixarPorLink()
  const periodo = ano !== undefined && mes !== undefined ? { ano, mes } : {}
  const consulta = $api.useQuery('get', '/api/gestor/equipe/{funcionarioId}/ponto', {
    params: { path: { funcionarioId }, query: periodo },
  })
  const espelho = consulta.data
  const resumo = espelho?.resumo
  return (
    <div className="flex flex-col gap-5">
      <NumerosDoPonto
        rotulo="Números do espelho"
        carregando={!resumo}
        itens={[
          {
            rotulo: 'Horas no mês',
            valor: resumo ? formatarMinutosComoHoras(resumo.horasTrabalhadasNoMesMinutos) : '—',
          },
          {
            rotulo: 'Banco de horas',
            valor: saldoComSinal(resumo?.saldoBancoHorasMinutos ?? null),
            tom: tomDoSaldo(resumo?.saldoBancoHorasMinutos ?? null),
          },
          {
            rotulo: 'Ajustes em análise',
            valor: resumo ? formatarNumero(resumo.pedidosEmAnalise) : '—',
          },
        ]}
      />
      <TabelaDoEspelho
        rotulo="Espelho da pessoa"
        dias={espelho?.dias}
        semFolgas={semFolgas}
        carregando={consulta.isLoading}
        erro={consulta.error ?? undefined}
        aoTentarDeNovo={() => void consulta.refetch()}
      />
      <p className="text-pequeno text-texto-suave">
        Você pode ver o espelho, mas só o RH altera marcações. Ajustes chegam como pedidos.
      </p>
      <div>
        <Botao
          variante="secundario"
          carregando={baixando}
          onClick={() =>
            void baixar(() =>
              clienteHttp.GET('/api/gestor/equipe/{funcionarioId}/ponto/espelho/download', {
                params: { path: { funcionarioId }, query: periodo },
              }),
            )
          }
        >
          Baixar PDF
        </Botao>
      </div>
      <AtestadosDaEquipe funcionarioId={funcionarioId} />
    </div>
  )
}
