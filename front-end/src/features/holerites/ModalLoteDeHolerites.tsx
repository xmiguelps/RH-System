import { useQueryClient } from '@tanstack/react-query'
import { ArrowUpFromLine } from 'lucide-react'
import { useId, useRef, useState } from 'react'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { validarArquivo } from '../../shared/arquivos/validarArquivo'
import { formatarDataDoInstante, hojeEmSaoPaulo } from '../../shared/formato/datas'
import { plural } from '../../shared/formato/texto'
import { Aviso } from '../../shared/ui/Aviso'
import { Botao } from '../../shared/ui/Botao'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { ComboDeBusca } from '../../shared/ui/ComboDeBusca'
import { Janela } from '../../shared/ui/Janela'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { useBuscaDeColaborador } from '../ponto/useBuscaDeColaborador'
import { CampoQuandoPublicar, CamposDaCompetencia } from './CamposDoHolerite'
import { enviarHoleritesEmLote } from './enviarHolerite'
import {
  camposDeQuandoPublicar,
  PDF,
  TAMANHO_MAXIMO_MB,
  type QuandoPublicar,
} from './rotulosDosHolerites'

type Tipo = Esquema<'HoleriteTipo'>
type Resultado = Esquema<'HoleriteLoteResultado'>
type Pessoa = { id: number; nome: string }
type Linha = { chave: number; arquivo: File; pessoa: Pessoa | null }

const MAXIMO_DE_ARQUIVOS = 200

