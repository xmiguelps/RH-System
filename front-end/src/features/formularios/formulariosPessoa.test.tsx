import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import type { Esquema } from '../../shared/api/tipos'
import { servidor } from '../../shared/mocks/servidor'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { renderizarTela } from '../../test/telas'
import { responderFormulario, responderFormularioPublico } from './enviarRespostas'

vi.mock('./enviarRespostas', () => ({
  responderFormulario: vi.fn(),
  responderFormularioPublico: vi.fn(),
}))

type ParaResponder = Esquema<'FormularioParaResponder'>
type Meus = Esquema<'FormularioMeusLista'>
const ex = <T,>(op: string, status?: number, nome?: string) =>
  exemploDoContrato<T>(op, status, nome)
const PARA_RESPONDER = ex<ParaResponder>('obterFormularioParaResponder')
const ABERTOS = ex<Meus>('listarMeusFormularios', 200, 'abertos')
const RESPONDIDOS = ex<Meus>('listarMeusFormularios', 200, 'respondidos')
const problemaDoContrato = (op: string, status: number, nome?: string) =>
  problema(status, ex<Record<string, unknown>>(op, status, nome))

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
  vi.mocked(responderFormulario).mockResolvedValue(ex('responderFormulario', 201, 'identificado'))
  vi.mocked(responderFormularioPublico).mockResolvedValue(ex('responderFormularioPublico', 201))
})
afterEach(() => {
  vi.useRealTimers()
  vi.clearAllMocks()
})

async function abrirMeus(
  perfil: 'Funcionario' | 'Gestor' | 'RH' = 'Funcionario',
  rota = '/eu/formularios',
) {
  const tela = renderizarTela(rota, { perfil })
  await screen.findByRole('heading', { level: 1, name: 'Formulários' })
  return tela
}

describe('Meus formularios (telas 39 e 40)', { timeout: 20000 }, () => {
  it.each(['Funcionario', 'Gestor', 'RH'] as const)(
    '%s: abas com contagens; "Todos" nao manda situacao; cartao anonimo sem data; Sugestao "Disponivel" e "Sempre aberto"',
    async (perfil) => {
      const lista = capturar('listarMeusFormularios', { corpo: ABERTOS })
      await abrirMeus(perfil)
      await screen.findByRole('tab', { name: 'Todos 3' })
      for (const nome of ['Todos 3', 'Para responder 1', 'Disponíveis 2', 'Respondidos 2'])
        expect(screen.getByRole('tab', { name: nome })).toBeInTheDocument()
      expect(lista.ultima().consulta.has('situacao')).toBe(false)
      lista.conforme()
      const sugestao = await screen.findByRole('article', { name: 'Sugestões para o RH' })
      expect(within(sugestao).getByText('Disponível')).toBeInTheDocument()
      expect(within(sugestao).getByText('Sempre aberto')).toBeInTheDocument()
      const clima = screen.getByRole('article', { name: 'Pesquisa de clima 2026' })
      expect(within(clima).getByText('Aberta até 31/10')).toBeInTheDocument()
      expect(within(clima).queryByText(/Respondido em/)).toBeNull()
      const avaliacao = screen.getByRole('article', {
        name: 'Avaliação da sua gestora, ciclo 2026.2',
      })
      expect(within(avaliacao).getByText('Até 16/10')).toBeInTheDocument()
      expect(screen.queryByText(/agrupados por setor/)).toBeNull()
    },
  )

  it('aba Respondidos (40): situacao=RESPONDIDO; anonimo sem data e sem "Ver minhas respostas"', async () => {
    const lista = capturar('listarMeusFormularios', { corpo: RESPONDIDOS })
    await abrirMeus('Funcionario', '/eu/formularios?aba=respondidos')
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(0))
    expect(lista.ultima().consulta.get('situacao')).toBe('RESPONDIDO')
    lista.conforme()
    const anonimo = await screen.findByRole('article', {
      name: 'Avaliação da sua gestora, ciclo 2026.1',
    })
    expect(within(anonimo).queryByText(/Respondido em/)).toBeNull()
    expect(within(anonimo).queryByRole('link', { name: 'Ver minhas respostas' })).toBeNull()
    const admissao = screen.getByRole('article', { name: 'Admissão' })
    expect(within(admissao).getByText('Respondido em 10/03/2022')).toBeInTheDocument()
    expect(within(admissao).getByRole('link', { name: 'Ver minhas respostas' })).toHaveAttribute(
      'href',
      '/eu/formularios/2/minhas-respostas',
    )
  })

  it('tipo e busca vao para listarMeusFormularios, conforme', async () => {
    const lista = capturar('listarMeusFormularios', { corpo: ABERTOS })
    const { user } = await abrirMeus()
    await user.click(screen.getByRole('combobox', { name: 'Tipo' }))
    await user.click(await screen.findByRole('option', { name: 'Sugestão' }))
    await waitFor(() => expect(lista.ultima().consulta.get('tipo')).toBe('OUTRO'))
    await user.type(screen.getByRole('searchbox', { name: 'Buscar formulário' }), 'clima')
    await waitFor(() => expect(lista.ultima().consulta.get('busca')).toBe('clima'))
    lista.conforme()
  })

  it('"Minhas respostas" mostra as respostas e o arquivo so pelo nome (sem baixar)', async () => {
    const minhas = capturar('obterMinhasRespostas')
    capturar('obterFormularioParaResponder', {
      corpo: {
        ...PARA_RESPONDER,
        id: 2,
        tipo: 'ADMISSAO',
        sentidoAvaliacao: null,
        avaliados: [],
      } satisfies ParaResponder,
    })
    renderizarTela('/eu/formularios/2/minhas-respostas', { perfil: 'Funcionario' })
    expect(await screen.findByRole('list', { name: 'Respostas' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^Baixar / })).toBeNull()
    expect(minhas.ultima().consulta.has('avaliadoId')).toBe(false)
    minhas.conforme()
  })
})

