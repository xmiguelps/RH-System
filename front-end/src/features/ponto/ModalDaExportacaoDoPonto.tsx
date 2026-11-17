import { ArrowDown, ArrowUp } from 'lucide-react'
import { useState } from 'react'
import { clienteHttp } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { baixarExportacao } from '../../shared/arquivos/baixar'
import { formatarMesAno } from '../../shared/formato/datas'
import { Aviso } from '../../shared/ui/Aviso'
import { Botao } from '../../shared/ui/Botao'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'
import { Janela } from '../../shared/ui/Janela'
import { OpcoesEmCartao } from '../../shared/ui/OpcoesEmCartao'

type FormatoDasHoras = Esquema<'PontoFolhaFormatoHoras'>
type Separador = Esquema<'PontoFolhaSeparador'>

/** As 12 colunas de sempre, na ordem de sempre, com o código que o contrato pede (sem a unidade). */
const COLUNAS = [
  { codigo: 'matricula', rotulo: 'Matrícula' },
  { codigo: 'nome', rotulo: 'Nome' },
  { codigo: 'setor', rotulo: 'Setor' },
  { codigo: 'cargo', rotulo: 'Cargo' },
  { codigo: 'dias_trabalhados', rotulo: 'Dias trabalhados' },
  { codigo: 'horas_trabalhadas', rotulo: 'Horas trabalhadas' },
  { codigo: 'horas_previstas', rotulo: 'Horas previstas' },
  { codigo: 'atrasos', rotulo: 'Atrasos' },
  { codigo: 'faltas', rotulo: 'Faltas' },
  { codigo: 'faltas_compensadas', rotulo: 'Faltas compensadas' },
  { codigo: 'banco_credito_do_mes', rotulo: 'Crédito do banco de horas no mês' },
  { codigo: 'banco_debito_do_mes', rotulo: 'Débito do banco de horas no mês' },
] as const

const CODIGOS_PADRAO = COLUNAS.map((c) => c.codigo)

const FORMATOS = [
  { valor: 'MINUTOS', titulo: 'Minutos', descricao: 'Exemplo: 1041' },
  { valor: 'HORAS_DECIMAIS', titulo: 'Horas decimais', descricao: 'Exemplo: 17,35' },
  { valor: 'HH_MM', titulo: 'Horas e minutos', descricao: 'Exemplo: 17:21' },
] as const satisfies readonly { valor: FormatoDasHoras; titulo: string; descricao: string }[]

const SEPARADORES = [
  { valor: 'PONTO_E_VIRGULA', titulo: 'Ponto e vírgula', descricao: 'Exemplo: nome;horas' },
  { valor: 'VIRGULA', titulo: 'Vírgula', descricao: 'Exemplo: nome,horas' },
] as const satisfies readonly { valor: Separador; titulo: string; descricao: string }[]

/**
 * "Exportar para a folha" (tela 12, contrato 0.3.0): o RH escolhe as colunas e a ordem delas, como saem as
 * horas e o separador. O que ficar como o padrão do contrato nem vai na chamada (o arquivo é o de sempre).
 */
export function ModalDaExportacaoDoPonto({
  aberta,
  ano,
  mes,
  aoFechar,
}: {
  aberta: boolean
  ano: number
  mes: number
  aoFechar: () => void
}) {
  return (
    <Janela aberta={aberta} aoFechar={aoFechar} titulo="Exportar para a folha" tamanho="media">
      {aberta && <Corpo ano={ano} mes={mes} aoFechar={aoFechar} />}
    </Janela>
  )
}

