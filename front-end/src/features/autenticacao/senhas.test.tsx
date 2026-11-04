import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { clienteHttpPublico } from '../../shared/api/apiPublica'
import { exemplos } from '../../shared/mocks/gerado/handlers'
import { servidor } from '../../shared/mocks/servidor'
import { capturar, problema } from '../../test/capturar'
import { renderizarApp } from '../../test/renderizarApp'
import { renderizarTela } from '../../test/telas'
import { tokenDeAcesso } from './armazenamentoDaSessao'
import { esquemaDeNovaSenha, esquemaDeTrocaDeSenha } from './esquemasDeSenha'
import { useValidacaoDoLink } from './useValidacaoDoLink'
import userEvent from '@testing-library/user-event'

const LINK = exemplos.validarLinkDeSenha.corpo as Record<string, unknown>
const LINK_VENCIDO = problema(422, {
  codigo: 'LINK_INVALIDO_OU_VENCIDO',
  title: 'Link inválido ou vencido.',
  detail: 'Este link não vale mais. Peça um novo ao RH ou use "Esqueci minha senha".',
})

function deslogado(rota: string) {
  localStorage.clear()
  tokenDeAcesso.definir(null)
  const user = userEvent.setup()
  return { ...renderizarApp({ rota }), user }
}

beforeEach(() => {
  localStorage.clear()
  tokenDeAcesso.definir(null)
})
afterEach(() => tokenDeAcesso.definir(null))

describe('cliente publico e esquemas', () => {
  it('nenhuma chamada do cliente publico leva Authorization, mesmo com token de acesso em memoria', async () => {
    tokenDeAcesso.definir('tok')
    const pedido = capturar('solicitarRedefinicaoDeSenha')
    await clienteHttpPublico.POST('/api/auth/senha/esqueci', { body: { email: 'a@b.c' } })
    expect(pedido.ultima().cabecalhos.get('authorization')).toBeNull()
  })

  it('senha de 8 a 128 sem trim; confirmacao diferente falha; igual a atual falha', () => {
    expect(
      esquemaDeNovaSenha.parse({ novaSenha: ' abcdefg ', confirmacao: ' abcdefg ' }).novaSenha,
    ).toBe(' abcdefg ')
    expect(esquemaDeNovaSenha.safeParse({ novaSenha: 'abc', confirmacao: 'abc' }).success).toBe(
      false,
    )
    const diferente = esquemaDeNovaSenha.safeParse({
      novaSenha: 'abcdefgh',
      confirmacao: 'abcdefgx',
    })
    expect(diferente.error?.issues[0]?.message).toBe('As senhas não conferem.')
    const igual = esquemaDeTrocaDeSenha.safeParse({
      senhaAtual: 'abcdefgh',
      novaSenha: 'abcdefgh',
      confirmacao: 'abcdefgh',
    })
    expect(igual.error?.issues[0]?.message).toBe('A nova senha precisa ser diferente da atual.')
  })

  it('em StrictMode chama validar-link uma unica vez (o limite e de 5 por minuto)', async () => {
    const validar = capturar('validarLinkDeSenha')
    const qc = new QueryClient()
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={qc}>{children}</QueryClientProvider>
    )
    const { result } = renderHook(() => useValidacaoDoLink('abc'), {
      wrapper,
      reactStrictMode: true,
    })
    await waitFor(() => expect(result.current.data).toBeDefined())
    expect(validar.quantas()).toBe(1)
  })
})