async function abrirResponder(
  corpo: ParaResponder,
  perfil: 'Funcionario' | 'Gestor' = 'Funcionario',
) {
  capturar('obterFormularioParaResponder', { corpo })
  const tela = renderizarTela(`/eu/formularios/${corpo.id}/responder`, { perfil })
  await screen.findByRole('heading', { level: 1, name: corpo.titulo })
  return tela
}

const PESQUISA_ANONIMA: ParaResponder = {
  ...PARA_RESPONDER,
  id: 8,
  titulo: 'Pesquisa de clima 2026',
  tipo: 'PESQUISA',
  sentidoAvaliacao: null,
  anonimo: true,
  avaliados: [],
  perguntas: [
    {
      id: 801,
      ordem: 1,
      texto: 'Como você avalia o clima?',
      tipo: 'ESCALA',
      obrigatoria: true,
      alternativas: [],
    },
    {
      id: 802,
      ordem: 2,
      texto: 'Algum comentário?',
      tipo: 'RESPOSTA_LONGA',
      obrigatoria: false,
      alternativas: [],
    },
  ],
}

describe('Responder (sem print)', { timeout: 20000 }, () => {
  it('anonimo: aviso de anonimato, uma pergunta por vez, obrigatoria bloqueia, e nada vai para o localStorage', async () => {
    vi.mocked(responderFormulario).mockResolvedValue(ex('responderFormulario', 201, 'anonimo'))
    const guardar = vi.spyOn(Storage.prototype, 'setItem')
    const { user } = await abrirResponder(PESQUISA_ANONIMA)
    expect(
      screen.getAllByText(
        'Suas respostas são anônimas: o RH e o gestor não veem quem respondeu o quê.',
      ).length,
    ).toBeGreaterThan(0)
    expect(screen.getByText('Pergunta 1 de 2')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Próxima' }))
    expect(await screen.findByText('Responda esta pergunta para seguir.')).toBeInTheDocument()
    await user.click(screen.getByRole('radio', { name: '4' }))
    await user.click(screen.getByRole('button', { name: 'Próxima' }))
    expect(await screen.findByText('Pergunta 2 de 2')).toBeInTheDocument()
    await user.type(screen.getByRole('textbox'), 'Bom ambiente.')
    await user.click(screen.getByRole('button', { name: 'Enviar respostas' }))
    await waitFor(() => expect(responderFormulario).toHaveBeenCalledTimes(1))
    expect(responderFormulario).toHaveBeenCalledWith(8, {
      respostas: [
        { perguntaId: 801, valorEscala: 4 },
        { perguntaId: 802, texto: 'Bom ambiente.' },
      ],
      arquivos: [],
    })
    expect(await screen.findByText('Participação registrada.')).toBeInTheDocument()
    const chaves = guardar.mock.calls.map((c) => c[0])
    expect(chaves.filter((k) => k !== 'rh.refresh')).toEqual([])
  })

  it('avaliacao da equipe: escolhe a pessoa, envia avaliadoId e oferece "Avaliar a proxima pessoa"', async () => {
    vi.mocked(responderFormulario).mockResolvedValue({
      formularioId: 9,
      participacaoId: null,
      restantes: 7,
    })
    const { user } = await abrirResponder(PARA_RESPONDER, 'Gestor')
    await user.click(screen.getByRole('button', { name: 'Avaliar Diego Farias' }))
    expect(screen.getByText('Avaliando: Diego Farias')).toBeInTheDocument()
    await user.click(screen.getByRole('radio', { name: '5' }))
    await user.click(screen.getByRole('button', { name: 'Próxima' }))
    await user.click(screen.getByRole('radio', { name: 'Sim' }))
    await user.click(screen.getByRole('button', { name: 'Próxima' }))
    await user.click(screen.getByRole('button', { name: 'Enviar respostas' }))
    await waitFor(() => expect(responderFormulario).toHaveBeenCalledTimes(1))
    expect(vi.mocked(responderFormulario).mock.calls[0]![1]).toEqual({
      respostas: [
        { perguntaId: 301, valorEscala: 5 },
        { perguntaId: 302, alternativaIds: [411] },
      ],
      arquivos: [],
      avaliadoId: 21,
    })
    expect(
      await screen.findByRole('button', { name: 'Avaliar a próxima pessoa' }),
    ).toBeInTheDocument()
  })

  it('409 JA_RESPONDIDO mostra o detail e volta para a lista', async () => {
    const e = ex<{ title: string; detail: string; codigo: string }>(
      'responderFormulario',
      409,
      'jaRespondido',
    )
    vi.mocked(responderFormulario).mockRejectedValue(
      new ErroDeApi(409, e.title, { detail: e.detail, codigo: e.codigo }),
    )
    const { user, roteador } = await abrirResponder(PESQUISA_ANONIMA)
    await user.click(screen.getByRole('radio', { name: '3' }))
    await user.click(screen.getByRole('button', { name: 'Próxima' }))
    await user.click(screen.getByRole('button', { name: 'Enviar respostas' }))
    expect(await screen.findByText('Você já respondeu este formulário.')).toBeInTheDocument()
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/eu/formularios'))
  })

  it('400 com errors por pergunta volta para a pergunta e mostra a mensagem junto dela', async () => {
    vi.mocked(responderFormulario).mockRejectedValue(
      new ErroDeApi(400, 'Dados inválidos.', {
        errors: { 'respostas[1].texto': ['O texto passa de 4000 caracteres.'] },
      }),
    )
    const { user } = await abrirResponder(PESQUISA_ANONIMA)
    await user.click(screen.getByRole('radio', { name: '2' }))
    await user.click(screen.getByRole('button', { name: 'Próxima' }))
    await user.type(screen.getByRole('textbox'), 'texto')
    await user.click(screen.getByRole('button', { name: 'Enviar respostas' }))
    expect(await screen.findByText('O texto passa de 4000 caracteres.')).toBeInTheDocument()
    expect(screen.getByText('Pergunta 2 de 2')).toBeInTheDocument()
  })

  it('422 (formulario encerrado) mostra o detail ao abrir', async () => {
    capturar(
      'obterFormularioParaResponder',
      problemaDoContrato('obterFormularioParaResponder', 422, 'encerrado'),
    )
    renderizarTela('/eu/formularios/8/responder', { perfil: 'Funcionario' })
    const detail = ex<{ detail: string }>('obterFormularioParaResponder', 422, 'encerrado').detail
    expect(await screen.findByText(detail)).toBeInTheDocument()
  })
})

describe('Formularios do setor (tela 57)', { timeout: 20000 }, () => {
  it('lista sem a avaliacao do gestor; anonimo aberto sem numero nem lista; o lembrete chama lembrarQuemFaltaNaEquipe', async () => {
    const lista = capturar('listarFormulariosDaEquipe', {
      corpo: {
        itens: [
          ...ex<Esquema<'FormularioEquipeLista'>>('listarFormulariosDaEquipe').itens,
          {
            id: 10,
            titulo: 'Avaliação do gestor, ciclo 2026.2',
            tipo: 'AVALIACAO',
            sentidoAvaliacao: 'EQUIPE_AVALIA_GESTOR',
            anonimo: true,
            prazo: '2026-10-16',
            respondidos: null,
            esperados: 8,
            taxaDeRespostaPercentual: null,
          },
        ],
      } satisfies Esquema<'FormularioEquipeLista'>,
    })
    const respondentes = capturar('listarRespondentesDaEquipe', {
      corpo: ex('listarRespondentesDaEquipe', 200, 'anonimoAberto'),
    })
    const lembrar = capturar('lembrarQuemFaltaNaEquipe', {
      corpo: ex('lembrarQuemFaltaNaEquipe', 200, 'anonimo'),
    })
    const { user } = renderizarTela('/gestor/formularios', { perfil: 'Gestor' })
    await screen.findByRole('heading', { level: 1, name: 'Formulários do setor' })
    expect(
      await screen.findByRole('button', { name: /Pesquisa de clima 2026/ }),
    ).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Avaliação do gestor/ })).toBeNull()
    const clima = screen.getByRole('button', { name: /Pesquisa de clima 2026/ })
    expect(within(clima).getByText('8 esperados, resultado ao encerrar')).toBeInTheDocument()
    expect(within(clima).queryByRole('progressbar')).toBeNull()
    lista.conforme()
    const painel = await screen.findByRole('region', { name: 'Quem respondeu' })
    expect(await within(painel).findByText('Esperados')).toBeInTheDocument()
    expect(within(painel).getAllByText('disponível ao encerrar')).toHaveLength(2)
    expect(within(painel).queryByRole('list', { name: 'Pessoas da equipe' })).toBeNull()
    respondentes.conforme()
    await user.click(within(painel).getByRole('button', { name: 'Lembrar quem falta por e-mail' }))
    await waitFor(() => expect(lembrar.quantas()).toBe(1))
    lembrar.conforme()
    expect(await screen.findByText('Lembrete enviado.')).toBeInTheDocument()
  })

  it('com nome: cartoes e a lista de pessoas com a observacao; faixa "Sua parte" com "3 de 8"', async () => {
    capturar('listarRespondentesDaEquipe', {
      corpo: ex('listarRespondentesDaEquipe', 200, 'identificado'),
    })
    capturar('listarMeusFormularios', {
      corpo: {
        ...ABERTOS,
        itens: [
          {
            ...ABERTOS.itens[0]!,
            id: 9,
            titulo: 'Avaliação da equipe, ciclo 2026.2',
            sentidoAvaliacao: 'GESTOR_AVALIA_EQUIPE',
            anonimo: false,
            respondidos: 3,
            total: 8,
            prazo: '2026-10-23',
          },
        ],
      } satisfies Meus,
    })
    const { user } = renderizarTela('/gestor/formularios', { perfil: 'Gestor' })
    const faixa = await screen.findByText('Você avaliou 3 de 8 pessoas. O prazo é 23/10.')
    expect(faixa).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Continuar avaliação' })).toHaveAttribute(
      'href',
      '/eu/formularios/9/responder',
    )
    await user.click(
      await screen.findByRole('button', { name: /Pesquisa de segurança no trabalho/ }),
    )
    const pessoas = await screen.findByRole('list', { name: 'Pessoas da equipe' })
    expect(within(pessoas).getByText('Vendedora, de férias até 17/10')).toBeInTheDocument()
  })
})

