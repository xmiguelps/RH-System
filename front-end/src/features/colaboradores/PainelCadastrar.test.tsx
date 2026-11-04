import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { servidor } from '../../shared/mocks/servidor'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { escolherOpcao, preencherData } from '../../test/formulario'
import { renderizarTela } from '../../test/telas'

async function abrir(rota = '/rh/colaboradores/novo') {
  const tela = renderizarTela(rota)
  await screen.findByRole('dialog', { name: 'Adicionar colaborador' })
  await waitFor(() =>
    expect(screen.getByRole('combobox', { name: 'Cargo' })).not.toHaveTextContent('Carregando'),
  )
  return tela
}

async function preencherTudo(
  user: Awaited<ReturnType<typeof abrir>>['user'],
  opcoes = { convite: true },
) {
  await user.type(screen.getByLabelText('Nome completo'), 'Rafael Torres')
  await user.type(screen.getByLabelText('CPF'), '84639271069')
  preencherData('Data de nascimento', '1995-02-09')
  await user.type(screen.getByLabelText('Telefone'), '11991234567')
  await user.type(screen.getByLabelText('E-mail corporativo'), 'rafael.torres@empresa.com.br')
  preencherData('Data de admissão', '2026-10-13')
  await escolherOpcao(user, 'Cargo', 'Vendedor')
  await escolherOpcao(user, 'Setor', 'Comercial')
  await user.click(screen.getByText('Vale-refeição'))
  await user.click(screen.getByText('Plano de saúde'))
  if (!opcoes.convite) await user.click(screen.getByText('Enviar convite de acesso ao portal'))
}

const salvar = () => screen.getByRole('button', { name: 'Adicionar colaborador' })

