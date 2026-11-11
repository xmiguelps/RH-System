import { useState, type ReactNode } from 'react'
import { useParams } from 'react-router'
import { $apiPublica } from '../../shared/api/apiPublica'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { formatarDiaMesDaData } from '../../shared/formato/datas'
import { Aviso } from '../../shared/ui/Aviso'
import { Botao } from '../../shared/ui/Botao'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Markdown } from '../../shared/ui/Markdown'
import { Progresso } from '../../shared/ui/Progresso'
import { SeletorDeArquivo } from '../../shared/ui/SeletorDeArquivo'
import { CampoDaPergunta } from './CampoDaPergunta'
import { responderFormularioPublico } from './enviarRespostas'
import { estaRespondida, montarEnvio, type RespostaLocal } from './respostasLocais'
import { ARQUIVOS_ACEITOS } from './rotulosDosFormularios'

type Pagina = Esquema<'FormularioPaginaPublica'>
type Dados = {
  nome: string
  email: string
  telefone: string
  curriculo: File | null
  aceite: boolean
}
type ErrosDosDados = Partial<Record<'nome' | 'email' | 'telefone' | 'curriculo' | 'aceite', string>>

const TELEFONE = /^[0-9 ()+-]{8,30}$/
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

/** As mensagens da nota (404, 422, 409 e 429); o resto mostra o `detail`. */
function mensagemDaPagina(erro: unknown): string {
  if (!(erro instanceof ErroDeApi)) return mensagemDeErro(erro)
  if (erro.status === 404) return 'Este link não existe ou não está mais disponível.'
  if (erro.status === 429) return 'Muitas tentativas. Tente de novo em alguns minutos.'
  if (erro.codigo === 'FORMULARIO_AINDA_NAO_ABERTO')
    return 'Este formulário ainda não está aberto. Tente de novo a partir da data de publicação.'
  if (erro.codigo === 'FORMULARIO_ENCERRADO')
    return 'Este formulário não está mais aceitando respostas.'
  if (erro.codigo === 'EMPRESA_NAO_CONFIGURADA')
    return 'Este formulário ainda não está disponível. Fale com o RH.'
  if (erro.status === 409) return erro.detail ?? 'Já recebemos uma candidatura com este e-mail.'
  return mensagemDeErro(erro)
}

function validarDados(d: Dados): ErrosDosDados {
  const e: ErrosDosDados = {}
  if (!d.nome.trim()) e.nome = 'Escreva o seu nome.'
  if (!EMAIL.test(d.email.trim())) e.email = 'Escreva um e-mail válido.'
  if (d.telefone.trim() && !TELEFONE.test(d.telefone.trim()))
    e.telefone = 'Use só números, espaço, parênteses, + e -, de 8 a 30 caracteres.'
  if (!d.aceite) e.aceite = 'Para começar, aceite o aviso de privacidade.'
  return e
}