describe('Entrar e Esqueci minha senha', () => {
  it('PaginaEntrar mostra o link Esqueci minha senha e leva o e-mail ja digitado', async () => {
    const { user, roteador } = deslogado('/entrar')
    await user.type(await screen.findByLabelText('E-mail'), 'ana.souza@empresa.com.br')
    await user.click(screen.getByRole('link', { name: 'Esqueci minha senha' }))
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/esqueci-senha'))
    expect(await screen.findByLabelText('E-mail')).toHaveValue('ana.souza@empresa.com.br')
  })

  it('mostra titulo, texto, campo, botao e Voltar; 202 troca o formulario pela frase uniforme', async () => {
    const pedido = capturar('solicitarRedefinicaoDeSenha')
    const { user } = deslogado('/esqueci-senha')
    expect(await screen.findByRole('heading', { name: 'Esqueci minha senha' })).toBeInTheDocument()
    expect(
      screen.getByText(
        'Digite o e-mail do seu acesso. Se ele estiver cadastrado, enviaremos um link para definir uma nova senha. O link vale 1 hora.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Voltar para entrar' })).toBeInTheDocument()
    await user.type(screen.getByLabelText('E-mail'), 'bruno.lima@empresa.com.br')
    await user.click(screen.getByRole('button', { name: 'Enviar link' }))
    expect(
      await screen.findByText(
        'Se o e-mail estiver cadastrado, enviaremos um link para definir uma nova senha.',
      ),
    ).toBeInTheDocument()
    expect(screen.queryByText(/enviado para/)).toBeNull()
    expect(pedido.ultima().corpo).toEqual({ email: 'bruno.lima@empresa.com.br' })
    pedido.conforme()
  })

  it('e-mail mal formado nao chama a API; 429 e rede mostram os textos', async () => {
    const pedido = capturar('solicitarRedefinicaoDeSenha', problema(429))
    const { user } = deslogado('/esqueci-senha')
    await user.type(await screen.findByLabelText('E-mail'), 'ana')
    await user.click(screen.getByRole('button', { name: 'Enviar link' }))
    expect(await screen.findByText('Informe um e-mail válido.')).toBeInTheDocument()
    expect(pedido.quantas()).toBe(0)
    await user.type(screen.getByLabelText('E-mail'), '@empresa.com.br')
    await user.click(screen.getByRole('button', { name: 'Enviar link' }))
    expect(
      await screen.findByText('Muitas tentativas. Aguarde um minuto e tente de novo.'),
    ).toBeInTheDocument()
    servidor.use(http.post('*/api/auth/senha/esqueci', () => HttpResponse.error()))
    await user.click(screen.getByRole('button', { name: 'Enviar link' }))
    expect(
      await screen.findByText('Não foi possível conectar. Tente novamente.'),
    ).toBeInTheDocument()
  })

  it('abre igual com sessao neste navegador e nao redireciona', async () => {
    const { roteador } = renderizarTela('/esqueci-senha')
    expect(await screen.findByRole('heading', { name: 'Esqueci minha senha' })).toBeInTheDocument()
    expect(roteador.state.location.pathname).toBe('/esqueci-senha')
  })
})

