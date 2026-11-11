import { screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Esquema } from '../../shared/api/tipos'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { renderizarTela } from '../../test/telas'

type Lista = Esquema<'TreinamentoMeuLista'>
type Detalhe = Esquema<'TreinamentoMeuDetalhe'>
const ex = <T,>(op: string, status?: number, nome?: string) =>
  exemploDoContrato<T>(op, status, nome)
const MEUS = ex<Lista>('listarMeusTreinamentos')
const DETALHE = ex<Detalhe>('obterMeuTreinamento')
const problemaDoContrato = (op: string, status: number, nome?: string) =>
  problema(status, ex<Record<string, unknown>>(op, status, nome))

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
})
afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
})

async function abrirMeus(
  perfil: 'Funcionario' | 'Gestor' | 'RH' = 'Funcionario',
  rota = '/eu/treinamentos',
) {
  const tela = renderizarTela(rota, { perfil })
  await screen.findByRole('heading', { level: 1, name: 'Meus treinamentos' })
  return tela
}

describe('Meus treinamentos (telas 41 e 42)', { timeout: 20000 }, () => {
  it.each(['Funcionario', 'Gestor', 'RH'] as const)(
    '%s: sem "Em breve" nem prazo; botao por situacao; aba=TODOS, conforme',
    async (perfil) => {
      const lista = capturar('listarMeusTreinamentos')
      await abrirMeus(perfil)
      const seguranca = await screen.findByRole('article', { name: 'Segurança da informação' })
      expect(within(seguranca).getByRole('button', { name: 'Continuar →' })).toBeInTheDocument()
      expect(within(seguranca).getByText('1h40')).toBeInTheDocument()
      const excel = screen.getByRole('article', { name: 'Excel para relatórios financeiros' })
      expect(
        within(excel).getByRole('button', { name: 'Quero me inscrever →' }),
      ).toBeInTheDocument()
      expect(within(excel).getByText('8 módulos')).toBeInTheDocument()
      expect(screen.queryByText(/Em breve|Até \d|De \d/)).toBeNull()
      expect(lista.ultima().consulta.get('aba')).toBe('TODOS')
      lista.conforme()
    },
  )

  it('"Quero me inscrever" chama inscreverMeEmTreinamento; "Comecar" chama iniciarMeuTreinamento e abre a tela de fazer', async () => {
    const inscrever = capturar('inscreverMeEmTreinamento')
    const iniciar = capturar('iniciarMeuTreinamento')
    capturar('listarMeusTreinamentos', {
      corpo: {
        ...MEUS,
        itens: [
          MEUS.itens[1]!,
          {
            ...MEUS.itens[0]!,
            id: 4,
            titulo: 'Novo obrigatório',
            situacao: 'NAO_INICIADO',
            modulosConcluidos: 0,
            progressoPercentual: 0,
          },
        ],
      } satisfies Lista,
    })
    const { user, roteador } = await abrirMeus()
    await user.click(await screen.findByRole('button', { name: 'Quero me inscrever →' }))
    await waitFor(() => expect(inscrever.quantas()).toBe(1))
    expect(inscrever.ultima().parametrosDoCaminho).toEqual({ treinamentoId: '7' })
    inscrever.conforme()
    await user.click(screen.getByRole('button', { name: 'Começar →' }))
    await waitFor(() => expect(iniciar.quantas()).toBe(1))
    iniciar.conforme()
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/eu/treinamentos/4'))
  })

  it('obrigatorio e busca vao para listarMeusTreinamentos; Concluidos manda aba=CONCLUIDOS', async () => {
    const lista = capturar('listarMeusTreinamentos')
    const { user } = await abrirMeus()
    await screen.findByRole('article', { name: 'Segurança da informação' })
    await user.click(screen.getByRole('combobox', { name: 'Obrigatoriedade' }))
    await user.click(await screen.findByRole('option', { name: 'Só obrigatórios' }))
    await waitFor(() => expect(lista.ultima().consulta.get('obrigatorio')).toBe('true'))
    await user.type(screen.getByRole('searchbox', { name: 'Buscar treinamento' }), 'LGPD')
    await waitFor(() => expect(lista.ultima().consulta.get('busca')).toBe('LGPD'))
    await user.click(screen.getByRole('tab', { name: /^Concluídos/ }))
    await waitFor(() => expect(lista.ultima().consulta.get('aba')).toBe('CONCLUIDOS'))
    lista.conforme()
  })

  it('Concluidos (42): o numero do certificado como a API devolve, "Ver certificado"; sem certificado, sem codigo', async () => {
    const certificado = capturar('baixarMeuCertificado')
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    const concluido = {
      ...DETALHE,
      certificado: { ...DETALHE.certificado!, numero: 'CERT-2030-00001' },
    }
    const { modulos: _modulos, ...cartao } = concluido
    void _modulos
    capturar('listarMeusTreinamentos', {
      corpo: {
        ...MEUS,
        itens: [
          cartao,
          { ...cartao, id: 9, titulo: 'Onboarding 2022', onboarding: true, certificado: null },
        ],
      } satisfies Lista,
    })
    const { user } = await abrirMeus('Funcionario', '/eu/treinamentos?aba=concluidos')
    const lgpd = await screen.findByRole('article', { name: 'LGPD na prática' })
    expect(within(lgpd).getByText('Código do certificado: CERT-2030-00001.')).toBeInTheDocument()
    expect(within(lgpd).getByText('Concluído em 15/09/2026')).toBeInTheDocument()
    expect(screen.queryByText(/LGPD-|nota/)).toBeNull()
    const onboarding = screen.getByRole('article', { name: 'Onboarding 2022' })
    expect(within(onboarding).getByText('Onboarding')).toBeInTheDocument()
    expect(within(onboarding).queryByText(/Código do certificado/)).toBeNull()
    expect(within(onboarding).queryByRole('button', { name: 'Ver certificado' })).toBeNull()
    expect(within(onboarding).getByRole('link', { name: 'Rever conteúdo' })).toBeInTheDocument()
    await user.click(within(lgpd).getByRole('button', { name: 'Ver certificado' }))
    await waitFor(() => expect(certificado.quantas()).toBe(1))
    certificado.conforme()
  })

  it('encerrado pelo RH mostra o aviso no cartao', async () => {
    capturar('listarMeusTreinamentos', {
      corpo: { ...MEUS, itens: [{ ...MEUS.itens[0]!, encerrado: true }] } satisfies Lista,
    })
    await abrirMeus()
    expect(
      await screen.findByText('Encerrado pelo RH; você ainda pode terminar'),
    ).toBeInTheDocument()
  })
})

