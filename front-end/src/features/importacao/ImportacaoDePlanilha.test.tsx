import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import type { Esquema } from '../../shared/api/tipos'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { renderizarTela } from '../../test/telas'
import { validarPlanilha } from './enviarPlanilha'
import { TIPO_XLSX } from './rotulosDaImportacao'

vi.mock('./enviarPlanilha', () => ({ validarPlanilha: vi.fn() }))

type Previa = Esquema<'ImportacaoPrevia'>
const COM_ERROS = exemploDoContrato<Previa>('validarPlanilhaDeImportacao', 201, 'comErros')
const PRONTA = exemploDoContrato<Previa>('validarPlanilhaDeImportacao', 201, 'pronta')
const planilha = (nome = 'novos.xlsx', tipo = TIPO_XLSX, tamanho = 2) =>
  new File(['P'.repeat(tamanho)], nome, { type: tipo })

function erroDoContrato(op: string, status: number, nome: string): ErroDeApi {
  const corpo = exemploDoContrato<Record<string, unknown>>(op, status, nome)
  return new ErroDeApi(status, String(corpo.title), {
    detail: corpo.detail as string,
    codigo: corpo.codigo as string,
  })
}

async function abrirImportacao(perfil: 'RH' = 'RH') {
  const tela = renderizarTela('/rh/colaboradores/novo', { perfil })
  await screen.findByRole('dialog', { name: 'Adicionar colaborador' })
  await tela.user.click(screen.getByRole('tab', { name: 'Importar planilha' }))
  await screen.findByRole('heading', { name: 'Importar planilha' })
  return tela
}

async function enviarArquivo(
  tela: Awaited<ReturnType<typeof abrirImportacao>>,
  arquivo = planilha(),
) {
  await tela.user.upload(screen.getByLabelText('Escolher arquivo'), arquivo)
}

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
})

afterEach(() => {
  vi.useRealTimers()
  vi.clearAllMocks()
  vi.restoreAllMocks()
})