describe('Definir senha', () => {
  it('sem token vai direto a Link invalido e nao chama a API', async () => {
    const validar = capturar('validarLinkDeSenha')
    deslogado('/definir-senha')
    expect(await screen.findByRole('heading', { name: 'Link inválido' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Esqueci minha senha' })).toBeInTheDocument()
    expect(validar.quantas()).toBe(0)
  })

  it('com token confere o link antes do formulario; CONVITE e REDEFINICAO mudam o titulo', async () => {
    let liberar: () => void = () => {}
    const validar = capturar('validarLinkDeSenha', {
      espera: new Promise<void>((r) => (liberar = r)),
    })
    const { unmount } = deslogado('/definir-senha?token=abc123')
    expect(await screen.findByText('Conferindo o link…')).toBeInTheDocument()
    expect(screen.queryByLabelText('Nova senha')).toBeNull()
    liberar()
    expect(
      await screen.findByRole('heading', { name: 'Bem-vindo, defina a sua senha' }),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        'Olá, Rafael Torres. Esta será a senha do acesso rafael.torres@empresa.com.br.',
      ),
    ).toBeInTheDocument()
    expect(validar.ultima().corpo).toEqual({ token: 'abc123' })
    validar.conforme()
    unmount()
    capturar('validarLinkDeSenha', { corpo: { ...LINK, tipo: 'REDEFINICAO' } })
    deslogado('/definir-senha?token=abc123')
    expect(
      await screen.findByRole('heading', { name: 'Defina uma nova senha' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Acesso rafael.torres@empresa.com.br.')).toBeInTheDocument()
  })

  it('confirmacao diferente nao chama a API; salvar envia so { token, novaSenha } sem trim e mostra a confirmacao', async () => {
    const definir = capturar('definirSenhaPeloLink')
    const { user } = deslogado('/definir-senha?token=abc123')
    await user.type(await screen.findByLabelText('Nova senha'), ' segredo1 ')
    await user.type(screen.getByLabelText('Repita a nova senha'), 'outra coisa')
    expect(screen.getByText('De 8 a 128 caracteres.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Salvar senha' }))
    expect(await screen.findByText('As senhas não conferem.')).toBeInTheDocument()
    expect(definir.quantas()).toBe(0)
    await user.clear(screen.getByLabelText('Repita a nova senha'))
    await user.type(screen.getByLabelText('Repita a nova senha'), ' segredo1 ')
    await user.click(screen.getByRole('button', { name: 'Salvar senha' }))
    expect(
      await screen.findByText('Senha definida. Agora você já pode entrar.'),
    ).toBeInTheDocument()
    expect(definir.ultima().corpo).toEqual({ token: 'abc123', novaSenha: ' segredo1 ' })
    definir.conforme()
    expect(screen.getByRole('link', { name: 'Ir para entrar' })).toBeInTheDocument()
    expect(localStorage.getItem('rh.refresh')).toBeNull()
  })

  it('422 ao abrir e ao salvar (link que venceu entre abrir e salvar) mostra o detail (Review Focus 5)', async () => {
    capturar('validarLinkDeSenha', LINK_VENCIDO)
    const { unmount } = deslogado('/definir-senha?token=abc123')
    expect(await screen.findByText(/Este link não vale mais/)).toBeInTheDocument()
    unmount()
    capturar('validarLinkDeSenha')
    capturar('definirSenhaPeloLink', LINK_VENCIDO)
    const { user } = deslogado('/definir-senha?token=abc123')
    await user.type(await screen.findByLabelText('Nova senha'), 'segredo12')
    await user.type(screen.getByLabelText('Repita a nova senha'), 'segredo12')
    await user.click(screen.getByRole('button', { name: 'Salvar senha' }))
    expect(await screen.findByRole('heading', { name: 'Link inválido' })).toBeInTheDocument()
    expect(screen.getByText(/Este link não vale mais/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Voltar para entrar' })).toBeInTheDocument()
  })

  it('erro diferente de 422 ao abrir mostra Tentar de novo e nao o formulario; 400 em novaSenha marca o campo', async () => {
    capturar('validarLinkDeSenha', problema(500))
    const { unmount } = deslogado('/definir-senha?token=abc123')
    expect(await screen.findByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
    expect(screen.queryByLabelText('Nova senha')).toBeNull()
    unmount()
    capturar('validarLinkDeSenha')
    capturar(
      'definirSenhaPeloLink',
      problema(400, { errors: { novaSenha: ['Senha fraca demais.'] } }),
    )
    const { user } = deslogado('/definir-senha?token=abc123')
    await user.type(await screen.findByLabelText('Nova senha'), 'segredo12')
    await user.type(screen.getByLabelText('Repita a nova senha'), 'segredo12')
    await user.click(screen.getByRole('button', { name: 'Salvar senha' }))
    expect(await screen.findByText('Senha fraca demais.')).toBeInTheDocument()
  })

  it('o token nunca aparece na tela nem no localStorage e nenhuma chamada leva Authorization', async () => {
    const validar = capturar('validarLinkDeSenha')
    renderizarTela('/definir-senha?token=abc123')
    await screen.findByRole('heading', { name: 'Bem-vindo, defina a sua senha' })
    expect(document.body.textContent).not.toContain('abc123')
    expect(JSON.stringify({ ...localStorage })).not.toContain('abc123')
    expect(validar.ultima().cabecalhos.get('authorization')).toBeNull()
  })
})

describe('Trocar senha', () => {
  it.each(['RH', 'Funcionario', 'Gestor'] as const)(
    'o rodape do %s tem Trocar senha ao lado de Sair e abre o modal',
    async (perfil) => {
      const { user } = renderizarTela('/eu/inicio', { perfil })
      const rodape = await screen.findByTestId('rodape-do-menu')
      expect(
        within(rodape)
          .getAllByRole('button')
          .map((b) => b.textContent),
      ).toEqual(['Trocar senha', 'Sair'])
      await user.click(within(rodape).getByRole('button', { name: 'Trocar senha' }))
      const modal = await screen.findByRole('dialog', { name: 'Trocar senha' })
      expect(within(modal).getByLabelText('Senha atual')).toBeInTheDocument()
      expect(within(modal).getByLabelText('Nova senha')).toBeInTheDocument()
      expect(within(modal).getByLabelText('Repita a nova senha')).toBeInTheDocument()
      expect(within(modal).getByText('De 8 a 128 caracteres.')).toBeInTheDocument()
    },
  )

  async function abrirTroca() {
    const tela = renderizarTela('/eu/inicio')
    await tela.user.click(await screen.findByRole('button', { name: 'Trocar senha' }))
    const modal = await screen.findByRole('dialog', { name: 'Trocar senha' })
    return { ...tela, modal }
  }

  it('envia { senhaAtual, novaSenha } conforme; 204 fecha, avisa e a sessao continua', async () => {
    const trocar = capturar('trocarMinhaSenha')
    const { user, modal } = await abrirTroca()
    await user.type(within(modal).getByLabelText('Senha atual'), 'antiga123')
    await user.type(within(modal).getByLabelText('Nova senha'), 'novinha123')
    await user.type(within(modal).getByLabelText('Repita a nova senha'), 'novinha123')
    await user.click(screen.getByRole('button', { name: 'Trocar senha', hidden: false }))
    await waitFor(() => expect(trocar.quantas()).toBe(1))
    expect(trocar.ultima().corpo).toEqual({ senhaAtual: 'antiga123', novaSenha: 'novinha123' })
    trocar.conforme()
    expect(
      await screen.findByText('Senha trocada. As suas outras sessões foram encerradas.'),
    ).toBeInTheDocument()
    expect(localStorage.getItem('rh.refresh')).not.toBeNull()
    expect(screen.getByRole('navigation', { name: 'Menu principal' })).toBeInTheDocument()
  })

  it('422 SENHA_ATUAL_INCORRETA marca Senha atual sem deslogar; nova igual a atual nao chama a API', async () => {
    const trocar = capturar(
      'trocarMinhaSenha',
      problema(422, { codigo: 'SENHA_ATUAL_INCORRETA', detail: 'A senha atual está incorreta.' }),
    )
    const { user, modal } = await abrirTroca()
    await user.type(within(modal).getByLabelText('Senha atual'), 'igual1234')
    await user.type(within(modal).getByLabelText('Nova senha'), 'igual1234')
    await user.type(within(modal).getByLabelText('Repita a nova senha'), 'igual1234')
    const botaoDoModal = within(modal.parentElement ?? document.body)
      .getAllByRole('button', { name: 'Trocar senha' })
      .at(-1)!
    await user.click(botaoDoModal)
    expect(
      await within(modal).findByText('A nova senha precisa ser diferente da atual.'),
    ).toBeInTheDocument()
    expect(trocar.quantas()).toBe(0)
    await user.clear(within(modal).getByLabelText('Nova senha'))
    await user.type(within(modal).getByLabelText('Nova senha'), 'outra1234')
    await user.clear(within(modal).getByLabelText('Repita a nova senha'))
    await user.type(within(modal).getByLabelText('Repita a nova senha'), 'outra1234')
    await user.click(botaoDoModal)
    expect(await within(modal).findByText('A senha atual está incorreta.')).toBeInTheDocument()
    expect(within(modal).getByLabelText('Senha atual')).toHaveAttribute('aria-invalid', 'true')
    expect(localStorage.getItem('rh.refresh')).not.toBeNull()
  })
})