const EM_ANDAMENTO: Detalhe = {
  ...DETALHE,
  situacao: 'EM_ANDAMENTO',
  concluidoEmUtc: null,
  certificado: null,
  modulosConcluidos: 3,
  progressoPercentual: 75,
  moduloAtual: 4,
  modulos: DETALHE.modulos.map((m) =>
    m.ordem === 4 ? { ...m, concluido: false, concluidoEmUtc: null } : m,
  ),
}

describe('Fazer o treinamento (sem print)', { timeout: 20000 }, () => {
  it('video e link abrem em nova aba com rel="noopener noreferrer" e nunca ha iframe; PDF pelo link temporario', async () => {
    const conteudo = capturar('obterConteudoDeModulo', {
      corpo: {
        id: 11,
        ordem: 1,
        titulo: 'O que é a LGPD',
        tipo: 'VIDEO',
        duracaoMinutos: 15,
        conteudoTexto: null,
        urlExterna: 'https://videos.exemplo.com.br/lgpd',
        temArquivo: false,
        concluido: true,
        concluidoEmUtc: '2026-08-02T13:10:00Z',
      } satisfies Esquema<'TreinamentoModuloConteudo'>,
    })
    capturar('obterMeuTreinamento', { corpo: EM_ANDAMENTO })
    const { user } = renderizarTela('/eu/treinamentos/2', { perfil: 'Funcionario' })
    await screen.findByRole('heading', { level: 1, name: 'LGPD na prática' })
    await user.click(screen.getByRole('button', { name: /1\. O que é a LGPD/ }))
    const link = await screen.findByRole('link', { name: /Abrir vídeo/ })
    expect(link).toHaveAttribute('target', '_blank')
    expect(link).toHaveAttribute('rel', 'noopener noreferrer')
    expect(link).toHaveAttribute('href', 'https://videos.exemplo.com.br/lgpd')
    expect(document.querySelector('iframe, video')).toBeNull()
    expect(conteudo.ultima().parametrosDoCaminho).toEqual({ treinamentoId: '2', moduloId: '11' })
    conteudo.conforme()

    const pdf = capturar('obterConteudoDeModulo', {
      corpo: {
        id: 14,
        ordem: 4,
        titulo: 'Canais para falar do assunto',
        tipo: 'PDF',
        duracaoMinutos: 20,
        conteudoTexto: null,
        urlExterna: null,
        temArquivo: true,
        concluido: false,
        concluidoEmUtc: null,
      } satisfies Esquema<'TreinamentoModuloConteudo'>,
    })
    const arquivo = capturar('baixarArquivoDeModulo')
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    await user.click(screen.getByRole('button', { name: /4\. Canais para falar do assunto/ }))
    await user.click(await screen.findByRole('button', { name: 'Abrir PDF' }))
    await waitFor(() => expect(arquivo.quantas()).toBe(1))
    arquivo.conforme()
    expect(pdf.quantas()).toBeGreaterThan(0)
  })

  it('concluir o ultimo modulo mostra "Voce concluiu" com o numero do certificado (como veio)', async () => {
    capturar('obterMeuTreinamento', { corpo: EM_ANDAMENTO })
    capturar('obterConteudoDeModulo', {
      corpo: {
        id: 14,
        ordem: 4,
        titulo: 'Canais para falar do assunto',
        tipo: 'TEXTO',
        duracaoMinutos: 20,
        conteudoTexto: 'Fale com o RH.',
        urlExterna: null,
        temArquivo: false,
        concluido: false,
        concluidoEmUtc: null,
      } satisfies Esquema<'TreinamentoModuloConteudo'>,
    })
    const resposta = ex<Esquema<'TreinamentoModuloConclusaoResposta'>>(
      'concluirModuloDeTreinamento',
    )
    const concluir = capturar('concluirModuloDeTreinamento', {
      corpo: {
        ...resposta,
        treinamento: {
          ...resposta.treinamento,
          certificado: { ...resposta.treinamento.certificado!, numero: 'CERT-2030-00001' },
        },
      },
    })
    const { user } = renderizarTela('/eu/treinamentos/2', { perfil: 'Funcionario' })
    await user.click(await screen.findByRole('button', { name: 'Concluir módulo' }))
    await waitFor(() => expect(concluir.quantas()).toBe(1))
    expect(concluir.ultima().parametrosDoCaminho).toEqual({ treinamentoId: '2', moduloId: '14' })
    concluir.conforme()
    const painel = await screen.findByRole('region', { name: 'Você concluiu' })
    expect(within(painel).getByText('Número do certificado: CERT-2030-00001')).toBeInTheDocument()
    expect(within(painel).getByRole('button', { name: 'Ver certificado' })).toBeInTheDocument()
  })

  it('modulos fora de ordem funcionam; 422 INSCRICAO_PENDENTE mostra "Quero me inscrever"; 422 encerrado mostra o texto', async () => {
    capturar('obterMeuTreinamento', {
      corpo: {
        ...EM_ANDAMENTO,
        modulos: EM_ANDAMENTO.modulos.map((m) => ({
          ...m,
          concluido: false,
          concluidoEmUtc: null,
        })),
      },
    })
    capturar('obterConteudoDeModulo', {
      corpo: {
        id: 13,
        ordem: 3,
        titulo: 'Quando pedir consentimento',
        tipo: 'TEXTO',
        duracaoMinutos: 15,
        conteudoTexto: 'Texto.',
        urlExterna: null,
        temArquivo: false,
        concluido: false,
        concluidoEmUtc: null,
      } satisfies Esquema<'TreinamentoModuloConteudo'>,
    })
    capturar(
      'concluirModuloDeTreinamento',
      problemaDoContrato('concluirModuloDeTreinamento', 422, 'inscricao'),
    )
    const { user } = renderizarTela('/eu/treinamentos/2', { perfil: 'Funcionario' })
    await user.click(await screen.findByRole('button', { name: /3\. Quando pedir consentimento/ }))
    await user.click(await screen.findByRole('button', { name: 'Concluir módulo' }))
    expect(await screen.findByRole('button', { name: 'Quero me inscrever' })).toBeInTheDocument()

    capturar('obterMeuTreinamento', problemaDoContrato('iniciarMeuTreinamento', 422, 'encerrado'))
    renderizarTela('/eu/treinamentos/5', { perfil: 'Funcionario' })
    expect(
      await screen.findByText('O RH encerrou este treinamento. Fale com o RH se precisar dele.'),
    ).toBeInTheDocument()
  })
})