describe('Pagina publica do candidato', { timeout: 20000 }, () => {
  it('nenhuma chamada leva Authorization; "Comecar" so liga com o aceite; envia nome, e-mail e respostas', async () => {
    const pagina = capturar('obterFormularioPublico')
    const { user } = renderizarTela('/formulario/token-de-teste', { perfil: 'RH' })
    const publico = ex<Esquema<'FormularioPaginaPublica'>>('obterFormularioPublico')
    expect(
      await screen.findByRole('heading', { level: 1, name: publico.titulo }),
    ).toBeInTheDocument()
    expect(pagina.ultima().cabecalhos.get('authorization')).toBeNull()
    expect(pagina.ultima().parametrosDoCaminho).toEqual({ token: 'token-de-teste' })
    pagina.conforme()
    expect(screen.queryByRole('navigation', { name: 'Menu principal' })).toBeNull()
    expect(
      screen.getByText(`Os seus dados ficam guardados por ${publico.retencaoDias} dias.`),
    ).toBeInTheDocument()
    const comecar = screen.getByRole('button', { name: 'Começar' })
    expect(comecar).toBeDisabled()
    await user.type(screen.getByRole('textbox', { name: 'Nome' }), 'Mariana Teixeira')
    await user.type(screen.getByRole('textbox', { name: 'E-mail' }), 'mariana@email.com.br')
    await user.click(screen.getByRole('checkbox', { name: /Li e concordo/ }))
    expect(comecar).toBeEnabled()
    await user.click(comecar)
    expect(await screen.findByText(`Pergunta 1 de ${publico.perguntas.length}`)).toBeInTheDocument()
  })

  it.each([
    [404, undefined, 'Este link não existe ou não está mais disponível.'],
    [
      422,
      'aindaNaoAberto',
      'Este formulário ainda não está aberto. Tente de novo a partir da data de publicação.',
    ],
    [422, 'encerrado', 'Este formulário não está mais aceitando respostas.'],
    [422, 'empresaNaoConfigurada', 'Este formulário ainda não está disponível. Fale com o RH.'],
    [429, undefined, 'Muitas tentativas. Tente de novo em alguns minutos.'],
  ] as const)('%s %s mostra o texto da nota', async (status, nome, texto) => {
    capturar('obterFormularioPublico', problemaDoContrato('obterFormularioPublico', status, nome))
    renderizarTela('/formulario/abc')
    expect(await screen.findByText(texto)).toBeInTheDocument()
  })

  it('envio: 409 JA_RESPONDIDO mostra o detail; sucesso mostra a mensagem do recibo', async () => {
    const publico = ex<Esquema<'FormularioPaginaPublica'>>('obterFormularioPublico')
    servidor.use(
      http.get('*/api/publico/formularios/:token', () =>
        HttpResponse.json({
          ...publico,
          perguntas: [
            {
              id: 1,
              ordem: 1,
              texto: 'Disponível aos sábados?',
              tipo: 'RESPOSTA_CURTA',
              obrigatoria: true,
              alternativas: [],
            },
          ],
        } satisfies Esquema<'FormularioPaginaPublica'>),
      ),
    )
    const e = ex<{ title: string; detail: string; codigo: string }>(
      'responderFormularioPublico',
      409,
      'candidato',
    )
    vi.mocked(responderFormularioPublico).mockRejectedValueOnce(
      new ErroDeApi(409, e.title, { detail: e.detail, codigo: e.codigo }),
    )
    const { user } = renderizarTela('/formulario/abc')
    await user.type(await screen.findByRole('textbox', { name: 'Nome' }), 'Mariana')
    await user.type(screen.getByRole('textbox', { name: 'E-mail' }), 'm@email.com.br')
    fireEvent.change(screen.getByRole('textbox', { name: 'Telefone (opcional)' }), {
      target: { value: '(11) 98877-1204' },
    })
    await user.click(screen.getByRole('checkbox', { name: /Li e concordo/ }))
    await user.click(screen.getByRole('button', { name: 'Começar' }))
    await user.type(screen.getByRole('textbox'), 'Sim')
    await user.click(screen.getByRole('button', { name: 'Enviar candidatura' }))
    expect(await screen.findByText(e.detail)).toBeInTheDocument()
    expect(responderFormularioPublico).toHaveBeenCalledWith(
      'abc',
      {
        nome: 'Mariana',
        email: 'm@email.com.br',
        telefone: '(11) 98877-1204',
        curriculo: null,
        aceitouAvisoDePrivacidade: true,
      },
      { respostas: [{ perguntaId: 1, texto: 'Sim' }], arquivos: [] },
    )
    await user.click(screen.getByRole('button', { name: 'Enviar candidatura' }))
    const recibo = ex<{ mensagem: string }>('responderFormularioPublico', 201)
    expect(await screen.findByText(recibo.mensagem)).toBeInTheDocument()
  })
})

