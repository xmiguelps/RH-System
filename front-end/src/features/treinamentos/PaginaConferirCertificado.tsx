import { useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { $apiPublica } from '../../shared/api/apiPublica'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import { formatarData } from '../../shared/formato/datas'
import { Aviso } from '../../shared/ui/Aviso'
import { Botao } from '../../shared/ui/Botao'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { Esqueleto } from '../../shared/ui/Esqueleto'

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

function mensagemDaConferencia(erro: unknown): string {
  if (erro instanceof ErroDeApi && erro.status === 404)
    return erro.detail ?? 'Não encontramos um certificado com este código.'
  if (erro instanceof ErroDeApi && erro.status === 429)
    return 'Muitas consultas. Tente de novo em alguns minutos.'
  if (erro instanceof ErroDeApi && erro.status === 400)
    return 'Digite o código completo, como está no certificado.'
  return mensagemDeErro(erro)
}

function Resultado({ codigo }: { codigo: string }) {
  // Sem sessão: o cliente público nunca manda Authorization nem renova.
  const consulta = $apiPublica.useQuery(
    'get',
    '/api/publico/certificados/{codigoValidacao}',
    { params: { path: { codigoValidacao: codigo } } },
    { retry: false },
  )
  const c = consulta.data
  if (consulta.error)
    return (
      <section aria-label="Resultado" className="flex flex-col gap-2">
        <h2 className="text-t3 font-bold text-texto">
          {consulta.error instanceof ErroDeApi && consulta.error.status === 404
            ? 'Certificado não encontrado'
            : 'Não foi possível conferir'}
        </h2>
        <Aviso tom="erro">{mensagemDaConferencia(consulta.error)}</Aviso>
      </section>
    )
  if (!c) return <Esqueleto className="h-32 w-full" />
  return (
    <section
      aria-label="Resultado"
      className="flex flex-col gap-3 rounded-cartao bg-sucesso-fundo p-5"
    >
      <h2 className="text-t3 font-bold text-sucesso-texto">Certificado válido</h2>
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-corpo">
        <dt className="text-texto-suave">Nome</dt>
        <dd className="text-texto">{c.titularNome}</dd>
        <dt className="text-texto-suave">Treinamento</dt>
        <dd className="text-texto">{c.treinamentoTitulo}</dd>
        <dt className="text-texto-suave">Emitido em</dt>
        <dd className="text-texto">{formatarData(c.emitidoEm)}</dd>
        <dt className="text-texto-suave">Código</dt>
        <dd className="break-all text-texto">{c.codigoValidacao}</dd>
      </dl>
    </section>
  )
}

/** Conferir um certificado (público, sem print): /certificado e /certificado/:codigo. */
export function PaginaConferirCertificado() {
  const navegar = useNavigate()
  const codigoDaUrl = useParams().codigo ?? ''
  const [digitado, setDigitado] = useState(codigoDaUrl)
  const [erro, setErro] = useState<string | null>(null)
  const valido = UUID.test(codigoDaUrl)

  function conferir() {
    const codigo = digitado.trim()
    if (!UUID.test(codigo)) {
      setErro('Digite o código completo, como está no certificado.')
      return
    }
    setErro(null)
    navegar(`/certificado/${codigo.toLowerCase()}`)
  }

  return (
    <main className="min-h-screen bg-pagina px-4 py-10">
      <div className="mx-auto flex w-full max-w-[560px] flex-col gap-6 rounded-cartao border border-borda bg-superficie p-7">
        <header className="flex flex-col gap-1">
          <h1 className="text-t2 font-bold text-texto">Conferir certificado</h1>
          <p className="text-corpo text-texto-suave">Digite o código impresso no certificado.</p>
        </header>
        <form
          noValidate
          className="flex flex-col gap-3"
          onSubmit={(e) => {
            e.preventDefault()
            conferir()
          }}
        >
          <Campo rotulo="Código de validação" erro={erro}>
            <Entrada
              value={digitado}
              onChange={(e) => setDigitado(e.target.value)}
              autoComplete="off"
            />
          </Campo>
          <div>
            <Botao type="submit">Conferir</Botao>
          </div>
        </form>
        {codigoDaUrl && !valido && (
          <Aviso tom="erro">Digite o código completo, como está no certificado.</Aviso>
        )}
        {valido && <Resultado key={codigoDaUrl} codigo={codigoDaUrl} />}
      </div>
    </main>
  )
}