describe('Painel Adicionar colaborador (tela 02)', { timeout: 15000 }, () => {
  it('mostra os blocos e os rotulos da tela 02 e nao tem matricula nem unidade nem gestorId', async () => {
    await abrir()
    for (const titulo of ['Dados pessoais', 'Contrato', 'Benefícios', 'Depois de salvar']) {
      expect(screen.getByText(titulo)).toBeInTheDocument()
    }
    expect(
      screen.getByText(
        'Os campos marcados entram no cadastro. O restante a própria pessoa completa no formulário de admissão.',
      ),
    ).toBeInTheDocument()
    expect(screen.queryByLabelText('Matrícula')).toBeNull()
    expect(screen.queryByText('Unidade')).toBeNull()
  })

  it('abre em Cadastrar um colaborador; Importar planilha (fatia 2) esta ligada', async () => {
    await abrir()
    expect(screen.getByRole('tab', { name: 'Importar planilha' })).not.toHaveAttribute(
      'aria-disabled',
      'true',
    )
    expect(screen.getByRole('tab', { name: 'Cadastrar um colaborador' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
  })

  it('o onboarding (ficha 6) e o formulario de admissao (ficha 5) ligam desmarcados; Enviar convite vem marcada', async () => {
    await abrir()
    const onboarding = await screen.findByRole('checkbox', {
      name: /Incluir no treinamento Onboarding 2026/,
    })
    expect(onboarding).toHaveAttribute('aria-checked', 'false')
    await waitFor(() => expect(onboarding).not.toHaveAttribute('aria-disabled', 'true'))
    const admissao = screen.getByRole('checkbox', { name: /Enviar o formulário de admissão/ })
    expect(admissao).toHaveAttribute('aria-checked', 'false')
    await waitFor(() => expect(admissao).not.toHaveAttribute('aria-disabled', 'true'))
    expect(screen.queryByText('Indisponível por enquanto')).toBeNull()
    expect(
      screen.getByRole('checkbox', { name: /Enviar convite de acesso ao portal/ }),
    ).toHaveAttribute('aria-checked', 'true')
  })

  it('os sete beneficios do catalogo aparecem desmarcados', async () => {
    await abrir()
    const nomes = [
      'Vale-refeição',
      'Plano de saúde',
      'Plano odontológico',
      'Vale-transporte',
      'Seguro de vida',
      'Auxílio-creche',
      'Vale-cultura',
    ]
    for (const nome of nomes) {
      expect(screen.getByRole('checkbox', { name: new RegExp(nome) })).toHaveAttribute(
        'aria-checked',
        'false',
      )
    }
  })

  it('o gestor aparece conforme o setor escolhido', async () => {
    const { user } = await abrir()
    const gestor = screen.getByLabelText('Gestor')
    expect(gestor).toHaveValue('')
    expect(gestor).toHaveAttribute('placeholder', 'O gestor do setor escolhido')
    await escolherOpcao(user, 'Setor', 'Comercial')
    expect(gestor).toHaveValue('Carlos Ribeiro')
    await escolherOpcao(user, 'Setor', 'Compras')
    expect(gestor).toHaveValue('Este setor não tem gestor')
  })

  it('valida no cliente, marca cada campo e nao chama a API', async () => {
    const criar = capturar('criarFuncionario')
    const { user } = await abrir()
    await user.type(screen.getByLabelText('CPF'), '11111111111')
    await user.click(salvar())
    expect(await screen.findByText('CPF inválido.')).toBeInTheDocument()
    expect(screen.getByText('Informe o nome.')).toBeInTheDocument()
    expect(criar.quantas()).toBe(0)
  })

  it('envia o corpo do contrato e a requisicao e conforme', async () => {
    const criar = capturar('criarFuncionario')
    const { user } = await abrir()
    await preencherTudo(user)
    await user.click(salvar())
    await waitFor(() => expect(criar.quantas()).toBe(1))
    expect(criar.ultima().corpo).toEqual({
      nome: 'Rafael Torres',
      cpf: '84639271069',
      dataNascimento: '1995-02-09',
      telefone: '11991234567',
      emailCorporativo: 'rafael.torres@empresa.com.br',
      dataAdmissao: '2026-10-13',
      tipoContrato: 'CLT',
      cargoId: 2,
      setorId: 1,
      perfil: 'Funcionario',
      beneficioIds: [1, 2],
      enviarConvite: true,
      incluirNoOnboarding: false,
      formularioAdmissaoId: null,
    })
    criar.conforme()
  })

  it('sucesso fecha o painel, atualiza a lista e mostra a faixa com Abrir ficha', async () => {
    const lista = capturar('listarFuncionarios')
    const { user, roteador } = await abrir()
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(0))
    const antes = lista.quantas()
    await preencherTudo(user)
    await user.click(salvar())
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/rh/colaboradores'))
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(antes))
    const faixa = await screen.findByText(
      'Rafael Torres foi cadastrado. O convite foi enviado para rafael.torres@empresa.com.br.',
    )
    expect(
      within(faixa.closest('[role="status"]')!).getByRole('link', { name: 'Abrir ficha' }),
    ).toHaveAttribute('href', '/rh/colaboradores/104')
  })

  it('sem convite a segunda frase some', async () => {
    const { user } = await abrir()
    await preencherTudo(user, { convite: false })
    await user.click(salvar())
    expect(await screen.findByText('Rafael Torres foi cadastrado.')).toBeInTheDocument()
  })

  it('409 de CPF de cadastro inativo mostra o detail junto do CPF e oferece Abrir a ficha de Henrique Alves', async () => {
    capturar(
      'criarFuncionario',
      problema(409, exemploDoContrato('criarFuncionario', 409, 'cpfInativo')),
    )
    const { user } = await abrir()
    await preencherTudo(user)
    await user.click(salvar())
    const detail = await screen.findAllByText(
      /Este CPF pertence a um cadastro inativo \(Henrique Alves\)/,
    )
    expect(detail).toHaveLength(1)
    expect(screen.getByLabelText('CPF')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByRole('link', { name: 'Abrir a ficha de Henrique Alves' })).toHaveAttribute(
      'href',
      '/rh/colaboradores/201',
    )
  })

  it('409 de e-mail repetido marca o e-mail e 422 do gestor inativo marca o Setor, sem regraDoBanco', async () => {
    capturar('criarFuncionario', problema(409, exemploDoContrato('criarFuncionario', 409, 'email')))
    const { user, unmount } = await abrir()
    await preencherTudo(user)
    await user.click(salvar())
    await waitFor(() =>
      expect(screen.getByLabelText('E-mail corporativo')).toHaveAttribute('aria-invalid', 'true'),
    )
    unmount()
    capturar(
      'criarFuncionario',
      problema(422, exemploDoContrato('criarFuncionario', 422, 'gestorInativo')),
    )
    const outra = await abrir()
    await preencherTudo(outra.user)
    await outra.user.click(salvar())
    expect(
      await screen.findByText(
        'O gestor do setor escolhido está inativo. Troque o gestor do setor em Cadastros antes de continuar.',
      ),
    ).toBeInTheDocument()
    expect(screen.queryByText('O gestor informado e um funcionario inativo.')).toBeNull()
  })

  it('400 com errors por campo e falha de rede aparecem no lugar certo', async () => {
    capturar('criarFuncionario', problema(400, { errors: { nome: ['Nome grande demais.'] } }))
    const { user, unmount } = await abrir()
    await preencherTudo(user)
    await user.click(salvar())
    expect(await screen.findByText('Nome grande demais.')).toBeInTheDocument()
    unmount()
    servidor.use(http.post('*/api/rh/funcionarios', () => HttpResponse.error()))
    const outra = await abrir()
    await preencherTudo(outra.user)
    await outra.user.click(salvar())
    expect(
      await screen.findByText('Não foi possível conectar. Tente novamente.'),
    ).toBeInTheDocument()
  })

  it('clique duplo em Adicionar colaborador envia uma unica requisicao', async () => {
    let liberar: () => void = () => {}
    const criar = capturar('criarFuncionario', {
      status: 201,
      espera: new Promise<void>((r) => (liberar = r)),
      corpo: exemploDoContrato('criarFuncionario'),
    })
    const { user } = await abrir()
    await preencherTudo(user)
    await user.dblClick(salvar())
    await waitFor(() => expect(criar.quantas()).toBe(1))
    await new Promise((r) => setTimeout(r, 30))
    expect(criar.quantas()).toBe(1)
    liberar()
  })

  it('opcoes carregando desabilitam o salvar e erro nas opcoes mostra Tentar de novo', async () => {
    let liberar: () => void = () => {}
    capturar('listarCargos', { espera: new Promise<void>((r) => (liberar = r)) })
    renderizarTela('/rh/colaboradores/novo')
    await screen.findByRole('dialog', { name: 'Adicionar colaborador' })
    expect(screen.getByRole('combobox', { name: 'Cargo' })).toHaveTextContent('Carregando…')
    expect(salvar()).toBeDisabled()
    liberar()
  })

  it('erro nas opcoes mostra Tentar de novo', async () => {
    capturar('listarCargos', problema(500))
    renderizarTela('/rh/colaboradores/novo')
    await screen.findByRole('dialog', { name: 'Adicionar colaborador' })
    expect(await screen.findByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
  })

  it('Cancelar e o X fecham o painel sem gravar e mantem os filtros da URL', async () => {
    const criar = capturar('criarFuncionario')
    const { user, roteador } = await abrir('/rh/colaboradores/novo?situacao=INATIVO')
    await user.click(screen.getByRole('button', { name: 'Cancelar' }))
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/rh/colaboradores'))
    expect(roteador.state.location.search).toBe('?situacao=INATIVO')
    await user.click(screen.getByRole('link', { name: 'Adicionar colaborador' }))
    await user.click(await screen.findByRole('button', { name: 'Fechar' }))
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/rh/colaboradores'))
    expect(criar.quantas()).toBe(0)
  })
})

describe('Formulario de admissao no cadastro (ficha 5, bloco 9)', { timeout: 20000 }, () => {
  it('marcada, o cadastro manda formularioAdmissaoId: 11; 422 FORMULARIO_ENCERRADO e FORMULARIO_NAO_E_POR_PESSOAS mostram o detail', async () => {
    const criar = capturar('criarFuncionario')
    const { user } = await abrir()
    await preencherTudo(user)
    const caixa = screen.getByRole('checkbox', { name: /Enviar o formulário de admissão/ })
    await waitFor(() => expect(caixa).not.toHaveAttribute('aria-disabled', 'true'))
    await user.click(caixa)
    await user.click(salvar())
    await waitFor(() => expect(criar.quantas()).toBe(1))
    expect(criar.ultima().corpo).toMatchObject({ formularioAdmissaoId: 11 })
    criar.conforme()
  })

  it.each([
    [
      'formularioEncerrado',
      exemploDoContrato<{ detail: string }>('criarFuncionario', 422, 'formularioEncerrado').detail,
    ],
    ['FORMULARIO_NAO_E_POR_PESSOAS', 'Este formulário de admissão não é por pessoas escolhidas.'],
  ])('422 %s mostra o detail', async (nome, detail) => {
    capturar(
      'criarFuncionario',
      nome === 'formularioEncerrado'
        ? problema(
            422,
            exemploDoContrato<Record<string, unknown>>(
              'criarFuncionario',
              422,
              'formularioEncerrado',
            ),
          )
        : problema(422, {
            title: 'Operação recusada.',
            detail,
            codigo: 'FORMULARIO_NAO_E_POR_PESSOAS',
          }),
    )
    const { user } = await abrir()
    await preencherTudo(user)
    const caixa = screen.getByRole('checkbox', { name: /Enviar o formulário de admissão/ })
    await waitFor(() => expect(caixa).not.toHaveAttribute('aria-disabled', 'true'))
    await user.click(caixa)
    await user.click(salvar())
    expect(await screen.findByText(detail)).toBeInTheDocument()
  })
})

describe('Onboarding no cadastro (ficha 6, bloco 7)', { timeout: 20000 }, () => {
  it('marcada, o cadastro manda incluirNoOnboarding: true; 422 SEM_TREINAMENTO_DE_ONBOARDING mostra o detail', async () => {
    const criar = capturar('criarFuncionario')
    const { user } = await abrir()
    await preencherTudo(user)
    const caixa = await screen.findByRole('checkbox', {
      name: /Incluir no treinamento Onboarding 2026/,
    })
    await waitFor(() => expect(caixa).not.toHaveAttribute('aria-disabled', 'true'))
    await user.click(caixa)
    await user.click(salvar())
    await waitFor(() => expect(criar.quantas()).toBe(1))
    expect(criar.ultima().corpo).toMatchObject({ incluirNoOnboarding: true })
    criar.conforme()
  })

  it('422 semOnboarding mostra o detail', async () => {
    capturar(
      'criarFuncionario',
      problema(
        422,
        exemploDoContrato<Record<string, unknown>>('criarFuncionario', 422, 'semOnboarding'),
      ),
    )
    const { user } = await abrir()
    await preencherTudo(user)
    const caixa = await screen.findByRole('checkbox', {
      name: /Incluir no treinamento Onboarding 2026/,
    })
    await waitFor(() => expect(caixa).not.toHaveAttribute('aria-disabled', 'true'))
    await user.click(caixa)
    await user.click(salvar())
    const detail = exemploDoContrato<{ detail: string }>(
      'criarFuncionario',
      422,
      'semOnboarding',
    ).detail
    expect(await screen.findByText(detail)).toBeInTheDocument()
  })
})