describe('Treinamentos do setor (tela 58) e a aba da ficha da equipe', { timeout: 20000 }, () => {
  it('sem prazo nem "Lembrar quem falta"; agendado mostra "Comeca em"; a lista de pessoas e os cartoes', async () => {
    const lista = capturar('listarTreinamentosDaEquipe')
    const pessoas = capturar('listarPessoasDaEquipeNoTreinamento')
    renderizarTela('/gestor/treinamentos', { perfil: 'Gestor' })
    await screen.findByRole('heading', { level: 1, name: 'Treinamentos do setor' })
    const atendimento = await screen.findByRole('button', { name: /Atendimento ao cliente/ })
    expect(within(atendimento).getByText('Começa em 03/11')).toBeInTheDocument()
    expect(within(atendimento).getByText('0 de 8 inscritos')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /LGPD na prática/ })).toHaveTextContent(
      '6 de 8 concluíram',
    )
    lista.conforme()
    const painel = await screen.findByRole('region', { name: 'Quem fez' })
    expect(within(painel).queryByRole('button', { name: /Lembrar/ })).toBeNull()
    expect(await within(painel).findByText('Faltam concluir')).toBeInTheDocument()
    expect(screen.queryByText(/Até \d|O prazo é|faltam \d+ dias/)).toBeNull()
    pessoas.conforme()
  })

  it('faixa "Sua parte" so com obrigatorio nao encerrado e nao concluido', async () => {
    capturar('listarMeusTreinamentos', { corpo: MEUS })
    const { unmount } = renderizarTela('/gestor/treinamentos', { perfil: 'Gestor' })
    expect(await screen.findByText('Você está no módulo 4 de 5.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Continuar treinamento' })).toHaveAttribute(
      'href',
      '/eu/treinamentos/3',
    )
    unmount()
    capturar('listarMeusTreinamentos', {
      corpo: {
        ...MEUS,
        itens: [{ ...MEUS.itens[0]!, encerrado: true }, MEUS.itens[1]!],
      } satisfies Lista,
    })
    renderizarTela('/gestor/treinamentos', { perfil: 'Gestor' })
    await screen.findByRole('region', { name: 'Quem fez' })
    expect(screen.queryByText(/Sua parte/)).toBeNull()
  })

  it('aba Treinamentos da ficha da equipe: os tres numeros e a lista, sem certificado', async () => {
    const aba = capturar('listarTreinamentosDePessoaDaEquipe')
    renderizarTela('/gestor/equipe/21?aba=treinamentos', { perfil: 'Gestor' })
    const tabela = await screen.findByRole('table', { name: 'Treinamentos da pessoa' })
    expect(await within(tabela).findByText('Onboarding 2026')).toBeInTheDocument()
    expect(within(tabela).queryByRole('columnheader', { name: /Certificado/ })).toBeNull()
    expect(aba.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '21' })
    aba.conforme()
  })
})

