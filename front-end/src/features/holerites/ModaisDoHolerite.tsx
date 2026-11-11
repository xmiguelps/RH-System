import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { formatarDataDoInstante, hojeEmSaoPaulo } from '../../shared/formato/datas'
import { primeiroNome } from '../../shared/formato/texto'
import { Aviso } from '../../shared/ui/Aviso'
import { Botao } from '../../shared/ui/Botao'
import { Janela } from '../../shared/ui/Janela'
import { SeletorDeArquivo } from '../../shared/ui/SeletorDeArquivo'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { CampoQuandoPublicar, CamposDaCompetencia } from './CamposDoHolerite'
import { enviarHolerite, substituirHolerite } from './enviarHolerite'
import {
  camposDeQuandoPublicar,
  PDF,
  referenciaDoHolerite,
  TAMANHO_MAXIMO_MB,
  type QuandoPublicar,
} from './rotulosDosHolerites'

type HoleriteRh = Esquema<'HoleriteItemRh'>
type Tipo = Esquema<'HoleriteTipo'>

type Erros = { arquivo?: string; data?: string; geral?: string }

/** 400 com `errors` vai ao campo (arquivo, publicarEm); o resto vira a mensagem geral. */
function errosDaResposta(erro: unknown): Erros {
  if (!(erro instanceof ErroDeApi)) return { geral: mensagemDeErro(erro) }
  const saida: Erros = {}
  const sobras: string[] = []
  for (const [chave, mensagens] of Object.entries(erro.errors)) {
    const texto = mensagens.join(' ')
    if (chave.toLowerCase() === 'arquivo') saida.arquivo = texto
    else if (chave.toLowerCase() === 'publicarem') saida.data = texto
    else sobras.push(texto)
  }
  if (sobras.length) saida.geral = sobras.join(' ')
  if (!saida.arquivo && !saida.data && !saida.geral) saida.geral = mensagemDeErro(erro)
  return saida
}

function validar(arquivo: File | null, quando: QuandoPublicar, data: string): Erros {
  const erros: Erros = {}
  if (!arquivo) erros.arquivo = 'Escolha o PDF do holerite.'
  if (quando === 'DATA') {
    if (!data) erros.data = 'Escolha o dia da publicação.'
    else if (data < hojeEmSaoPaulo()) erros.data = 'Escolha hoje ou um dia futuro.'
  }
  return erros
}

function mensagemDoResultado(h: HoleriteRh): string {
  return h.situacao === 'AGENDADO'
    ? `Holerite agendado para ${formatarDataDoInstante(h.publicacaoEmUtc)}.`
    : 'Holerite publicado.'
}

function CampoDoArquivo({
  arquivo,
  aoMudar,
  erro,
}: {
  arquivo: File | null
  aoMudar: (a: File | null) => void
  erro?: string
}) {
  return (
    <SeletorDeArquivo
      arquivo={arquivo}
      aoMudar={aoMudar}
      tiposAceitos={PDF}
      tamanhoMaximoMb={TAMANHO_MAXIMO_MB}
      titulo="Arraste o PDF do holerite aqui"
      textoDeAjuda="Só PDF, até 5 MB"
      erro={erro}
    />
  )
}