function Identificacao({
  pagina,
  dados,
  aoMudar,
  erros,
  aoComecar,
}: {
  pagina: Pagina
  dados: Dados
  aoMudar: (d: Dados) => void
  erros: ErrosDosDados
  aoComecar: () => void
}) {
  return (
    <div className="flex flex-col gap-5">
      <Campo rotulo="Nome" erro={erros.nome}>
        <Entrada
          maxLength={150}
          value={dados.nome}
          onChange={(e) => aoMudar({ ...dados, nome: e.target.value })}
        />
      </Campo>
      <Campo rotulo="E-mail" erro={erros.email}>
        <Entrada
          type="email"
          value={dados.email}
          onChange={(e) => aoMudar({ ...dados, email: e.target.value })}
        />
      </Campo>
      <Campo rotulo="Telefone (opcional)" erro={erros.telefone}>
        <Entrada
          type="tel"
          maxLength={30}
          value={dados.telefone}
          onChange={(e) => aoMudar({ ...dados, telefone: e.target.value })}
        />
      </Campo>
      <div className="flex flex-col gap-2">
        <span className="text-tabela font-semibold text-texto">Currículo (opcional)</span>
        <SeletorDeArquivo
          arquivo={dados.curriculo}
          aoMudar={(curriculo) => aoMudar({ ...dados, curriculo })}
          tiposAceitos={ARQUIVOS_ACEITOS}
          tamanhoMaximoMb={5}
          titulo="Arraste o seu currículo aqui"
          textoDeAjuda="PDF ou Word, até 5 MB"
          erro={erros.curriculo}
        />
      </div>
      <section
        aria-label="Aviso de privacidade"
        className="flex flex-col gap-2 rounded-cartao bg-superficie-suave p-4"
      >
        <h2 className="text-tabela font-semibold text-texto">Aviso de privacidade</h2>
        <Markdown texto={pagina.avisoDePrivacidade} className="text-tabela text-texto" />
        <p className="text-pequeno text-texto-suave">
          Os seus dados ficam guardados por {pagina.retencaoDias} dias.
        </p>
      </section>
      <CaixaDeSelecao
        marcada={dados.aceite}
        aoMudar={(aceite) => aoMudar({ ...dados, aceite })}
        rotulo="Li e concordo com o aviso de privacidade"
      />
      {erros.aceite && <p className="text-pequeno text-erro-texto">{erros.aceite}</p>}
      <div className="flex justify-end">
        <Botao onClick={aoComecar} disabled={!dados.aceite}>
          Começar
        </Botao>
      </div>
    </div>
  )
}

