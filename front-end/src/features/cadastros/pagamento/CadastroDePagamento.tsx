import { useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { $api } from '../../../shared/api/api'
import { ErroDeApi } from '../../../shared/api/erroDeApi'
import { invalidarRota } from '../../../shared/api/invalidar'
import { mensagemDeErro } from '../../../shared/api/mensagemDeErro'
import type { Esquema } from '../../../shared/api/tipos'
import { formatarData, hojeEmSaoPaulo, nomeDoMes } from '../../../shared/formato/datas'
import { Aviso } from '../../../shared/ui/Aviso'
import { AvisoDeErro } from '../../../shared/ui/AvisoDeErro'
import { Botao } from '../../../shared/ui/Botao'
import { Campo } from '../../../shared/ui/Campo'
import { Entrada } from '../../../shared/ui/Entrada'
import { EstadoVazio } from '../../../shared/ui/EstadoVazio'
import { Janela } from '../../../shared/ui/Janela'
import { JanelaDeConfirmacao } from '../../../shared/ui/JanelaDeConfirmacao'
import { Selecao } from '../../../shared/ui/Selecao'
import { Tabela, type ColunaDaTabela } from '../../../shared/ui/Tabela'
import { useNotificacao } from '../../../shared/ui/useNotificacao'

type Data = Esquema<'PagamentoData'>
type Excecao = Esquema<'PagamentoExcecao'>
type Ajuste = Data['ajustes'][number]

const FRASE_DO_AJUSTE: Record<Ajuste, string> = {
  ULTIMO_DIA_DO_MES: 'O mês não tem esse dia: vale o último dia',
  ANTECIPADO_FIM_DE_SEMANA: 'Antecipado: caiu no fim de semana',
  ANTECIPADO_FERIADO: 'Antecipado: caiu em feriado',
}

const maiuscula = (texto: string) => `${texto.charAt(0).toUpperCase()}${texto.slice(1)}`
const mesEAno = (ano: number, mes: number) => `${maiuscula(nomeDoMes(mes))} de ${ano}`
const doisDigitos = (n: number) => String(n).padStart(2, '0')

/** Dentro do mês e do ano escolhidos (a API recusa a data de fora com 400). */
function dataDentroDoMes(data: string, ano: number, mes: number): boolean {
  return data.startsWith(`${ano}-${doisDigitos(mes)}-`)
}

function ModalDaExcecao({
  aberta,
  anoInicial,
  aoFechar,
}: {
  aberta: boolean
  anoInicial: number
  aoFechar: () => void
}) {
  return (
    <Janela aberta={aberta} aoFechar={aoFechar} titulo="Nova exceção de pagamento" tamanho="media">
      {aberta && <CorpoDaExcecao anoInicial={anoInicial} aoFechar={aoFechar} />}
    </Janela>
  )
}

function CorpoDaExcecao({ anoInicial, aoFechar }: { anoInicial: number; aoFechar: () => void }) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const definir = $api.useMutation('put', '/api/rh/pagamento/excecoes/{ano}/{mes}')
  const hoje = hojeEmSaoPaulo()
  const [mes, setMes] = useState(String(Number(hoje.slice(5, 7))))
  const [ano, setAno] = useState(String(anoInicial))
  const [data, setData] = useState('')
  const [erros, setErros] = useState<Record<string, string>>({})
  const [erroGeral, setErroGeral] = useState<string | null>(null)

  async function salvar() {
    if (definir.isPending) return
    const novos: Record<string, string> = {}
    const anoNumero = Number(ano)
    if (!/^\d{4}$/.test(ano) || anoNumero < 2000 || anoNumero > 2100)
      novos.ano = 'Use um ano de 2000 a 2100.'
    if (!data) novos.dataPagamento = 'Escolha a data do pagamento.'
    else if (!novos.ano && !dataDentroDoMes(data, anoNumero, Number(mes)))
      novos.dataPagamento = 'A data tem de cair dentro do mês e do ano escolhidos.'
    setErros(novos)
    setErroGeral(null)
    if (Object.keys(novos).length) return
    try {
      await definir.mutateAsync({
        params: { path: { ano: anoNumero, mes: Number(mes) } },
        body: { dataPagamento: data },
      })
      notificacao.sucesso('Exceção salva.')
      await Promise.all([
        invalidarRota(queryClient, '/api/rh/pagamento'),
        invalidarRota(queryClient, '/api/eu/painel'),
        invalidarRota(queryClient, '/api/eu/pagamento'),
      ])
      aoFechar()
    } catch (e) {
      const campos: Record<string, string> = {}
      if (e instanceof ErroDeApi)
        for (const [chave, mensagens] of Object.entries(e.errors))
          campos[chave.charAt(0).toLowerCase() + chave.slice(1)] = mensagens.join(' ')
      setErros(campos)
      if (Object.keys(campos).length === 0) setErroGeral(mensagemDeErro(e))
    }
  }

  return (
    <form
      className="flex flex-col gap-5"
      onSubmit={(evento) => {
        evento.preventDefault()
        void salvar()
      }}
    >
      <Aviso tom="info">
        A data da exceção vale como está: não é antecipada por fim de semana nem por feriado. Quem
        escolhe a data é o RH. Se já existir uma exceção para o mês, esta a substitui.
      </Aviso>
      <div className="grid grid-cols-2 gap-4">
        <Selecao
          rotulo="Mês"
          valor={mes}
          aoMudar={(v) => v && setMes(v)}
          opcoes={Array.from({ length: 12 }, (_, i) => ({
            valor: String(i + 1),
            rotulo: maiuscula(nomeDoMes(i + 1)),
          }))}
        />
        <Campo rotulo="Ano" erro={erros.ano}>
          <Entrada
            inputMode="numeric"
            maxLength={4}
            value={ano}
            onChange={(e) => setAno(e.target.value)}
          />
        </Campo>
      </div>
      <Campo rotulo="Data do pagamento" erro={erros.dataPagamento}>
        <Entrada type="date" lang="pt-BR" value={data} onChange={(e) => setData(e.target.value)} />
      </Campo>
      {erroGeral && <Aviso tom="erro">{erroGeral}</Aviso>}
      <div className="flex justify-end gap-3">
        <Botao variante="secundario" onClick={aoFechar} disabled={definir.isPending}>
          Cancelar
        </Botao>
        <Botao type="submit" carregando={definir.isPending}>
          Salvar exceção
        </Botao>
      </div>
    </form>
  )
}

