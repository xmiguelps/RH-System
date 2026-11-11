import { clienteHttp } from '../../shared/api/api'
import { clienteHttpPublico } from '../../shared/api/apiPublica'
import type { ClienteHttp } from '../../shared/api/clienteApi'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import type { Esquema } from '../../shared/api/tipos'

type Item = Esquema<'RespostaFormularioItem'>

function semResposta(): never {
  throw new ErroDeApi(500, 'Resposta inesperada da API.')
}

/** A parte `respostas` vai como JSON (`application/json`, como o `encoding` do contrato). */
function partesDasRespostas(
  formulario: FormData,
  respostas: readonly Item[],
  arquivos: readonly File[],
) {
  formulario.append(
    'respostas',
    new Blob([JSON.stringify(respostas)], { type: 'application/json' }),
  )
  for (const arquivo of arquivos) formulario.append('arquivos', arquivo)
}

/** Responder (todos os perfis): `respostas`, `arquivos` e, nas avaliações, `avaliadoId`. */
export async function responderFormulario(
  formularioId: number,
  envio: { respostas: readonly Item[]; arquivos: readonly File[]; avaliadoId?: number },
  cliente: ClienteHttp = clienteHttp,
): Promise<Esquema<'RespostaFormularioRecibo'>> {
  const corpo: Esquema<'RespostaFormularioEnvio'> = {
    ...(envio.avaliadoId !== undefined ? { avaliadoId: envio.avaliadoId } : {}),
    respostas: [...envio.respostas],
    ...(envio.arquivos.length ? { arquivos: envio.arquivos.map((a) => a.name) } : {}),
  }
  const { data } = await cliente.POST('/api/eu/formularios/{formularioId}/respostas', {
    params: { path: { formularioId } },
    body: corpo,
    bodySerializer: () => {
      const formulario = new FormData()
      if (envio.avaliadoId !== undefined) formulario.append('avaliadoId', String(envio.avaliadoId))
      partesDasRespostas(formulario, envio.respostas, envio.arquivos)
      return formulario
    },
  })
  return data ?? semResposta()
}

export type DadosDoCandidato = {
  nome: string
  email: string
  telefone?: string
  curriculo?: File | null
  aceitouAvisoDePrivacidade: boolean
}

/** Página pública: sem sessão (cliente público, sem Authorization nem renovação). */
export async function responderFormularioPublico(
  token: string,
  candidato: DadosDoCandidato,
  envio: { respostas: readonly Item[]; arquivos: readonly File[] },
  cliente: ClienteHttp = clienteHttpPublico,
): Promise<Esquema<'CandidatoRecibo'>> {
  const corpo: Esquema<'CandidatoEnvio'> = {
    nome: candidato.nome,
    email: candidato.email,
    ...(candidato.telefone ? { telefone: candidato.telefone } : {}),
    ...(candidato.curriculo ? { curriculo: candidato.curriculo.name } : {}),
    aceitouAvisoDePrivacidade: candidato.aceitouAvisoDePrivacidade,
    respostas: [...envio.respostas],
    ...(envio.arquivos.length ? { arquivos: envio.arquivos.map((a) => a.name) } : {}),
  }
  const { data } = await cliente.POST('/api/publico/formularios/{token}/respostas', {
    params: { path: { token } },
    body: corpo,
    bodySerializer: () => {
      const formulario = new FormData()
      formulario.append('nome', candidato.nome)
      formulario.append('email', candidato.email)
      if (candidato.telefone) formulario.append('telefone', candidato.telefone)
      if (candidato.curriculo) formulario.append('curriculo', candidato.curriculo)
      formulario.append('aceitouAvisoDePrivacidade', String(candidato.aceitouAvisoDePrivacidade))
      partesDasRespostas(formulario, envio.respostas, envio.arquivos)
      return formulario
    },
  })
  return data ?? semResposta()
}
