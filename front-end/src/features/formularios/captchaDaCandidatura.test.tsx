import { fireEvent, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import type { Esquema } from '../../shared/api/tipos'
import { servidor } from '../../shared/mocks/servidor'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { renderizarTela } from '../../test/telas'
import { instalarTurnstileFalso, type TurnstileFalso } from '../../test/turnstileFalso'
import { responderFormularioPublico } from './enviarRespostas'

vi.mock('./enviarRespostas', () => ({
  responderFormulario: vi.fn(),
  responderFormularioPublico: vi.fn(),
}))

type Pagina = Esquema<'FormularioPaginaPublica'>
const PUBLICO = exemploDoContrato<Pagina>('obterFormularioPublico')
const CHAVE = 'chave-publica-de-teste'
const PERGUNTA: Pagina['perguntas'][number] = {
  id: 1,
  ordem: 1,
  texto: 'Disponível aos sábados?',
  tipo: 'RESPOSTA_CURTA',
  obrigatoria: true,
  alternativas: [],
}
const SCRIPT = 'script[src^="https://challenges.cloudflare.com/turnstile/v0/api.js"]'

let turnstile: TurnstileFalso

function paginaComCaptcha(captcha: Pagina['captcha']) {
  servidor.use(
    http.get('*/api/publico/formularios/:token', () =>
      HttpResponse.json({ ...PUBLICO, captcha, perguntas: [PERGUNTA] } satisfies Pagina),
    ),
  )
}

/** Preenche os dados, passa para a pergunta e responde; devolve o botão de envio. */
async function chegarAoEnvio(user: ReturnType<typeof renderizarTela>['user']) {
  await user.type(await screen.findByRole('textbox', { name: 'Nome' }), 'Mariana')
  await user.type(screen.getByRole('textbox', { name: 'E-mail' }), 'm@email.com.br')
  await user.click(screen.getByRole('checkbox', { name: /Li e concordo/ }))
  await user.click(screen.getByRole('button', { name: 'Começar' }))
  await user.type(screen.getByRole('textbox'), 'Sim')
  return screen.getByRole('button', { name: 'Enviar candidatura' })
}

const botaoDeEnvio = () => screen.getByRole('button', { name: 'Enviar candidatura' })

beforeEach(() => {
  turnstile = instalarTurnstileFalso()
  vi.mocked(responderFormularioPublico).mockResolvedValue(
    exemploDoContrato('responderFormularioPublico', 201),
  )
})
afterEach(() => {
  turnstile.desinstalar()
  document.querySelectorAll(SCRIPT).forEach((s) => s.remove())
  vi.clearAllMocks()
})

describe('Captcha da candidatura publica (contrato 0.3.0)', { timeout: 20000 }, () => {
  it('ligado: desenha o widget com a chave do site so na ultima etapa e o envio espera o token', async () => {
    paginaComCaptcha({ chaveDoSite: CHAVE })
    const { user } = renderizarTela('/formulario/abc')
    expect(turnstile.renders).toHaveLength(0)
    const enviar = await chegarAoEnvio(user)
    await waitFor(() => expect(turnstile.renders).toHaveLength(1))
    expect(turnstile.renders[0]!.opcoes.sitekey).toBe(CHAVE)
    expect(enviar).toBeDisabled()
    expect(screen.getByText('Confirme que você não é um robô para enviar.')).toBeInTheDocument()
    turnstile.resolver('token-1')
    await waitFor(() => expect(enviar).toBeEnabled())
    await user.click(enviar)
    await waitFor(() => expect(responderFormularioPublico).toHaveBeenCalledTimes(1))
    expect(vi.mocked(responderFormularioPublico).mock.calls[0]![1]).toMatchObject({
      captchaToken: 'token-1',
    })
  })

  it('desligado (captcha null): nao desenha widget, o envio nao espera nada e nao manda captchaToken', async () => {
    paginaComCaptcha(null)
    const { user } = renderizarTela('/formulario/abc')
    const enviar = await chegarAoEnvio(user)
    expect(enviar).toBeEnabled()
    await user.click(enviar)
    await waitFor(() => expect(responderFormularioPublico).toHaveBeenCalledTimes(1))
    expect(turnstile.renders).toHaveLength(0)
    expect(vi.mocked(responderFormularioPublico).mock.calls[0]![1]).not.toHaveProperty(
      'captchaToken',
    )
  })

  it('depois de um erro o widget nasce de novo e o token usado nao serve mais (cada token vale uma tentativa)', async () => {
    paginaComCaptcha({ chaveDoSite: CHAVE })
    vi.mocked(responderFormularioPublico).mockRejectedValueOnce(
      new ErroDeApi(422, 'Operação recusada.', {
        detail: 'Não foi possível confirmar que você não é um robô. Tente de novo.',
        codigo: 'CAPTCHA_INVALIDO',
      }),
    )
    const { user } = renderizarTela('/formulario/abc')
    const enviar = await chegarAoEnvio(user)
    await waitFor(() => expect(turnstile.renders).toHaveLength(1))
    turnstile.resolver('token-velho')
    await waitFor(() => expect(enviar).toBeEnabled())
    await user.click(enviar)
    expect(
      await screen.findByText('Não foi possível confirmar que você não é um robô. Tente de novo.'),
    ).toBeInTheDocument()
    await waitFor(() => expect(turnstile.renders).toHaveLength(2))
    expect(turnstile.removidos).toContain('widget-1')
    expect(botaoDeEnvio()).toBeDisabled()
    turnstile.resolver('token-novo')
    await waitFor(() => expect(botaoDeEnvio()).toBeEnabled())
    await user.click(botaoDeEnvio())
    await waitFor(() => expect(responderFormularioPublico).toHaveBeenCalledTimes(2))
    expect(vi.mocked(responderFormularioPublico).mock.calls[1]![1]).toMatchObject({
      captchaToken: 'token-novo',
    })
  })

  it('400 em errors.captchaToken mostra a mensagem do contrato e pede outro token', async () => {
    paginaComCaptcha({ chaveDoSite: CHAVE })
    vi.mocked(responderFormularioPublico).mockRejectedValueOnce(
      new ErroDeApi(400, 'Confira os dados informados.', {
        errors: { captchaToken: ['Confirme que você não é um robô.'] },
      }),
    )
    const { user } = renderizarTela('/formulario/abc')
    const enviar = await chegarAoEnvio(user)
    await waitFor(() => expect(turnstile.renders).toHaveLength(1))
    turnstile.resolver('t')
    await waitFor(() => expect(enviar).toBeEnabled())
    await user.click(enviar)
    expect(await screen.findByText('Confirme que você não é um robô.')).toBeInTheDocument()
    await waitFor(() => expect(turnstile.renders).toHaveLength(2))
  })

  it('token vencido (expired-callback) desliga o envio ate passar de novo', async () => {
    paginaComCaptcha({ chaveDoSite: CHAVE })
    const { user } = renderizarTela('/formulario/abc')
    const enviar = await chegarAoEnvio(user)
    await waitFor(() => expect(turnstile.renders).toHaveLength(1))
    turnstile.resolver('t1')
    await waitFor(() => expect(enviar).toBeEnabled())
    turnstile.expirar()
    await waitFor(() => expect(enviar).toBeDisabled())
    turnstile.resolver('t2')
    await waitFor(() => expect(enviar).toBeEnabled())
  })

  it('o widget com erro mostra a mensagem e "Tentar de novo" cria outro widget', async () => {
    paginaComCaptcha({ chaveDoSite: CHAVE })
    const { user } = renderizarTela('/formulario/abc')
    await chegarAoEnvio(user)
    await waitFor(() => expect(turnstile.renders).toHaveLength(1))
    turnstile.falhar()
    expect(await screen.findByText('Não foi possível carregar a verificação.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Tentar de novo' }))
    await waitFor(() => expect(turnstile.renders).toHaveLength(2))
  })

  it('sem o script do Cloudflare (bloqueado ou sem rede) mostra a mensagem, nao deixa enviar e permite tentar de novo', async () => {
    turnstile.desinstalar()
    paginaComCaptcha({ chaveDoSite: CHAVE })
    const { user } = renderizarTela('/formulario/abc')
    const enviar = await chegarAoEnvio(user)
    const script = await waitFor(() => {
      const achado = document.querySelector<HTMLScriptElement>(SCRIPT)
      expect(achado).not.toBeNull()
      return achado!
    })
    expect(script.src).toContain('render=explicit')
    fireEvent.error(script)
    expect(await screen.findByText('Não foi possível carregar a verificação.')).toBeInTheDocument()
    expect(enviar).toBeDisabled()
    // "Tentar de novo" pede o script outra vez (o primeiro foi removido); agora ele carrega
    await user.click(screen.getByRole('button', { name: 'Tentar de novo' }))
    const outro = await waitFor(() => {
      const achado = document.querySelector<HTMLScriptElement>(SCRIPT)
      expect(achado).not.toBeNull()
      expect(achado).not.toBe(script)
      return achado!
    })
    turnstile = instalarTurnstileFalso()
    fireEvent.load(outro)
    await waitFor(() => expect(turnstile.renders).toHaveLength(1))
    turnstile.resolver('t')
    await waitFor(() => expect(enviar).toBeEnabled())
  })
})