describe(
  'Ligacoes: "Enviar o formulario de admissao" (ficha 5, bloco 9)',
  { timeout: 20000 },
  () => {
    it('cadastro: a caixa liga, lista o formulario 11 (tipo=ADMISSAO, situacao=PUBLICADO) e manda formularioAdmissaoId', async () => {
      const formularios = capturar('listarFormulariosRh')
      const criar = capturar(
        'criarFuncionario',
        problemaDoContrato('criarFuncionario', 422, 'formularioEncerrado'),
      )
      const { user } = renderizarTela('/rh/colaboradores/novo')
      const caixa = await screen.findByRole('checkbox', { name: /Enviar o formulário de admissão/ })
      await waitFor(() => expect(caixa).not.toHaveAttribute('aria-disabled', 'true'))
      expect(formularios.ultima().consulta.get('tipo')).toBe('ADMISSAO')
      expect(formularios.ultima().consulta.get('situacao')).toBe('PUBLICADO')
      formularios.conforme()
      await user.click(caixa)
      expect(screen.getByText('Admissão: outubro de 2026')).toBeInTheDocument()
      expect(criar.quantas()).toBe(0)
    })

    it('sem formulario de admissao publicado, a caixa fica desligada com a dica', async () => {
      capturar('listarFormulariosRh', {
        corpo: { ...ex<Esquema<'FormularioRhLista'>>('listarFormulariosRh'), total: 0, itens: [] },
      })
      renderizarTela('/rh/colaboradores/novo')
      const caixa = await screen.findByRole('checkbox', { name: /Enviar o formulário de admissão/ })
      expect(
        await screen.findByText('Crie e publique um formulário de admissão em Formulários'),
      ).toBeInTheDocument()
      expect(caixa).toHaveAttribute('aria-disabled', 'true')
    })
  },
)