function Corpo({ ano, mes, aoFechar }: { ano: number; mes: number; aoFechar: () => void }) {
  const [ordem, setOrdem] = useState<string[]>([...CODIGOS_PADRAO])
  const [marcadas, setMarcadas] = useState<ReadonlySet<string>>(() => new Set(CODIGOS_PADRAO))
  const [formato, setFormato] = useState<FormatoDasHoras>('MINUTOS')
  const [separador, setSeparador] = useState<Separador>('PONTO_E_VIRGULA')
  const [baixando, setBaixando] = useState(false)
  const [erro, setErro] = useState<unknown>(null)

  const escolhidas = ordem.filter((c) => marcadas.has(c))
  const nenhuma = escolhidas.length === 0
  const erroDasColunas = erro instanceof ErroDeApi ? erro.errors.colunas?.join(' ') : undefined
  const periodo = `${ano}-${String(mes).padStart(2, '0')}`

  function mover(codigo: string, passo: -1 | 1) {
    setOrdem((atual) => {
      const de = atual.indexOf(codigo)
      const para = de + passo
      if (para < 0 || para >= atual.length) return atual
      const nova = [...atual]
      nova.splice(de, 1)
      nova.splice(para, 0, codigo)
      return nova
    })
  }

  async function baixar() {
    if (baixando || nenhuma) return
    setBaixando(true)
    setErro(null)
    const padrao = escolhidas.join(',') === CODIGOS_PADRAO.join(',')
    try {
      await baixarExportacao(
        clienteHttp.GET('/api/rh/ponto/folha/exportacao', {
          params: {
            query: {
              ano,
              mes,
              ...(padrao ? {} : { colunas: escolhidas.join(',') }),
              ...(formato === 'MINUTOS' ? {} : { formatoHoras: formato }),
              ...(separador === 'PONTO_E_VIRGULA' ? {} : { separador }),
            },
          },
          parseAs: 'blob',
        }),
        `ponto-${periodo}.csv`,
      )
      aoFechar()
    } catch (e) {
      setErro(e)
    } finally {
      setBaixando(false)
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <p className="text-corpo text-texto-suave">
        Uma linha por pessoa que estava ativa em{' '}
        <strong className="text-texto">{formatarMesAno(ano, mes)}</strong>.
      </p>

      <fieldset className="flex flex-col gap-2">
        <legend className="text-tabela font-semibold text-texto">
          Colunas, na ordem do arquivo
        </legend>
        <ul className="flex flex-col gap-1">
          {ordem.map((codigo, i) => {
            const coluna = COLUNAS.find((c) => c.codigo === codigo)!
            return (
              <li
                key={codigo}
                className="flex items-center justify-between gap-3 rounded-campo border border-borda px-3 py-1.5"
              >
                <CaixaDeSelecao
                  marcada={marcadas.has(codigo)}
                  aoMudar={(marcada) =>
                    setMarcadas((atual) => {
                      const nova = new Set(atual)
                      if (marcada) nova.add(codigo)
                      else nova.delete(codigo)
                      return nova
                    })
                  }
                  rotulo={coluna.rotulo}
                />
                <span className="flex gap-1">
                  <Botao
                    variante="texto"
                    tamanho="pequeno"
                    aria-label={`Subir a coluna ${coluna.rotulo}`}
                    disabled={i === 0}
                    onClick={() => mover(codigo, -1)}
                  >
                    <ArrowUp size={16} strokeWidth={1.75} aria-hidden="true" />
                  </Botao>
                  <Botao
                    variante="texto"
                    tamanho="pequeno"
                    aria-label={`Descer a coluna ${coluna.rotulo}`}
                    disabled={i === ordem.length - 1}
                    onClick={() => mover(codigo, 1)}
                  >
                    <ArrowDown size={16} strokeWidth={1.75} aria-hidden="true" />
                  </Botao>
                </span>
              </li>
            )
          })}
        </ul>
        {nenhuma && <p className="text-pequeno text-erro-texto">Escolha ao menos uma coluna.</p>}
        {erroDasColunas && (
          <p role="alert" className="text-pequeno text-erro-texto">
            {erroDasColunas}
          </p>
        )}
      </fieldset>

      <OpcoesEmCartao
        rotulo="Como saem as horas"
        opcoes={FORMATOS}
        valor={formato}
        aoMudar={setFormato}
        colunas={3}
      />
      <OpcoesEmCartao
        rotulo="Separador das colunas"
        opcoes={SEPARADORES}
        valor={separador}
        aoMudar={setSeparador}
      />

      {erro !== null && !erroDasColunas && <Aviso tom="erro">{mensagemDeErro(erro)}</Aviso>}
      <div className="flex justify-end gap-3">
        <Botao variante="secundario" onClick={aoFechar} disabled={baixando}>
          Cancelar
        </Botao>
        <Botao onClick={() => void baixar()} carregando={baixando} disabled={nenhuma}>
          Baixar arquivo
        </Botao>
      </div>
    </div>
  )
}