describe('Conferir certificado (publico)', { timeout: 20000 }, () => {
  it('nenhuma chamada leva Authorization; o codigo valido mostra so o minimo', async () => {
    const validar = capturar('validarCertificado')
    renderizarTela('/certificado/a7f3c1d2-5e9b-4c68-b2d1-8f04e6a93b57', { perfil: 'RH' })
    expect(await screen.findByText('Certificado válido')).toBeInTheDocument()
    expect(screen.getByText('Bruno Lima')).toBeInTheDocument()
    expect(screen.getByText('15/09/2026')).toBeInTheDocument()
    expect(validar.ultima().cabecalhos.get('authorization')).toBeNull()
    expect(screen.queryByRole('navigation', { name: 'Menu principal' })).toBeNull()
    validar.conforme()
  })

  it('o formato do codigo e conferido antes de chamar; 404 e 429 mostram o texto', async () => {
    const validar = capturar('validarCertificado', problemaDoContrato('validarCertificado', 404))
    const { user } = renderizarTela('/certificado')
    await user.type(await screen.findByRole('textbox', { name: 'Código de validação' }), 'abc')
    await user.click(screen.getByRole('button', { name: 'Conferir' }))
    expect(
      await screen.findByText('Digite o código completo, como está no certificado.'),
    ).toBeInTheDocument()
    expect(validar.quantas()).toBe(0)
    await user.clear(screen.getByRole('textbox', { name: 'Código de validação' }))
    await user.type(
      screen.getByRole('textbox', { name: 'Código de validação' }),
      '11111111-2222-4333-8444-555555555555',
    )
    await user.click(screen.getByRole('button', { name: 'Conferir' }))
    expect(await screen.findByText('Certificado não encontrado')).toBeInTheDocument()
    expect(screen.getByText('Não encontramos um certificado com este código.')).toBeInTheDocument()

    capturar('validarCertificado', problemaDoContrato('validarCertificado', 429))
    renderizarTela('/certificado/22222222-2222-4333-8444-555555555555')
    expect(
      await screen.findByText('Muitas consultas. Tente de novo em alguns minutos.'),
    ).toBeInTheDocument()
  })
})