/** A página pública do candidato (sem print): /formulario/:token, sem login, sem menu, sem sessão. */
export function PaginaFormularioPublico() {
  const token = useParams().token ?? ''
  const consulta = $apiPublica.useQuery(
    'get',
    '/api/publico/formularios/{token}',
    { params: { path: { token } } },
    { retry: false },
  )
  const [etapa, setEtapa] = useState<'dados' | 'perguntas'>('dados')
  const [dados, setDados] = useState<Dados>({
    nome: '',
    email: '',
    telefone: '',
    curriculo: null,
    aceite: false,
  })
  const [errosDosDados, setErrosDosDados] = useState<ErrosDosDados>({})
  const [respostas, setRespostas] = useState<Record<number, RespostaLocal>>({})
  const [indice, setIndice] = useState(0)
  const [erroDaPergunta, setErroDaPergunta] = useState<string | null>(null)
  const [erroGeral, setErroGeral] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [mensagemFinal, setMensagemFinal] = useState<string | null>(null)
  const pagina = consulta.data

  const moldura = (conteudo: ReactNode) => (
    <main className="min-h-screen bg-pagina px-4 py-8">
      <div className="mx-auto flex w-full max-w-[640px] flex-col gap-6">
        <header className="flex items-center gap-3">
          <span
            aria-hidden="true"
            className="flex h-10 w-10 items-center justify-center rounded-campo bg-primaria font-bold text-pagina"
          >
            {(pagina?.empresaNome ?? '?').charAt(0)}
          </span>
          <span className="text-corpo font-semibold text-texto">
            {pagina?.empresaNome ?? 'Formulário'}
          </span>
        </header>
        {conteudo}
      </div>
    </main>
  )

  if (consulta.error) return moldura(<Aviso tom="erro">{mensagemDaPagina(consulta.error)}</Aviso>)
  if (!pagina)
    return moldura(
      <div role="status" aria-label="Carregando o formulário" className="flex flex-col gap-3">
        <Esqueleto className="h-10 w-full" />
        <Esqueleto className="h-60 w-full" />
      </div>,
    )
  if (mensagemFinal)
    return moldura(
      <section
        aria-label="Candidatura enviada"
        className="rounded-cartao border border-borda bg-superficie p-6"
      >
        <p className="text-corpo text-texto">{mensagemFinal}</p>
      </section>,
    )

  const perguntas = [...pagina.perguntas].sort((a, b) => a.ordem - b.ordem)
  const atual = perguntas[indice]
  const ultima = indice === perguntas.length - 1

  async function enviar() {
    if (enviando || !pagina) return
    setEnviando(true)
    setErroGeral(null)
    try {
      const recibo = await responderFormularioPublico(
        token,
        {
          nome: dados.nome.trim(),
          email: dados.email.trim(),
          telefone: dados.telefone.trim() || undefined,
          curriculo: dados.curriculo,
          aceitouAvisoDePrivacidade: dados.aceite,
        },
        montarEnvio(perguntas, respostas),
      )
      setMensagemFinal(recibo.mensagem)
    } catch (erro) {
      if (erro instanceof ErroDeApi && erro.status === 400) {
        const e = erro.errors
        const doCandidato: ErrosDosDados = {
          nome: e.nome?.join(' '),
          email: e.email?.join(' '),
          telefone: e.telefone?.join(' '),
          curriculo: e.curriculo?.join(' '),
        }
        if (Object.values(doCandidato).some(Boolean)) {
          setErrosDosDados(doCandidato)
          setEtapa('dados')
          return
        }
        setErroGeral(Object.values(e).flat().join(' ') || mensagemDaPagina(erro))
      } else setErroGeral(mensagemDaPagina(erro))
    } finally {
      setEnviando(false)
    }
  }

  function seguir() {
    if (!atual) return void enviar()
    if (atual.obrigatoria && !estaRespondida(atual, respostas[atual.id])) {
      setErroDaPergunta('Responda esta pergunta para seguir.')
      return
    }
    setErroDaPergunta(null)
    if (ultima) void enviar()
    else setIndice(indice + 1)
  }

  return moldura(
    <>
      <section className="flex flex-col gap-2">
        <h1 className="text-t1 font-bold text-texto">{pagina.titulo}</h1>
        {pagina.descricao && (
          <Markdown texto={pagina.descricao} className="text-corpo text-texto-suave" />
        )}
        {pagina.prazo && (
          <p className="text-pequeno text-texto-suave">
            Aceita respostas até {formatarDiaMesDaData(pagina.prazo)}
          </p>
        )}
      </section>
      <div className="rounded-cartao border border-borda bg-superficie p-6">
        {etapa === 'dados' ? (
          <Identificacao
            pagina={pagina}
            dados={dados}
            aoMudar={setDados}
            erros={errosDosDados}
            aoComecar={() => {
              const e = validarDados(dados)
              setErrosDosDados(e)
              if (Object.keys(e).length === 0) setEtapa('perguntas')
            }}
          />
        ) : atual ? (
          <div className="flex flex-col gap-5">
            <div className="flex justify-between text-pequeno text-texto-suave">
              <span>
                Pergunta {indice + 1} de {perguntas.length}
              </span>
              <span>{Math.round(((indice + 1) / perguntas.length) * 100)}%</span>
            </div>
            <Progresso valor={indice + 1} maximo={perguntas.length} rotulo="Progresso" />
            <CampoDaPergunta
              key={atual.id}
              pergunta={atual}
              valor={respostas[atual.id] ?? {}}
              aoMudar={(v) => setRespostas({ ...respostas, [atual.id]: v })}
              erro={erroDaPergunta}
            />
            {erroGeral && <Aviso tom="erro">{erroGeral}</Aviso>}
            <div className="flex justify-between gap-3">
              <Botao
                variante="secundario"
                onClick={() => (indice === 0 ? setEtapa('dados') : setIndice(indice - 1))}
              >
                Voltar
              </Botao>
              <Botao onClick={seguir} carregando={enviando}>
                {ultima ? 'Enviar candidatura' : 'Próxima'}
              </Botao>
            </div>
          </div>
        ) : (
          <div className="flex flex-col gap-3">
            {erroGeral && <Aviso tom="erro">{erroGeral}</Aviso>}
            <Botao onClick={() => void enviar()} carregando={enviando}>
              Enviar candidatura
            </Botao>
          </div>
        )}
      </div>
    </>,
  )
}
