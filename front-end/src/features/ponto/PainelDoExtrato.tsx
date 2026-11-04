import { useState } from 'react'
import { useNavigate } from 'react-router'
import { $api } from '../../shared/api/api'
import type { Esquema } from '../../shared/api/tipos'
import { FiltroDeSelecao } from '../../shared/lista/FiltroDeSelecao'
import { formatarDiaMesDaData } from '../../shared/formato/datas'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Janela } from '../../shared/ui/Janela'
import { Paginacao } from '../../shared/ui/Paginacao'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { Saldo } from './PecasDoPonto'
import { rotuloDoTipoDeLancamento, sinalDoLancamento } from './rotulosDoPonto'

type Lancamento = Esquema<'BancoHorasLancamento'>
type Tipo = Esquema<'BancoHorasTipoLancamento'>

const COLUNAS: readonly ColunaDaTabela<Lancamento>[] = [
  { chave: 'dia', titulo: 'Dia', celula: (l) => formatarDiaMesDaData(l.diaReferencia) },
  {
    chave: 'tipo',
    titulo: 'Tipo',
    celula: (l) => <span className="font-bold">{rotuloDoTipoDeLancamento(l.tipo)}</span>,
  },
  { chave: 'descricao', titulo: 'Descrição', celula: (l) => l.descricao },
  {
    chave: 'quantidade',
    titulo: 'Quantidade',
    alinhamento: 'direita',
    celula: (l) => (
      <span
        className={
          l.tipo === 'CREDITO'
            ? 'tabular-nums text-primaria-media'
            : 'tabular-nums text-alerta-texto'
        }
      >
        {sinalDoLancamento(l)}
      </span>
    ),
  },
]

/** O extrato completo (sem print): painel sobre Meu ponto. Só Crédito e Débito (decisão do dono). */
export function PainelDoExtrato() {
  const navegar = useNavigate()
  const [de, setDe] = useState('')
  const [ate, setAte] = useState('')
  const [tipo, setTipo] = useState<Tipo | undefined>(undefined)
  const [pagina, setPagina] = useState(1)
  const consulta = $api.useQuery('get', '/api/eu/ponto/banco-de-horas', {
    params: {
      query: {
        ...(de ? { de } : {}),
        ...(ate ? { ate } : {}),
        ...(tipo ? { tipo } : {}),
        ...(pagina > 1 ? { pagina } : {}),
      },
    },
  })
  const extrato = consulta.data
  const fechar = () => navegar('/eu/meu-ponto')
  return (
    <Janela aberta aoFechar={fechar} titulo="Banco de horas" posicao="direita" largura="larga">
      <div className="flex flex-col gap-5">
        <div className="flex flex-col gap-1">
          {extrato ? (
            <Saldo minutos={extrato.saldoMinutos} className="text-numero font-bold" />
          ) : (
            <span className="text-numero font-bold text-texto-suave">—</span>
          )}
          <span className="text-pequeno text-texto-suave">
            {extrato?.atualizadoAte
              ? `Atualizado até ${formatarDiaMesDaData(extrato.atualizadoAte)}`
              : extrato
                ? 'Banco de horas ainda não atualizado.'
                : ''}
          </span>
        </div>
        <div className="grid grid-cols-3 items-end gap-4">
          <Campo rotulo="De">
            <Entrada
              type="date"
              lang="pt-BR"
              value={de}
              onChange={(e) => {
                setDe(e.target.value)
                setPagina(1)
              }}
            />
          </Campo>
          <Campo rotulo="Até">
            <Entrada
              type="date"
              lang="pt-BR"
              value={ate}
              onChange={(e) => {
                setAte(e.target.value)
                setPagina(1)
              }}
            />
          </Campo>
          <FiltroDeSelecao
            rotulo="Tipo"
            todos="Todos"
            opcoes={[
              { valor: 'CREDITO', rotulo: 'Crédito' },
              { valor: 'DEBITO', rotulo: 'Débito' },
            ]}
            valor={tipo}
            aoMudar={(v) => {
              setTipo(v ?? undefined)
              setPagina(1)
            }}
          />
        </div>
        <Tabela
          rotulo="Lançamentos do banco de horas"
          colunas={COLUNAS}
          linhas={extrato?.itens}
          chaveDaLinha={(l) => l.id}
          carregando={consulta.isFetching && !extrato}
          erro={consulta.error ?? undefined}
          aoTentarDeNovo={() => void consulta.refetch()}
          vazio={<EstadoVazio titulo="Nenhum lançamento neste período." />}
          rodape={
            extrato && extrato.totalPaginas > 1 ? (
              <Paginacao dados={extrato} aoMudar={setPagina} />
            ) : undefined
          }
        />
      </div>
    </Janela>
  )
}