describe('Caixa do onboarding (ficha 6, bloco 7)', { timeout: 20000 }, () => {
  it('um item: "Incluir no treinamento {titulo}"; mais de um: plural; vazia: desligada com a dica', async () => {
    const lista = capturar('listarTreinamentosDeOnboarding')
    const { unmount } = renderizarTela('/rh/colaboradores/novo')
    expect(
      await screen.findByRole('checkbox', { name: /Incluir no treinamento Onboarding 2026/ }),
    ).toBeInTheDocument()
    lista.conforme()
    unmount()
    capturar('listarTreinamentosDeOnboarding', {
      corpo: {
        itens: [
          { id: 1, titulo: 'Onboarding 2026', comecaEm: '2026-01-01' },
          { id: 8, titulo: 'Onboarding loja', comecaEm: null },
        ],
      },
    })
    const segunda = renderizarTela('/rh/colaboradores/novo')
    expect(
      await screen.findByRole('checkbox', { name: /Incluir nos treinamentos de onboarding/ }),
    ).toBeInTheDocument()
    segunda.unmount()
    capturar('listarTreinamentosDeOnboarding', { corpo: { itens: [] } })
    renderizarTela('/rh/colaboradores/novo')
    expect(
      await screen.findByText('Nenhum treinamento de onboarding publicado'),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('checkbox', { name: /Incluir no treinamento de onboarding/ }),
    ).toHaveAttribute('aria-disabled', 'true')
  })
})
