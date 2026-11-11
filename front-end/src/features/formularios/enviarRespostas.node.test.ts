// @vitest-environment node
import { http } from 'msw'
import { describe, expect, it } from 'vitest'
import { criarClienteHttp } from '../../shared/api/clienteApi'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { servidor } from '../../shared/mocks/servidor'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { responderFormulario, responderFormularioPublico } from './enviarRespostas'

const comSessao = criarClienteHttp({
  urlBase: 'https://api.exemplo.test',
  obterToken: () => 'token-de-teste',
})
const semSessao = criarClienteHttp({ urlBase: 'https://api.exemplo.test' })
const pdf = (nome: string) => new File(['%PDF'], nome, { type: 'application/pdf' })

/** Lê as partes do multipart que o `capturar` resume (a parte JSON e as repetidas). */
function lerPartes(caminho: string) {
  const lido: { respostas?: unknown; tipoDasRespostas?: string; arquivos?: string[] } = {}
  servidor.use(
    http.post(caminho, async ({ request }) => {
      const formulario = await request.clone().formData()
      const respostas = formulario.get('respostas') as File
      lido.respostas = JSON.parse(await respostas.text())
      lido.tipoDasRespostas = respostas.type
      lido.arquivos = formulario.getAll('arquivos').map((a) => (a as File).name)
      return undefined
    }),
  )
  return lido
}

describe('responderFormulario (multipart)', () => {
  it('respostas em JSON (application/json), arquivos pela posicao e avaliadoId nas avaliacoes, conforme', async () => {
    const envio = capturar('responderFormulario')
    const partes = lerPartes('*/api/eu/formularios/:formularioId/respostas')
    await responderFormulario(
      9,
      {
        avaliadoId: 21,
        respostas: [
          { perguntaId: 301, valorEscala: 4 },
          { perguntaId: 304, arquivoIndice: 0 },
        ],
        arquivos: [pdf('comprovante.pdf')],
      },
      comSessao,
    )
    expect(envio.ultima().cabecalhos.get('content-type')).toMatch(
      /^multipart\/form-data; boundary=/,
    )
    expect(envio.ultima().corpo).toMatchObject({ avaliadoId: '21' })
    expect(partes).toEqual({
      respostas: [
        { perguntaId: 301, valorEscala: 4 },
        { perguntaId: 304, arquivoIndice: 0 },
      ],
      tipoDasRespostas: 'application/json',
      arquivos: ['comprovante.pdf'],
    })
    envio.conforme()
  })

  it('sem avaliacao nao manda avaliadoId; 409 JA_RESPONDIDO vira ErroDeApi com o codigo', async () => {
    const envio = capturar(
      'responderFormulario',
      problema(
        409,
        exemploDoContrato<Record<string, unknown>>('responderFormulario', 409, 'jaRespondido'),
      ),
    )
    const erro = await responderFormulario(
      8,
      { respostas: [{ perguntaId: 801, valorEscala: 3 }], arquivos: [] },
      comSessao,
    ).catch((e: unknown) => e)
    expect(envio.ultima().corpo).not.toHaveProperty('avaliadoId')
    expect(erro).toBeInstanceOf(ErroDeApi)
    expect((erro as ErroDeApi).codigo).toBe('JA_RESPONDIDO')
  })

  it('400 com errors por campo chega ao front', async () => {
    capturar(
      'responderFormulario',
      problema(400, {
        title: 'Dados inválidos.',
        errors: { 'respostas[0].texto': ['O texto é obrigatório.'] },
      }),
    )
    const erro = (await responderFormulario(
      8,
      { respostas: [{ perguntaId: 1, texto: 'x' }], arquivos: [] },
      comSessao,
    ).catch((e: unknown) => e)) as ErroDeApi
    expect(erro.errors).toEqual({ 'respostas[0].texto': ['O texto é obrigatório.'] })
  })
})

describe('responderFormularioPublico (multipart, sem sessao)', () => {
  it('candidato com curriculo: os campos do candidato, respostas em JSON e nenhum Authorization, conforme', async () => {
    const envio = capturar('responderFormularioPublico')
    const partes = lerPartes('*/api/publico/formularios/:token/respostas')
    const recibo = await responderFormularioPublico(
      'k3Qz9vT1mP8xLw2RbN7cYdEa5HuJ0sFgXo4iBtVq6Zc',
      {
        nome: 'Mariana Teixeira',
        email: 'mariana.teixeira@email.com.br',
        telefone: '(11) 98877-1204',
        curriculo: pdf('curriculo.pdf'),
        aceitouAvisoDePrivacidade: true,
      },
      { respostas: [{ perguntaId: 501, alternativaIds: [901] }], arquivos: [] },
      semSessao,
    )
    expect(envio.ultima().cabecalhos.get('authorization')).toBeNull()
    expect(envio.ultima().corpo).toMatchObject({
      nome: 'Mariana Teixeira',
      email: 'mariana.teixeira@email.com.br',
      telefone: '(11) 98877-1204',
      curriculo: 'curriculo.pdf',
      aceitouAvisoDePrivacidade: 'true',
    })
    expect(partes.respostas).toEqual([{ perguntaId: 501, alternativaIds: [901] }])
    expect(recibo).toEqual(exemploDoContrato('responderFormularioPublico', 201))
    envio.conforme()
  })
})