/** Tela 7.4: "Pagamento" em Cadastros: a regra do dia, o calendário do ano e as exceções por mês. */
export function CadastroDePagamento() {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const anoAtual = Number(hojeEmSaoPaulo().slice(0, 4))
  const [ano, setAno] = useState(anoAtual)
  const [criando, setCriando] = useState(false)
  const [removendo, setRemovendo] = useState<Excecao | null>(null)
  const [erroDaRemocao, setErroDaRemocao] = useState<unknown>(null)
  const empresa = $api.useQuery('get', '/api/eu/empresa', {}, { retry: false })
  const calendario = $api.useQuery('get', '/api/rh/pagamento/calendario', {
    params: { query: { ano } },
  })
  const excecoes = $api.useQuery('get', '/api/rh/pagamento/excecoes')
  const remover = $api.useMutation('delete', '/api/rh/pagamento/excecoes/{ano}/{mes}')
  const dia = empresa.data?.diaPagamento

  const colunasDoCalendario: readonly ColunaDaTabela<Data>[] = [
    { chave: 'mes', titulo: 'Mês', celula: (m) => maiuscula(nomeDoMes(m.mes)) },
    { chave: 'data', titulo: 'Data do pagamento', celula: (m) => formatarData(m.data) },
    {
      chave: 'origem',
      titulo: 'Origem',
      celula: (m) => (m.origem === 'EXCECAO' ? 'Exceção' : 'Regra'),
    },
    {
      chave: 'ajustes',
      titulo: 'Ajustes',
      celula: (m) =>
        m.ajustes.length === 0 ? (
          '—'
        ) : (
          <span className="flex flex-col gap-1">
            {m.ajustes.map((a) => (
              <span key={a}>
                {a === 'ULTIMO_DIA_DO_MES' && dia
                  ? `O mês não tem o dia ${dia}: vale o último dia`
                  : FRASE_DO_AJUSTE[a]}
              </span>
            ))}
          </span>
        ),
    },
  ]
  const colunasDasExcecoes: readonly ColunaDaTabela<Excecao>[] = [
    { chave: 'mes', titulo: 'Mês e ano', celula: (x) => mesEAno(x.ano, x.mes) },
    { chave: 'data', titulo: 'Data do pagamento', celula: (x) => formatarData(x.dataPagamento) },
    {
      chave: 'acoes',
      titulo: 'Ações',
      alinhamento: 'direita',
      celula: (x) => (
        <Botao
          variante="perigo"
          tamanho="pequeno"
          aria-label={`Remover a exceção de ${mesEAno(x.ano, x.mes)}`}
          onClick={() => {
            setErroDaRemocao(null)
            setRemovendo(x)
          }}
        >
          Remover
        </Botao>
      ),
    },
  ]

  return (
    <div className="flex flex-col gap-6">
      <section aria-labelledby="titulo-regra" className="flex flex-col gap-2">
        <h2 id="titulo-regra" className="text-t3 font-bold text-texto">
          Regra do pagamento
        </h2>
        <p className="text-corpo text-texto">
          {empresa.isLoading
            ? 'Carregando…'
            : dia
              ? `Todo dia ${dia}`
              : 'O dia de pagamento ainda não foi definido.'}
        </p>
        <p className="text-tabela text-texto-suave">
          A regra é a mesma para a empresa toda. Mês sem esse dia vale o último dia; fim de semana
          ou feriado antecipa para o dia útil anterior.{' '}
          <Link to="../empresa" className="font-semibold text-primaria underline">
            Alterar o dia em Empresa
          </Link>
        </p>
      </section>

      <section aria-labelledby="titulo-calendario" className="flex flex-col gap-3">
        <div className="flex items-end justify-between gap-3">
          <h2 id="titulo-calendario" className="text-t3 font-bold text-texto">
            Calendário do ano
          </h2>
          <div className="w-40">
            <Selecao
              rotulo="Ano"
              valor={String(ano)}
              aoMudar={(v) => v && setAno(Number(v))}
              opcoes={[anoAtual - 1, anoAtual, anoAtual + 1].map((a) => ({
                valor: String(a),
                rotulo: String(a),
              }))}
            />
          </div>
        </div>
        <Tabela
          rotulo="Calendário de pagamento"
          colunas={colunasDoCalendario}
          linhas={calendario.data?.meses}
          chaveDaLinha={(m) => `${m.ano}-${m.mes}`}
          carregando={calendario.isLoading}
          erro={calendario.error ?? undefined}
          aoTentarDeNovo={() => void calendario.refetch()}
          vazio={<EstadoVazio titulo="Sem dia de pagamento definido para este ano." />}
        />
      </section>

      <section aria-labelledby="titulo-excecoes" className="flex flex-col gap-3">
        <div className="flex items-center justify-between gap-3">
          <h2 id="titulo-excecoes" className="text-t3 font-bold text-texto">
            Exceções
          </h2>
          <Botao onClick={() => setCriando(true)}>
            <Plus aria-hidden="true" className="h-4 w-4" />
            Nova exceção
          </Botao>
        </div>
        {excecoes.error ? (
          <AvisoDeErro erro={excecoes.error} aoTentarDeNovo={() => void excecoes.refetch()} />
        ) : (
          <Tabela
            rotulo="Exceções de pagamento"
            colunas={colunasDasExcecoes}
            linhas={excecoes.data?.itens}
            chaveDaLinha={(x) => `${x.ano}-${x.mes}`}
            carregando={excecoes.isLoading}
            vazio={<EstadoVazio titulo="Nenhuma exceção cadastrada." />}
          />
        )}
      </section>

      <ModalDaExcecao aberta={criando} anoInicial={ano} aoFechar={() => setCriando(false)} />
      <JanelaDeConfirmacao
        aberta={removendo !== null}
        titulo={
          removendo
            ? `Remover a exceção de ${mesEAno(removendo.ano, removendo.mes)}?`
            : 'Remover a exceção?'
        }
        descricao="O pagamento desse mês volta a seguir a regra."
        rotuloDeConfirmar="Remover"
        perigo
        confirmando={remover.isPending}
        erro={erroDaRemocao ?? undefined}
        aoCancelar={() => setRemovendo(null)}
        aoConfirmar={async () => {
          if (!removendo) return
          try {
            await remover.mutateAsync({
              params: { path: { ano: removendo.ano, mes: removendo.mes } },
            })
            notificacao.sucesso('Exceção removida.')
            setRemovendo(null)
          } catch (e) {
            // 404: outra pessoa já tirou; a lista recarrega e a janela fecha
            if (e instanceof ErroDeApi && e.status === 404) setRemovendo(null)
            else {
              setErroDaRemocao(e)
              return
            }
          }
          await Promise.all([
            invalidarRota(queryClient, '/api/rh/pagamento'),
            invalidarRota(queryClient, '/api/eu/painel'),
            invalidarRota(queryClient, '/api/eu/pagamento'),
          ])
        }}
      />
    </div>
  )
}