function tamanhoLegivel(bytes: number): string {
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1).replace('.', ',')} MB`
}

function problemaDaLinha(linha: Linha, todas: readonly Linha[]): string | null {
  const doArquivo = validarArquivo(linha.arquivo, {
    tiposAceitos: PDF,
    tamanhoMaximoMb: TAMANHO_MAXIMO_MB,
  })
  if (doArquivo)
    return doArquivo === 'Esse tipo de arquivo não é aceito.' ? 'Não é PDF.' : doArquivo
  if (todas.filter((l) => l.arquivo.name === linha.arquivo.name).length > 1)
    return 'Nome de arquivo repetido.'
  return null
}

function SeletorDaPessoa({
  linha,
  aoMudar,
}: {
  linha: Linha
  aoMudar: (pessoa: Pessoa | null) => void
}) {
  const busca = useBuscaDeColaborador(linha.pessoa ?? undefined)
  return (
    <ComboDeBusca
      rotulo={`Colaborador de ${linha.arquivo.name}`}
      placeholder="Buscar colaborador"
      opcoes={busca.opcoes}
      valor={linha.pessoa ? String(linha.pessoa.id) : null}
      aoDigitar={busca.aoDigitar}
      carregando={busca.carregando}
      aoMudar={(valor) => {
        const opcao = busca.opcoes.find((o) => o.valor === valor)
        aoMudar(opcao ? { id: Number(opcao.valor), nome: opcao.rotulo } : null)
      }}
    />
  )
}

function ResultadoDoLote({
  resultado,
  nomes,
}: {
  resultado: Resultado
  nomes: ReadonlyMap<number, string>
}) {
  const colunas: readonly ColunaDaTabela<Resultado['itens'][number]>[] = [
    { chave: 'arquivo', titulo: 'Arquivo', celula: (i) => i.nomeDoArquivo },
    {
      chave: 'pessoa',
      titulo: 'Colaborador',
      celula: (i) => nomes.get(i.funcionarioId) ?? `Colaborador ${i.funcionarioId}`,
    },
    {
      chave: 'resultado',
      titulo: 'Resultado',
      celula: (i) =>
        i.sucesso && i.holerite ? (
          i.holerite.situacao === 'AGENDADO' ? (
            `Agendado para ${formatarDataDoInstante(i.holerite.publicacaoEmUtc)}`
          ) : (
            'Publicado'
          )
        ) : (
          <span className="flex flex-col items-start gap-1">
            <span className="text-erro-texto">{i.mensagemDoErro ?? 'Não foi enviado.'}</span>
            {i.codigoDoErro === 'HOLERITE_JA_EXISTE' && (
              <BotaoLink
                variante="texto"
                tamanho="pequeno"
                to={`/rh/colaboradores/${i.funcionarioId}?aba=holerites`}
              >
                Abrir a ficha
              </BotaoLink>
            )}
          </span>
        ),
    },
  ]
  return (
    <div className="flex flex-col gap-4">
      <p className="text-corpo font-semibold text-texto">
        {resultado.enviados} {plural(resultado.enviados, 'enviado', 'enviados')},{' '}
        {resultado.comErro} com erro
      </p>
      {resultado.itens.some((i) => i.codigoDoErro === 'HOLERITE_JA_EXISTE') && (
        <p className="text-tabela text-texto-suave">
          O lote não cria versões: para corrigir um holerite que já existe, abra a ficha e use
          "Substituir".
        </p>
      )}
      <Tabela
        rotulo="Resultado do envio"
        colunas={colunas}
        linhas={resultado.itens}
        chaveDaLinha={(i) => i.nomeDoArquivo}
      />
    </div>
  )
}

/** "Enviar holerites em lote" (sem print; dúvida D-F4-4): um PDF por pessoa, o RH liga cada um. */
export function ModalLoteDeHolerites({ aoFechar }: { aoFechar: () => void }) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const idDaEntrada = useId()
  const entrada = useRef<HTMLInputElement>(null)
  const proximaChave = useRef(1)
  const hoje = hojeEmSaoPaulo()
  const [mes, setMes] = useState(String(Number(hoje.slice(5, 7))))
  const [ano, setAno] = useState(hoje.slice(0, 4))
  const [tipo, setTipo] = useState<Tipo>('MENSAL')
  const [quando, setQuando] = useState<QuandoPublicar>('AGORA')
  const [data, setData] = useState('')
  const [linhas, setLinhas] = useState<Linha[]>([])
  const [erroDaData, setErroDaData] = useState<string | undefined>()
  const [erroGeral, setErroGeral] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [resultado, setResultado] = useState<Resultado | null>(null)
  const [nomes, setNomes] = useState<ReadonlyMap<number, string>>(new Map())

  function acrescentar(arquivos: FileList | null) {
    if (!arquivos) return
    const novos = Array.from(arquivos).map((arquivo) => ({
      chave: proximaChave.current++,
      arquivo,
      pessoa: null,
    }))
    setLinhas((atuais) => [...atuais, ...novos])
  }

  const problemas = linhas.map((l) => problemaDaLinha(l, linhas))
  const semPessoa = linhas.some((l) => l.pessoa === null)
  const demais = linhas.length > MAXIMO_DE_ARQUIVOS
  const pronto = linhas.length > 0 && !semPessoa && !demais && problemas.every((p) => !p)
  const rotuloDoEnvio = `${quando === 'AGORA' ? 'Publicar' : 'Agendar'} ${linhas.length} ${plural(linhas.length, 'holerite', 'holerites')}`

  async function enviar() {
    if (enviando || !pronto) return
    setErroGeral(null)
    setErroDaData(undefined)
    if (quando === 'DATA' && (!data || data < hoje)) {
      setErroDaData(data ? 'Escolha hoje ou um dia futuro.' : 'Escolha o dia da publicação.')
      return
    }
    setEnviando(true)
    try {
      const resposta = await enviarHoleritesEmLote({
        mes: Number(mes),
        ano: Number(ano),
        tipo,
        ...camposDeQuandoPublicar(quando, data),
        itens: linhas.map((l) => ({ funcionarioId: l.pessoa!.id, arquivo: l.arquivo })),
      })
      setNomes(new Map(linhas.map((l) => [l.pessoa!.id, l.pessoa!.nome])))
      setResultado(resposta)
      if (resposta.enviados > 0) notificacao.sucesso('Lote processado.')
      await invalidarRota(queryClient, '/api/rh/funcionarios/{funcionarioId}/holerites')
    } catch (erro) {
      setErroGeral(
        erro instanceof ErroDeApi && Object.keys(erro.errors).length > 0
          ? Object.values(erro.errors).flat().join(' ')
          : mensagemDeErro(erro),
      )
    } finally {
      setEnviando(false)
    }
  }

  const colunas: readonly ColunaDaTabela<Linha>[] = [
    {
      chave: 'arquivo',
      titulo: 'Arquivo',
      celula: (l) => {
        const problema = problemas[linhas.indexOf(l)]
        return (
          <span className="flex flex-col">
            <span className="text-texto">{l.arquivo.name}</span>
            <span className="text-pequeno text-texto-suave">{tamanhoLegivel(l.arquivo.size)}</span>
            {problema && <span className="text-pequeno text-erro-texto">{problema}</span>}
          </span>
        )
      },
    },
    {
      chave: 'pessoa',
      titulo: 'Colaborador',
      celula: (l) => (
        <SeletorDaPessoa
          linha={l}
          aoMudar={(pessoa) =>
            setLinhas((atuais) => atuais.map((a) => (a.chave === l.chave ? { ...a, pessoa } : a)))
          }
        />
      ),
    },
    {
      chave: 'remover',
      titulo: 'Remover',
      alinhamento: 'direita',
      celula: (l) => (
        <Botao
          variante="texto"
          tamanho="pequeno"
          aria-label={`Remover ${l.arquivo.name}`}
          onClick={() => setLinhas((atuais) => atuais.filter((a) => a.chave !== l.chave))}
        >
          Remover
        </Botao>
      ),
    },
  ]

  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo="Enviar holerites em lote"
      tamanho="extra"
      rodape={
        resultado ? (
          <>
            <Botao
              variante="secundario"
              onClick={() => {
                setResultado(null)
                setLinhas([])
              }}
            >
              Enviar mais
            </Botao>
            <Botao onClick={aoFechar}>Fechar</Botao>
          </>
        ) : (
          <>
            <Botao variante="secundario" onClick={aoFechar}>
              Cancelar
            </Botao>
            <Botao onClick={() => void enviar()} disabled={!pronto} carregando={enviando}>
              {enviando ? `Enviando ${linhas.length} arquivos...` : rotuloDoEnvio}
            </Botao>
          </>
        )
      }
    >
      {resultado ? (
        <ResultadoDoLote resultado={resultado} nomes={nomes} />
      ) : (
        <div className="flex flex-col gap-5">
          <CamposDaCompetencia
            mes={mes}
            ano={ano}
            tipo={tipo}
            aoMudar={(campo, valor) => {
              if (campo === 'mes') setMes(valor)
              else if (campo === 'ano') setAno(valor)
              else setTipo(valor as Tipo)
            }}
          />
          <CampoQuandoPublicar
            quando={quando}
            aoMudarQuando={setQuando}
            data={data}
            aoMudarData={setData}
            erroDaData={erroDaData}
          />
          <div
            onDragOver={(e) => e.preventDefault()}
            onDrop={(e) => {
              e.preventDefault()
              acrescentar(e.dataTransfer.files)
            }}
            className="flex flex-col items-center gap-2 rounded-cartao border border-dashed border-tracejado bg-[#fafcfb] px-6 py-8 text-center"
          >
            <ArrowUpFromLine
              size={24}
              strokeWidth={1.75}
              aria-hidden="true"
              className="text-primaria-media"
            />
            <p className="text-corpo font-semibold text-texto">Arraste os PDFs aqui</p>
            <p className="text-pequeno text-texto-suave">
              Só PDF, até 5 MB cada, até 200 arquivos e 100 MB no total
            </p>
            <input
              ref={entrada}
              id={idDaEntrada}
              type="file"
              multiple
              aria-label="Escolher arquivos"
              accept="application/pdf"
              className="sr-only"
              onChange={(e) => {
                acrescentar(e.target.files)
                e.target.value = ''
              }}
            />
            <Botao variante="secundario" tamanho="pequeno" onClick={() => entrada.current?.click()}>
              Escolher arquivos
            </Botao>
          </div>
          {demais && <Aviso tom="erro">Passou de 200 arquivos. Remova alguns para enviar.</Aviso>}
          {linhas.length === 0 ? (
            <p className="text-corpo text-texto-suave">Adicione os PDFs para começar.</p>
          ) : (
            <Tabela
              rotulo="Arquivos do lote"
              colunas={colunas}
              linhas={linhas}
              chaveDaLinha={(l) => l.chave}
            />
          )}
          {semPessoa && linhas.length > 0 && (
            <p className="text-pequeno text-texto-suave">
              Escolha o colaborador de cada arquivo para enviar.
            </p>
          )}
          {erroGeral && <Aviso tom="erro">{erroGeral}</Aviso>}
        </div>
      )}
    </Janela>
  )
}