describe('Importar planilha (tela 02, modo planilha)', { timeout: 15000 }, () => {
  it('a alternancia troca o conteudo do painel; o modo nao vai na URL; voltar descarta a previa', async () => {
    vi.mocked(validarPlanilha).mockResolvedValue(COM_ERROS)
    const tela = await abrirImportacao()
    expect(screen.getByRole('tab', { name: 'Importar planilha' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
    expect(
      screen.getByText(
        'Cadastre vários colaboradores de uma vez. Baixe o modelo, preencha no Excel e envie aqui. Só cadastra pessoas novas.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByText('Só .xlsx, até 5 MB e 500 linhas')).toBeInTheDocument()
    expect(screen.queryByLabelText('Nome completo')).toBeNull()
    expect(tela.roteador.state.location.pathname).toBe('/rh/colaboradores/novo')
    expect(tela.roteador.state.location.search).toBe('')
    await enviarArquivo(tela)
    expect(await screen.findByRole('table', { name: 'Prévia da planilha' })).toBeInTheDocument()
    await tela.user.click(screen.getByRole('tab', { name: 'Cadastrar um colaborador' }))
    expect(await screen.findByLabelText('Nome completo')).toBeInTheDocument()
    await tela.user.click(screen.getByRole('tab', { name: 'Importar planilha' }))
    expect(screen.queryByRole('table', { name: 'Prévia da planilha' })).toBeNull()
    expect(screen.getByLabelText('Escolher arquivo')).toBeInTheDocument()
  })

  it('Baixar modelo chama baixarModeloDeImportacao e entrega o arquivo', async () => {
    const baixados: string[] = []
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      baixados.push(this.download)
    })
    Object.defineProperty(URL, 'createObjectURL', {
      value: vi.fn(() => 'blob:x'),
      configurable: true,
    })
    Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true })
    const modelo = capturar('baixarModeloDeImportacao', {
      status: 200,
      corpo: 'PK',
      cabecalhos: {
        'content-type': TIPO_XLSX,
        'content-disposition': 'attachment; filename="modelo-importacao-colaboradores.xlsx"',
      },
    })
    const tela = await abrirImportacao()
    await tela.user.click(screen.getByRole('button', { name: 'Baixar modelo' }))
    await waitFor(() => expect(baixados).toEqual(['modelo-importacao-colaboradores.xlsx']))
    expect(modelo.quantas()).toBe(1)
    modelo.conforme()
  })

  it('Baixar modelo com erro avisa e nada baixa', async () => {
    const clique = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    capturar('baixarModeloDeImportacao', problema(500))
    const tela = await abrirImportacao()
    await tela.user.click(screen.getByRole('button', { name: 'Baixar modelo' }))
    expect(
      await screen.findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
    expect(clique).not.toHaveBeenCalled()
  })

  it('previa pronta (importacao 16): faixa verde, todas OK, Depois de salvar e Cadastrar 2 colaboradores ligado', async () => {
    vi.mocked(validarPlanilha).mockResolvedValue(PRONTA)
    const tela = await abrirImportacao()
    const arquivo = planilha('novos-colaboradores-outubro-corrigido.xlsx')
    await enviarArquivo(tela, arquivo)
    expect(validarPlanilha).toHaveBeenCalledWith(arquivo)
    expect(
      await screen.findByText('2 linhas lidas, todas certas. Pode cadastrar.'),
    ).toBeInTheDocument()
    // 13:40 UTC = 10:40 em São Paulo
    expect(
      screen.getByText(
        'Arquivo: novos-colaboradores-outubro-corrigido.xlsx. Esta prévia vale até 10:40.',
      ),
    ).toBeInTheDocument()
    const tabela = screen.getByRole('table', { name: 'Prévia da planilha' })
    expect(within(tabela).getAllByText('OK')).toHaveLength(2)
    expect(
      within(tabela)
        .getAllByRole('columnheader')
        .map((c) => c.textContent),
    ).toEqual([
      'Linha',
      'Situação',
      'Nome',
      'CPF',
      'Nascimento',
      'Telefone',
      'E-mail corporativo',
      'Admissão',
      'Contrato',
      'Cargo',
      'Setor',
      'Nível de acesso',
      'Benefícios',
    ])
    expect(within(tabela).getByText('402.817.593-32')).toBeInTheDocument()
    expect(screen.queryByRole('switch', { name: 'Mostrar só as linhas com erro' })).toBeNull()
    expect(screen.getByText('Depois de salvar')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Cadastrar 2 colaboradores' })).toBeEnabled()
  })

  it('previa com erros (importacao 15): linhas 3 e 5 com coluna e mensagem, celula marcada e Cadastrar desligado', async () => {
    vi.mocked(validarPlanilha).mockResolvedValue(COM_ERROS)
    const tela = await abrirImportacao()
    await enviarArquivo(tela)
    expect(
      await screen.findByText(
        '4 linhas lidas: 2 certas e 2 com erro. Corrija a planilha e envie de novo. Nada foi cadastrado.',
      ),
    ).toBeInTheDocument()
    const tabela = screen.getByRole('table', { name: 'Prévia da planilha' })
    // "Mostrar só as linhas com erro" vem ligado: só 3 e 5
    expect(within(tabela).getAllByText('Com erro')).toHaveLength(2)
    expect(within(tabela).queryByText('Tiago Nogueira')).toBeNull()
    const linha3 = within(tabela).getByRole('list', { name: 'Erros da linha 3' })
    expect(within(linha3).getByText('CPF: CPF inválido.')).toBeInTheDocument()
    const linha5 = within(tabela).getByRole('list', { name: 'Erros da linha 5' })
    expect(
      within(linha5)
        .getAllByRole('listitem')
        .map((i) => i.textContent),
    ).toEqual([
      'E-mail corporativo: Este e-mail aparece também na linha 2.',
      'Cargo: O cargo "Analista de compras (novo)" não existe. Use um cargo da aba Valores válidos.',
      'Setor: O setor "Marketing" não existe. Use um setor da aba Valores válidos.',
    ])
    expect(within(tabela).getByText('123.456.789-00')).toHaveAttribute('data-erro', 'true')
    expect(within(tabela).getByText('Mariana Teixeira')).not.toHaveAttribute('data-erro')
    expect(within(tabela).getAllByText('(vazio)').length).toBeGreaterThan(0)
    expect(screen.getByRole('button', { name: 'Cadastrar 2 colaboradores' })).toBeDisabled()
    expect(
      screen.getByText('Só dá para cadastrar quando todas as linhas estão certas.'),
    ).toBeInTheDocument()
    expect(screen.queryByText('Depois de salvar')).toBeNull()
    await tela.user.click(screen.getByRole('switch', { name: 'Mostrar só as linhas com erro' }))
    expect(within(tabela).getByText('Tiago Nogueira')).toBeInTheDocument()
    expect(within(tabela).getAllByText('OK')).toHaveLength(2)
    await tela.user.click(screen.getByRole('button', { name: 'Enviar outra planilha' }))
    expect(screen.getByLabelText('Escolher arquivo')).toBeInTheDocument()
  })

  it('enviando mostra o nome do arquivo e "Lendo a planilha..." com o painel travado', async () => {
    let soltar: (p: Previa) => void = () => {}
    vi.mocked(validarPlanilha).mockImplementation(() => new Promise<Previa>((r) => (soltar = r)))
    const tela = await abrirImportacao()
    await enviarArquivo(tela, planilha('lote.xlsx'))
    expect(await screen.findByText('Lendo a planilha...')).toBeInTheDocument()
    expect(screen.getByText('lote.xlsx')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Baixar modelo' })).toBeDisabled()
    soltar(PRONTA)
    expect(await screen.findByRole('table', { name: 'Prévia da planilha' })).toBeInTheDocument()
  })

  it('extensao errada ou mais de 5 MB avisa antes de enviar', async () => {
    await abrirImportacao()
    const semAccept = userEvent.setup({ applyAccept: false })
    await semAccept.upload(screen.getByLabelText('Escolher arquivo'), planilha('a.csv', 'text/csv'))
    expect(await screen.findByText('Esse tipo de arquivo não é aceito.')).toBeInTheDocument()
    await semAccept.upload(
      screen.getByLabelText('Escolher arquivo'),
      planilha('grande.xlsx', TIPO_XLSX, 5 * 1024 * 1024 + 1),
    )
    expect(await screen.findByText('O arquivo passa de 5 MB.')).toBeInTheDocument()
    expect(validarPlanilha).not.toHaveBeenCalled()
  })

  it.each([
    ['naoEExcel', 'O arquivo não é uma planilha do Excel (.xlsx). Baixe o modelo e preencha nele.'],
    ['limite', 'A planilha tem 501 linhas. O limite é de 500 por importação.'],
  ])('422 %s do arquivo inteiro mostra o detail e volta a escolher', async (nome, detail) => {
    vi.mocked(validarPlanilha).mockRejectedValue(
      erroDoContrato('validarPlanilhaDeImportacao', 422, nome),
    )
    const tela = await abrirImportacao()
    await enviarArquivo(tela)
    expect(await screen.findByText(detail)).toBeInTheDocument()
    expect(screen.getByLabelText('Escolher arquivo')).toBeInTheDocument()
    expect(screen.queryByRole('table', { name: 'Prévia da planilha' })).toBeNull()
  })

  it('400 em arquivo mostra a mensagem junto da area de envio', async () => {
    vi.mocked(validarPlanilha).mockRejectedValue(
      new ErroDeApi(400, 'x', { errors: { arquivo: ['Envie a planilha.'] } }),
    )
    const tela = await abrirImportacao()
    await enviarArquivo(tela)
    expect(await screen.findByText('Envie a planilha.')).toBeInTheDocument()
  })

  it('confirmar envia { enviarConvite, incluirNoOnboarding: false, formularioAdmissaoId: null } no id 16 e mostra o resultado como veio', async () => {
    vi.mocked(validarPlanilha).mockResolvedValue(PRONTA)
    const confirmar = capturar('confirmarImportacao')
    const lista = capturar('listarFuncionarios')
    const tela = await abrirImportacao()
    await enviarArquivo(tela)
    await screen.findByRole('table', { name: 'Prévia da planilha' })
    // O onboarding (ficha 6) e o formulário de admissão (ficha 5) ligaram; desmarcados, vão false e null.
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
    await tela.user.click(screen.getByText('Enviar convite de acesso ao portal'))
    const antes = lista.quantas()
    await tela.user.click(screen.getByRole('button', { name: 'Cadastrar 2 colaboradores' }))
    expect(await screen.findByText('2 colaboradores cadastrados.')).toBeInTheDocument()
    expect(confirmar.quantas()).toBe(1)
    expect(confirmar.ultima().parametrosDoCaminho).toEqual({ importacaoId: '16' })
    expect(confirmar.ultima().corpo).toEqual({
      enviarConvite: false,
      incluirNoOnboarding: false,
      formularioAdmissaoId: null,
    })
    confirmar.conforme()
    for (const rotulo of [
      'Convites na fila',
      'Incluídos no onboarding',
      'Incluídos no formulário de admissão',
    ]) {
      expect(screen.getByText(rotulo).nextElementSibling).toHaveTextContent('2')
    }
    // 12:41:44 UTC = 09:41 em São Paulo
    expect(screen.getByText('07/10/2026 09:41')).toBeInTheDocument()
    const pessoas = screen.getByRole('list', { name: 'Colaboradores cadastrados' })
    expect(within(pessoas).getByText('Tiago Nogueira')).toBeInTheDocument()
    expect(within(pessoas).getByText('Aline Carvalho')).toBeInTheDocument()
    expect(
      within(pessoas).getByRole('link', { name: 'Abrir ficha de Tiago Nogueira' }),
    ).toHaveAttribute('href', '/rh/colaboradores/105')
    expect(
      within(pessoas).getByRole('link', { name: 'Abrir ficha de Aline Carvalho' }),
    ).toHaveAttribute('href', '/rh/colaboradores/202')
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(antes))
    await tela.user.click(screen.getByRole('button', { name: 'Importar outra planilha' }))
    expect(screen.getByLabelText('Escolher arquivo')).toBeInTheDocument()
  })

  it('Abrir ficha leva a ficha da pessoa cadastrada', async () => {
    vi.mocked(validarPlanilha).mockResolvedValue(PRONTA)
    const tela = await abrirImportacao()
    await enviarArquivo(tela)
    await tela.user.click(await screen.findByRole('button', { name: 'Cadastrar 2 colaboradores' }))
    await tela.user.click(
      await screen.findByRole('link', { name: 'Abrir ficha de Tiago Nogueira' }),
    )
    await waitFor(() => expect(tela.roteador.state.location.pathname).toBe('/rh/colaboradores/105'))
  })

  it('clique duplo em Cadastrar confirma uma vez so', async () => {
    vi.mocked(validarPlanilha).mockResolvedValue(PRONTA)
    const confirmar = capturar('confirmarImportacao')
    const tela = await abrirImportacao()
    await enviarArquivo(tela)
    const botao = await screen.findByRole('button', { name: 'Cadastrar 2 colaboradores' })
    await tela.user.dblClick(botao)
    expect(await screen.findByText('2 colaboradores cadastrados.')).toBeInTheDocument()
    expect(confirmar.quantas()).toBe(1)
  })

  it('409 IMPORTACAO_JA_CONFIRMADA mostra o detail, nada e dado como cadastrado e so resta Fechar', async () => {
    vi.mocked(validarPlanilha).mockResolvedValue(PRONTA)
    capturar(
      'confirmarImportacao',
      problema(409, exemploDoContrato('confirmarImportacao', 409) as Record<string, unknown>),
    )
    const lista = capturar('listarFuncionarios')
    const tela = await abrirImportacao()
    await enviarArquivo(tela)
    await screen.findByRole('table', { name: 'Prévia da planilha' })
    const antes = lista.quantas()
    await tela.user.click(screen.getByRole('button', { name: 'Cadastrar 2 colaboradores' }))
    expect(await screen.findByText('Esta importação já foi confirmada.')).toBeInTheDocument()
    expect(screen.queryByText(/colaboradores cadastrados/)).toBeNull()
    expect(screen.queryByRole('button', { name: /^Cadastrar/ })).toBeNull()
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(antes))
    await tela.user.click(screen.getByText('Fechar', { selector: 'button' }))
    await waitFor(() => expect(tela.roteador.state.location.pathname).toBe('/rh/colaboradores'))
  })

  it.each([
    ['linhaRecusada', /^A linha 12 foi recusada/],
    ['expirada', /^A prévia venceu/],
    ['comErros', /^A planilha ainda tem linhas com erro/],
  ])(
    '422 %s ao confirmar mostra o detail, volta a escolher e nada e cadastrado',
    async (nome, texto) => {
      vi.mocked(validarPlanilha).mockResolvedValue(PRONTA)
      capturar(
        'confirmarImportacao',
        problema(
          422,
          exemploDoContrato('confirmarImportacao', 422, nome) as Record<string, unknown>,
        ),
      )
      const tela = await abrirImportacao()
      await enviarArquivo(tela)
      await tela.user.click(
        await screen.findByRole('button', { name: 'Cadastrar 2 colaboradores' }),
      )
      expect(await screen.findByText(texto)).toBeInTheDocument()
      expect(screen.getByLabelText('Escolher arquivo')).toBeInTheDocument()
      expect(screen.queryByText(/colaboradores cadastrados/)).toBeNull()
    },
  )

  it('500 ao confirmar mantem a previa e avisa', async () => {
    vi.mocked(validarPlanilha).mockResolvedValue(PRONTA)
    capturar('confirmarImportacao', problema(500))
    const tela = await abrirImportacao()
    await enviarArquivo(tela)
    await tela.user.click(await screen.findByRole('button', { name: 'Cadastrar 2 colaboradores' }))
    expect(
      await screen.findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('table', { name: 'Prévia da planilha' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Cadastrar 2 colaboradores' })).toBeEnabled()
  })

  it('valores de usuario aparecem como texto', async () => {
    vi.mocked(validarPlanilha).mockResolvedValue({
      ...PRONTA,
      linhas: [
        {
          ...PRONTA.linhas[0]!,
          valores: { ...PRONTA.linhas[0]!.valores, nome: '<img src=x onerror="window.__x=1">' },
        },
      ],
    })
    const tela = await abrirImportacao()
    await enviarArquivo(tela)
    expect(await screen.findByText('<img src=x onerror="window.__x=1">')).toBeInTheDocument()
    expect((window as { __x?: number }).__x).toBeUndefined()
  })

  it.each(['Gestor', 'Funcionario'] as const)(
    '%s nao ve a alternancia nem entra na tela',
    async (perfil) => {
      renderizarTela('/rh/colaboradores/novo', { perfil })
      expect(
        await screen.findByText('Você não tem permissão para ver esta página.'),
      ).toBeInTheDocument()
      expect(screen.queryByRole('tab', { name: 'Importar planilha' })).toBeNull()
    },
  )
})

describe('Formulario de admissao na importacao (ficha 5, bloco 9)', { timeout: 20000 }, () => {
  it('confirma com formularioAdmissaoId: 11 e mostra "Incluidos no formulario de admissao" como veio', async () => {
    vi.mocked(validarPlanilha).mockResolvedValue(PRONTA)
    const confirmar = capturar('confirmarImportacao')
    const tela = await abrirImportacao()
    await enviarArquivo(tela)
    await screen.findByRole('table', { name: 'Prévia da planilha' })
    const caixa = screen.getByRole('checkbox', { name: /Enviar o formulário de admissão/ })
    await waitFor(() => expect(caixa).not.toHaveAttribute('aria-disabled', 'true'))
    await tela.user.click(caixa)
    await tela.user.click(screen.getByRole('button', { name: 'Cadastrar 2 colaboradores' }))
    expect(await screen.findByText('2 colaboradores cadastrados.')).toBeInTheDocument()
    expect(confirmar.ultima().corpo).toMatchObject({ formularioAdmissaoId: 11 })
    confirmar.conforme()
    const resultado = exemploDoContrato<{ incluidosNoFormularioDeAdmissao: number }>(
      'confirmarImportacao',
    )
    const numero = screen.getByText('Incluídos no formulário de admissão').parentElement!
    expect(numero).toHaveTextContent(String(resultado.incluidosNoFormularioDeAdmissao))
  })

  it('422 formularioEncerrado mostra o detail', async () => {
    vi.mocked(validarPlanilha).mockResolvedValue(PRONTA)
    capturar(
      'confirmarImportacao',
      problema(
        422,
        exemploDoContrato<Record<string, unknown>>(
          'confirmarImportacao',
          422,
          'formularioEncerrado',
        ),
      ),
    )
    const tela = await abrirImportacao()
    await enviarArquivo(tela)
    await screen.findByRole('table', { name: 'Prévia da planilha' })
    const caixa = screen.getByRole('checkbox', { name: /Enviar o formulário de admissão/ })
    await waitFor(() => expect(caixa).not.toHaveAttribute('aria-disabled', 'true'))
    await tela.user.click(caixa)
    await tela.user.click(screen.getByRole('button', { name: 'Cadastrar 2 colaboradores' }))
    const detail = exemploDoContrato<{ detail: string }>(
      'confirmarImportacao',
      422,
      'formularioEncerrado',
    ).detail
    expect(await screen.findByText(detail)).toBeInTheDocument()
  })
})

describe('Onboarding na importacao (ficha 6, bloco 7)', { timeout: 20000 }, () => {
  it('confirma com incluirNoOnboarding: true e mostra "Incluidos no onboarding" como veio; 422 semOnboarding mostra o detail', async () => {
    vi.mocked(validarPlanilha).mockResolvedValue(PRONTA)
    const confirmar = capturar('confirmarImportacao')
    const tela = await abrirImportacao()
    await enviarArquivo(tela)
    await screen.findByRole('table', { name: 'Prévia da planilha' })
    const caixa = await screen.findByRole('checkbox', {
      name: /Incluir no treinamento Onboarding 2026/,
    })
    await waitFor(() => expect(caixa).not.toHaveAttribute('aria-disabled', 'true'))
    await tela.user.click(caixa)
    await tela.user.click(screen.getByRole('button', { name: 'Cadastrar 2 colaboradores' }))
    expect(await screen.findByText('2 colaboradores cadastrados.')).toBeInTheDocument()
    expect(confirmar.ultima().corpo).toMatchObject({ incluirNoOnboarding: true })
    confirmar.conforme()
    const resultado = exemploDoContrato<{ incluidosNoOnboarding: number }>('confirmarImportacao')
    expect(screen.getByText('Incluídos no onboarding').parentElement!).toHaveTextContent(
      String(resultado.incluidosNoOnboarding),
    )
  })

  it('422 semOnboarding mostra o detail', async () => {
    vi.mocked(validarPlanilha).mockResolvedValue(PRONTA)
    capturar(
      'confirmarImportacao',
      problema(
        422,
        exemploDoContrato<Record<string, unknown>>('confirmarImportacao', 422, 'semOnboarding'),
      ),
    )
    const tela = await abrirImportacao()
    await enviarArquivo(tela)
    await screen.findByRole('table', { name: 'Prévia da planilha' })
    const caixa = await screen.findByRole('checkbox', {
      name: /Incluir no treinamento Onboarding 2026/,
    })
    await waitFor(() => expect(caixa).not.toHaveAttribute('aria-disabled', 'true'))
    await tela.user.click(caixa)
    await tela.user.click(screen.getByRole('button', { name: 'Cadastrar 2 colaboradores' }))
    const detail = exemploDoContrato<{ detail: string }>(
      'confirmarImportacao',
      422,
      'semOnboarding',
    ).detail
    expect(await screen.findByText(detail)).toBeInTheDocument()
  })
})