/** Tela 08: "Enviar holerite". 409 HOLERITE_JA_EXISTE oferece "Substituir". */
export function ModalEnviarHolerite({
  funcionario,
  ultimoPublicado,
  holerites,
  aoFechar,
  aoSubstituir,
}: {
  funcionario: { id: number; nome: string }
  ultimoPublicado: HoleriteRh | null | undefined
  holerites: readonly HoleriteRh[]
  aoFechar: () => void
  aoSubstituir: (h: HoleriteRh) => void
}) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const hoje = hojeEmSaoPaulo()
  const [mes, setMes] = useState(String(Number(hoje.slice(5, 7))))
  const [ano, setAno] = useState(hoje.slice(0, 4))
  const [tipo, setTipo] = useState<Tipo>('MENSAL')
  const [arquivo, setArquivo] = useState<File | null>(null)
  const [quando, setQuando] = useState<QuandoPublicar>('AGORA')
  const [data, setData] = useState('')
  const [erros, setErros] = useState<Erros>({})
  const [jaExiste, setJaExiste] = useState<{ detalhe: string; holerite?: HoleriteRh } | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function publicar() {
    if (enviando) return
    const problemas = validar(arquivo, quando, data)
    setErros(problemas)
    setJaExiste(null)
    if (Object.keys(problemas).length > 0 || !arquivo) return
    setEnviando(true)
    try {
      const criado = await enviarHolerite(funcionario.id, {
        mes: Number(mes),
        ano: Number(ano),
        tipo,
        arquivo,
        ...camposDeQuandoPublicar(quando, data),
      })
      notificacao.sucesso(mensagemDoResultado(criado))
      await invalidarRota(queryClient, '/api/rh/funcionarios/{funcionarioId}/holerites')
      aoFechar()
    } catch (erro) {
      if (erro instanceof ErroDeApi && erro.status === 409) {
        setJaExiste({
          detalhe: mensagemDeErro(erro),
          holerite: holerites.find(
            (h) =>
              h.versaoAtual && h.mes === Number(mes) && h.ano === Number(ano) && h.tipo === tipo,
          ),
        })
      } else {
        setErros(errosDaResposta(erro))
      }
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo={`Enviar holerite de ${primeiroNome(funcionario.nome)}`}
      tamanho="media"
      rodape={
        <div className="flex w-full items-center justify-between gap-3">
          <span className="text-pequeno text-texto-suave">
            O arquivo fica guardado na ficha e na área de holerites do colaborador.
          </span>
          <div className="flex gap-3">
            <Botao variante="secundario" onClick={aoFechar}>
              Cancelar
            </Botao>
            <Botao onClick={() => void publicar()} carregando={enviando}>
              {enviando
                ? 'Enviando...'
                : quando === 'AGORA'
                  ? 'Publicar holerite'
                  : 'Agendar holerite'}
            </Botao>
          </div>
        </div>
      }
    >
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
        <CampoDoArquivo arquivo={arquivo} aoMudar={setArquivo} erro={erros.arquivo} />
        <CampoQuandoPublicar
          quando={quando}
          aoMudarQuando={setQuando}
          data={data}
          aoMudarData={setData}
          erroDaData={erros.data}
        />
        {ultimoPublicado && (
          <p className="text-tabela text-texto-suave">
            Último holerite enviado: {referenciaDoHolerite(ultimoPublicado)}, publicado em{' '}
            {formatarDataDoInstante(ultimoPublicado.publicacaoEmUtc)}.
          </p>
        )}
        {erros.geral && <Aviso tom="erro">{erros.geral}</Aviso>}
        {jaExiste && (
          <Aviso
            tom="alerta"
            acao={
              jaExiste.holerite ? (
                <Botao
                  variante="secundario"
                  tamanho="pequeno"
                  onClick={() => jaExiste.holerite && aoSubstituir(jaExiste.holerite)}
                >
                  Substituir
                </Botao>
              ) : undefined
            }
          >
            {jaExiste.detalhe}
          </Aviso>
        )}
      </div>
    </Janela>
  )
}

/** "Substituir holerite" (sem print): a nova versão; a anterior fica no histórico. */
export function ModalSubstituirHolerite({
  holerite,
  nome,
  aoFechar,
}: {
  holerite: HoleriteRh
  nome: string
  aoFechar: () => void
}) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const [arquivo, setArquivo] = useState<File | null>(null)
  const [quando, setQuando] = useState<QuandoPublicar>('AGORA')
  const [data, setData] = useState('')
  const [erros, setErros] = useState<Erros>({})
  const [enviando, setEnviando] = useState(false)

  async function substituir() {
    if (enviando) return
    const problemas = validar(arquivo, quando, data)
    setErros(problemas)
    if (Object.keys(problemas).length > 0 || !arquivo) return
    setEnviando(true)
    try {
      const nova = await substituirHolerite(holerite.id, {
        arquivo,
        ...camposDeQuandoPublicar(quando, data),
      })
      notificacao.sucesso(mensagemDoResultado(nova))
      await invalidarRota(queryClient, '/api/rh/funcionarios/{funcionarioId}/holerites')
      aoFechar()
    } catch (erro) {
      setErros(errosDaResposta(erro))
      if (erro instanceof ErroDeApi && erro.status === 409) {
        await invalidarRota(queryClient, '/api/rh/funcionarios/{funcionarioId}/holerites')
      }
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo={`Substituir holerite de ${referenciaDoHolerite(holerite)}`}
      descricao={`Para ${nome}.`}
      tamanho="media"
      rodape={
        <>
          <Botao variante="secundario" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao onClick={() => void substituir()} carregando={enviando}>
            Substituir holerite
          </Botao>
        </>
      }
    >
      <div className="flex flex-col gap-5">
        <p className="text-corpo text-texto-suave">
          O holerite anterior continua guardado no histórico. A pessoa passa a ver a nova versão
          assim que ela for publicada e recebe um e-mail.
        </p>
        <CampoDoArquivo arquivo={arquivo} aoMudar={setArquivo} erro={erros.arquivo} />
        <CampoQuandoPublicar
          quando={quando}
          aoMudarQuando={setQuando}
          data={data}
          aoMudarData={setData}
          erroDaData={erros.data}
        />
        {erros.geral && <Aviso tom="erro">{erros.geral}</Aviso>}
      </div>
    </Janela>
  )
}
