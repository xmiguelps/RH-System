import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import type { Esquema } from '../../shared/api/tipos'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar } from '../../test/capturar'
import { escolherOpcao, preencherData } from '../../test/formulario'
import { renderizarTela } from '../../test/telas'
import { enviarHolerite, enviarHoleritesEmLote, substituirHolerite } from './enviarHolerite'

vi.mock('./enviarHolerite', () => ({
  enviarHolerite: vi.fn(),
  substituirHolerite: vi.fn(),
  enviarHoleritesEmLote: vi.fn(),
}))

type HoleriteRh = Esquema<'HoleriteItemRh'>
const CRIADO = exemploDoContrato<HoleriteRh>('enviarHolerite', 201)
const NOVA_VERSAO = exemploDoContrato<HoleriteRh>('substituirHolerite', 201)
const LOTE = exemploDoContrato<Esquema<'HoleriteLoteResultado'>>('enviarHoleritesEmLote')
const LISTA_RH = exemploDoContrato<Esquema<'HoleriteListaRh'>>('listarHoleritesDoFuncionario')

const pdf = (nome = 'holerite.pdf', bytes = 4) =>
  new File(['%'.repeat(bytes)], nome, { type: 'application/pdf' })

function erroDoContrato(op: string, status: number, nome?: string): ErroDeApi {
  const e = exemploDoContrato<{ title: string; detail: string; codigo?: string }>(op, status, nome)
  return new ErroDeApi(status, e.title, { detail: e.detail, codigo: e.codigo })
}

let cliques: string[] = []

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
  cliques = []
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
    this: HTMLAnchorElement,
  ) {
    cliques.push(this.href)
  })
  vi.mocked(enviarHolerite).mockResolvedValue(CRIADO)
  vi.mocked(substituirHolerite).mockResolvedValue(NOVA_VERSAO)
  vi.mocked(enviarHoleritesEmLote).mockResolvedValue(LOTE)
})

afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
  vi.clearAllMocks()
})

async function abrirMeusHolerites(perfil: 'Funcionario' | 'Gestor' | 'RH' = 'Funcionario') {
  const tela = renderizarTela('/eu/holerites', { perfil })
  await screen.findByRole('heading', { level: 1, name: 'Holerites' })
  return tela
}

describe('Holerites da pessoa (telas 36 e 49)', { timeout: 15000 }, () => {
  it.each(['Funcionario', 'Gestor'] as const)(
    '%s: abas com contagens, 10 holerites em 2026 e o cartao "Proximo pagamento"',
    async (perfil) => {
      const lista = capturar('listarMeusHolerites')
      const proximo = capturar('obterProximoPagamento')
      await abrirMeusHolerites(perfil)
      const tabela = await screen.findByRole('table', { name: 'Holerites' })
      expect(await within(tabela).findByText('Setembro de 2026')).toBeInTheDocument()
      expect(within(tabela).getByText('Férias de janeiro de 2026')).toBeInTheDocument()
      expect(within(tabela).getByText('05/10/2026')).toBeInTheDocument()
      for (const nome of ['Todos 10', 'Mensais 9', 'Férias 1', '13º salário 0'])
        expect(screen.getByRole('tab', { name: nome })).toBeInTheDocument()
      expect(screen.getByText('10 holerites em 2026')).toBeInTheDocument()
      const cartao = await screen.findByRole('region', { name: 'Próximo pagamento' })
      expect(within(cartao).getByText('Até 6 de novembro')).toBeInTheDocument()
      expect(
        within(cartao).getByText(
          'O holerite de outubro aparece aqui quando o RH publicar. Você também recebe um e-mail.',
        ),
      ).toBeInTheDocument()
      expect(within(cartao).getByText('Dúvida sobre um valor? Fale com o RH')).toBeInTheDocument()
      expect(within(cartao).queryByRole('link')).toBeNull()
      lista.conforme()
      proximo.conforme()
    },
  )

  it('aba e ano vao para listarMeusHolerites (tipo e ano), conforme', async () => {
    const lista = capturar('listarMeusHolerites')
    const { user } = await abrirMeusHolerites()
    await screen.findByText('Setembro de 2026')
    await user.click(screen.getByRole('tab', { name: 'Mensais 9' }))
    await waitFor(() => expect(lista.ultima().consulta.get('tipo')).toBe('MENSAL'))
    await escolherOpcao(user, 'Ano', '2025')
    await waitFor(() => expect(lista.ultima().consulta.get('ano')).toBe('2025'))
    expect(lista.ultima().consulta.get('tipo')).toBe('MENSAL')
    lista.conforme()
  })

  it('"Ver" e "Baixar PDF" pedem o link no clique e o abrem', async () => {
    const link = capturar('obterLinkDeDownloadDoMeuHolerite')
    const { user } = await abrirMeusHolerites()
    await user.click(await screen.findByRole('button', { name: 'Ver Setembro de 2026' }))
    await waitFor(() => expect(cliques).toHaveLength(1))
    await user.click(screen.getByRole('button', { name: 'Baixar PDF de Setembro de 2026' }))
    await waitFor(() => expect(cliques).toHaveLength(2))
    expect(link.quantas()).toBe(2)
    expect(link.ultima().parametrosDoCaminho).toEqual({ holeriteId: '5121' })
    expect(cliques[0]).toMatch(/^https:\/\/arquivos\.exemplo\.com\.br\/download\//)
    link.conforme()
  })

  it('sem dia de pagamento configurado o cartao nao aparece', async () => {
    capturar('obterProximoPagamento', { corpo: { pagamento: null, competencia: null } })
    await abrirMeusHolerites()
    await screen.findByText('Setembro de 2026')
    expect(screen.queryByRole('region', { name: 'Próximo pagamento' })).toBeNull()
  })

  it('ano sem holerites: "Voce ainda nao tem holerites em 2025."', async () => {
    capturar('listarMeusHolerites', {
      corpo: {
        ...exemploDoContrato<Esquema<'HoleriteListaEu'>>('listarMeusHolerites'),
        ano: 2025,
        total: 0,
        itens: [],
        contagens: { todos: 0, mensais: 0, ferias: 0, decimoTerceiro: 0 },
      },
    })
    await abrirMeusHolerites()
    expect(await screen.findByText('Você ainda não tem holerites em 2025.')).toBeInTheDocument()
  })
})

async function abrirAbaHolerites() {
  const tela = renderizarTela('/rh/colaboradores/12?aba=holerites')
  await screen.findByText('Bruno Lima', { selector: 'p' })
  const tabela = await screen.findByRole('table', { name: 'Holerites' })
  await within(tabela).findByText('Setembro de 2026')
  return { ...tela, tabela }
}

describe('Aba Holerites da ficha (tela 07) e "Substituir"', { timeout: 15000 }, () => {
  it('lista as versoes atuais, baixa o PDF pelo link e "Ver versoes anteriores" pede todasAsVersoes', async () => {
    const lista = capturar('listarHoleritesDoFuncionario')
    const link = capturar('obterLinkDeDownloadDoHolerite')
    const { user, tabela } = await abrirAbaHolerites()
    expect(
      screen.getByText('O colaborador recebe um e-mail sempre que um holerite novo é publicado.'),
    ).toBeInTheDocument()
    expect(within(tabela).getAllByText('Mensal')).toHaveLength(2)
    expect(lista.ultima().consulta.has('todasAsVersoes')).toBe(false)
    lista.conforme()
    await user.click(within(tabela).getByRole('button', { name: 'Baixar PDF de Setembro de 2026' }))
    await waitFor(() => expect(cliques).toHaveLength(1))
    expect(link.ultima().parametrosDoCaminho).toEqual({ holeriteId: '5121' })
    link.conforme()
    await user.click(screen.getByRole('button', { name: 'Ver versões anteriores' }))
    await waitFor(() => expect(lista.ultima().consulta.get('todasAsVersoes')).toBe('true'))
    lista.conforme()
  })

  it('holerite agendado mostra o chip e "Agendado para"; versao 2 mostra "Versao 2"', async () => {
    capturar('listarHoleritesDoFuncionario', {
      corpo: {
        ...LISTA_RH,
        itens: [{ ...CRIADO }, { ...LISTA_RH.itens[0]!, id: 5240, versao: 2 }],
      } satisfies Esquema<'HoleriteListaRh'>,
    })
    renderizarTela('/rh/colaboradores/12?aba=holerites')
    const tabela = await screen.findByRole('table', { name: 'Holerites' })
    expect(await within(tabela).findByText('Agendado para 06/11/2026')).toBeInTheDocument()
    expect(within(tabela).getByText('Agendado')).toBeInTheDocument()
    expect(within(tabela).getByText('Versão 2')).toBeInTheDocument()
  })

  it('"Substituir" envia a nova versao; 409 HOLERITE_VERSAO_DESATUALIZADA mostra o detail', async () => {
    const { user, tabela } = await abrirAbaHolerites()
    await user.click(within(tabela).getByRole('button', { name: 'Substituir Setembro de 2026' }))
    const modal = await screen.findByRole('dialog', {
      name: 'Substituir holerite de Setembro de 2026',
    })
    await user.upload(within(modal).getByLabelText('Escolher arquivo'), pdf('corrigido.pdf'))
    await user.click(within(modal).getByRole('button', { name: 'Substituir holerite' }))
    await waitFor(() => expect(substituirHolerite).toHaveBeenCalledTimes(1))
    expect(vi.mocked(substituirHolerite).mock.calls[0]![0]).toBe(5121)
    expect(vi.mocked(substituirHolerite).mock.calls[0]![1]).toEqual({
      arquivo: expect.objectContaining({ name: 'corrigido.pdf' }),
    })
    expect(await screen.findByText('Holerite publicado.')).toBeInTheDocument()

    vi.mocked(substituirHolerite).mockRejectedValueOnce(erroDoContrato('substituirHolerite', 409))
    await user.click(within(tabela).getByRole('button', { name: 'Substituir Setembro de 2026' }))
    const outro = await screen.findByRole('dialog', {
      name: 'Substituir holerite de Setembro de 2026',
    })
    await user.upload(within(outro).getByLabelText('Escolher arquivo'), pdf())
    await user.click(within(outro).getByRole('button', { name: 'Substituir holerite' }))
    expect(
      await within(outro).findByText(
        'Este holerite já foi substituído por uma versão mais nova. Atualize a lista.',
      ),
    ).toBeInTheDocument()
  })
})

async function abrirEnvio() {
  const tela = await abrirAbaHolerites()
  await tela.user.click(screen.getByRole('button', { name: 'Enviar holerite' }))
  const modal = await screen.findByRole('dialog', { name: 'Enviar holerite de Bruno' })
  return { ...tela, modal }
}

describe('Enviar holerite (tela 08)', { timeout: 15000 }, () => {
  it('mostra o ultimo enviado; "Publicar agora" nao manda publicarEm nem noDiaDoPagamento', async () => {
    const { user, modal } = await abrirEnvio()
    expect(
      within(modal).getByText(
        'Último holerite enviado: Setembro de 2026, publicado em 05/10/2026.',
      ),
    ).toBeInTheDocument()
    await user.click(within(modal).getByRole('button', { name: 'Publicar holerite' }))
    expect(await within(modal).findByText('Escolha o PDF do holerite.')).toBeInTheDocument()
    expect(enviarHolerite).not.toHaveBeenCalled()
    await user.upload(within(modal).getByLabelText('Escolher arquivo'), pdf())
    await user.click(within(modal).getByRole('button', { name: 'Publicar holerite' }))
    await waitFor(() => expect(enviarHolerite).toHaveBeenCalledTimes(1))
    expect(enviarHolerite).toHaveBeenCalledWith(12, {
      mes: 10,
      ano: 2026,
      tipo: 'MENSAL',
      arquivo: expect.objectContaining({ name: 'holerite.pdf' }),
    })
    expect(await screen.findByText('Holerite agendado para 06/11/2026.')).toBeInTheDocument()
  })

  it('"Agendar para o dia do pagamento" mostra a data e envia noDiaDoPagamento: true', async () => {
    const { user, modal } = await abrirEnvio()
    const opcao = await within(modal).findByRole('radio', {
      name: /Agendar para o dia do pagamento \(06\/11\/2026\)/,
    })
    await user.click(opcao)
    await user.upload(within(modal).getByLabelText('Escolher arquivo'), pdf())
    await user.click(within(modal).getByRole('button', { name: 'Agendar holerite' }))
    await waitFor(() => expect(enviarHolerite).toHaveBeenCalledTimes(1))
    const campos = vi.mocked(enviarHolerite).mock.calls[0]![1]
    expect(campos.noDiaDoPagamento).toBe(true)
    expect(campos).not.toHaveProperty('publicarEm')
  })

  it('"Agendar para uma data" envia publicarEm (nunca junto com noDiaDoPagamento)', async () => {
    const { user, modal } = await abrirEnvio()
    await user.click(within(modal).getByRole('radio', { name: 'Agendar para uma data' }))
    preencherData('Publicar no dia', '2026-11-06')
    await user.upload(within(modal).getByLabelText('Escolher arquivo'), pdf())
    await user.click(within(modal).getByRole('button', { name: 'Agendar holerite' }))
    await waitFor(() => expect(enviarHolerite).toHaveBeenCalledTimes(1))
    const campos = vi.mocked(enviarHolerite).mock.calls[0]![1]
    expect(campos.publicarEm).toBe('2026-11-06')
    expect(campos).not.toHaveProperty('noDiaDoPagamento')
  })

  it('sem dia de pagamento configurado a opcao fica desligada com a explicacao', async () => {
    capturar('obterProximoPagamento', { corpo: { pagamento: null, competencia: null } })
    const { modal } = await abrirEnvio()
    const opcao = await within(modal).findByRole('radio', {
      name: /Agendar para o dia do pagamento/,
    })
    await waitFor(() => expect(opcao).toHaveAttribute('aria-disabled', 'true'))
    expect(
      within(modal).getByText('Configure o dia do pagamento em Cadastros, Empresa.'),
    ).toBeInTheDocument()
  })

  it('so PDF ate 5 MB: o aviso vem antes de enviar', async () => {
    const { modal } = await abrirEnvio()
    const semAccept = userEvent.setup({ applyAccept: false })
    await semAccept.upload(
      within(modal).getByLabelText('Escolher arquivo'),
      new File(['x'], 'foto.png', { type: 'image/png' }),
    )
    expect(await within(modal).findByText('Esse tipo de arquivo não é aceito.')).toBeInTheDocument()
    await semAccept.upload(
      within(modal).getByLabelText('Escolher arquivo'),
      pdf('grande.pdf', 5 * 1024 * 1024 + 1),
    )
    expect(await within(modal).findByText('O arquivo passa de 5 MB.')).toBeInTheDocument()
    expect(enviarHolerite).not.toHaveBeenCalled()
  })

  it('400 do servidor no campo arquivo aparece embaixo do arquivo', async () => {
    vi.mocked(enviarHolerite).mockRejectedValueOnce(
      new ErroDeApi(400, 'Dados inválidos.', {
        errors: { arquivo: ['O arquivo precisa ser um PDF.'] },
      }),
    )
    const { user, modal } = await abrirEnvio()
    await user.upload(within(modal).getByLabelText('Escolher arquivo'), pdf())
    await user.click(within(modal).getByRole('button', { name: 'Publicar holerite' }))
    expect(await within(modal).findByText('O arquivo precisa ser um PDF.')).toBeInTheDocument()
  })

  it('409 HOLERITE_JA_EXISTE mostra o detail e "Substituir" abre a nova versao daquele holerite', async () => {
    vi.mocked(enviarHolerite).mockRejectedValueOnce(erroDoContrato('enviarHolerite', 409))
    const { user, modal } = await abrirEnvio()
    await escolherOpcao(user, 'Mês', 'Setembro')
    await user.upload(within(modal).getByLabelText('Escolher arquivo'), pdf())
    await user.click(within(modal).getByRole('button', { name: 'Publicar holerite' }))
    expect(
      await within(modal).findByText(
        'Já existe um holerite mensal de outubro de 2026 para esta pessoa. Para corrigir, envie uma nova versão.',
      ),
    ).toBeInTheDocument()
    await user.click(within(modal).getByRole('button', { name: 'Substituir' }))
    expect(
      await screen.findByRole('dialog', { name: 'Substituir holerite de Setembro de 2026' }),
    ).toBeInTheDocument()
  })

  it.each([
    ['empresaSemDiaDePagamento', 'A empresa ainda não tem o dia do pagamento configurado.'],
    ['competenciaFutura', 'Só o holerite de férias pode ter competência futura.'],
    ['anteriorAAdmissao', 'A competência do holerite é anterior à admissão da pessoa.'],
  ])('422 %s mostra o detail', async (nome, trecho) => {
    vi.mocked(enviarHolerite).mockRejectedValueOnce(erroDoContrato('enviarHolerite', 422, nome))
    const { user, modal } = await abrirEnvio()
    await user.upload(within(modal).getByLabelText('Escolher arquivo'), pdf())
    await user.click(within(modal).getByRole('button', { name: 'Publicar holerite' }))
    expect(await within(modal).findByText(new RegExp(trecho))).toBeInTheDocument()
  })

  it('competencia futura com tipo que nao e Ferias avisa antes (quem decide e a API)', async () => {
    const { user, modal } = await abrirEnvio()
    await escolherOpcao(user, 'Mês', 'Dezembro')
    expect(
      within(modal).getByText('Só o holerite de Férias pode ter competência futura.'),
    ).toBeInTheDocument()
    await escolherOpcao(user, 'Tipo', 'Férias')
    expect(
      within(modal).queryByText('Só o holerite de Férias pode ter competência futura.'),
    ).toBeNull()
  })
})

describe('Enviar holerites em lote (sem print)', { timeout: 20000 }, () => {
  it('um PDF por pessoa; o botao so liga com todos ligados; o resultado vem por item', async () => {
    const { user } = renderizarTela('/rh/colaboradores')
    await user.click(await screen.findByRole('button', { name: 'Enviar holerites em lote' }))
    const modal = await screen.findByRole('dialog', { name: 'Enviar holerites em lote' })
    expect(within(modal).getByText('Adicione os PDFs para começar.')).toBeInTheDocument()
    await user.upload(within(modal).getByLabelText('Escolher arquivos'), [
      pdf('bruno-lima.pdf'),
      pdf('carla-mendes.pdf'),
    ])
    const enviar = within(modal).getByRole('button', { name: 'Publicar 2 holerites' })
    expect(enviar).toBeDisabled()
    await user.type(
      within(modal).getByRole('combobox', { name: 'Colaborador de bruno-lima.pdf' }),
      'Bru',
    )
    await user.click(await screen.findByRole('option', { name: /Bruno Lima/ }))
    expect(enviar).toBeDisabled()
    await user.type(
      within(modal).getByRole('combobox', { name: 'Colaborador de carla-mendes.pdf' }),
      'Car',
    )
    await user.click(await screen.findByRole('option', { name: /Carla Mendes/ }))
    await waitFor(() => expect(enviar).toBeEnabled())
    await user.click(enviar)
    await waitFor(() => expect(enviarHoleritesEmLote).toHaveBeenCalledTimes(1))
    expect(enviarHoleritesEmLote).toHaveBeenCalledWith({
      mes: 10,
      ano: 2026,
      tipo: 'MENSAL',
      itens: [
        { funcionarioId: 12, arquivo: expect.objectContaining({ name: 'bruno-lima.pdf' }) },
        { funcionarioId: 4, arquivo: expect.objectContaining({ name: 'carla-mendes.pdf' }) },
      ],
    })
    expect(await within(modal).findByText('2 enviados, 1 com erro')).toBeInTheDocument()
    const resultado = within(modal).getByRole('table', { name: 'Resultado do envio' })
    expect(within(resultado).getAllByText('Agendado para 06/11/2026')).toHaveLength(2)
    expect(
      within(resultado).getByText(
        'Já existe um holerite mensal de outubro de 2026 para esta pessoa.',
      ),
    ).toBeInTheDocument()
    expect(within(resultado).getByRole('link', { name: 'Abrir a ficha' })).toHaveAttribute(
      'href',
      '/rh/colaboradores/24?aba=holerites',
    )
  })

  it('arquivo que nao e PDF e nome repetido sao marcados e travam o envio', async () => {
    const { user } = renderizarTela('/rh/colaboradores')
    await user.click(await screen.findByRole('button', { name: 'Enviar holerites em lote' }))
    const modal = await screen.findByRole('dialog', { name: 'Enviar holerites em lote' })
    const semAccept = userEvent.setup({ applyAccept: false })
    await semAccept.upload(within(modal).getByLabelText('Escolher arquivos'), [
      pdf('a.pdf'),
      pdf('a.pdf'),
      new File(['x'], 'b.png', { type: 'image/png' }),
    ])
    expect(within(modal).getAllByText('Nome de arquivo repetido.')).toHaveLength(2)
    expect(within(modal).getByText('Não é PDF.')).toBeInTheDocument()
    await user.click(within(modal).getByRole('button', { name: 'Remover b.png' }))
    expect(within(modal).queryByText('Não é PDF.')).toBeNull()
    expect(within(modal).getByRole('button', { name: 'Publicar 2 holerites' })).toBeDisabled()
  })
})
