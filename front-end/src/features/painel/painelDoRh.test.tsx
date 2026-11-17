import { screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Esquema } from '../../shared/api/tipos'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { escolherOpcao } from '../../test/formulario'
import { renderizarTela } from '../../test/telas'

type Painel = Esquema<'PainelRh'>
type Pendencias = Esquema<'PainelPendenciaLista'>
const ex = <T,>(op: string, status?: number, nome?: string) =>
  exemploDoContrato<T>(op, status, nome)
const PAINEL = ex<Painel>('obterPainelDoRh')
const PENDENCIAS = ex<Pendencias>('listarPendenciasDoRh')

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
})
afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
})

async function abrir() {
  const tela = renderizarTela('/rh/painel')
  await screen.findByRole('heading', { level: 1, name: 'Bom dia, Ana' })
  await screen.findByRole('region', { name: 'Precisa da sua atenção' })
  return tela
}
const comPainel = (parcial: Partial<Painel>) =>
  capturar('obterPainelDoRh', { corpo: { ...PAINEL, ...parcial } })

describe('Painel do RH (tela 00)', { timeout: 20000 }, () => {
  it('saudacao, data, o numero de pedidos esperando e as tres acoes do topo; chama so o painel (sem parametros), conforme', async () => {
    const painel = capturar('obterPainelDoRh')
    await abrir()
    expect(
      await screen.findByText('Quarta-feira, 7 de outubro. Há 10 pedidos esperando revisão.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Publicar aviso' })).toHaveAttribute(
      'href',
      '/rh/avisos/novo',
    )
    expect(screen.getByRole('link', { name: 'Adicionar colaborador' })).toHaveAttribute(
      'href',
      '/rh/colaboradores/novo',
    )
    expect(screen.getByRole('searchbox', { name: 'Buscar colaborador' })).toBeInTheDocument()
    expect(painel.ultima().consulta.size).toBe(0)
    painel.conforme()
  })

  it.each([
    [1, 'Há 1 pedido esperando revisão.'],
    [0, 'Nenhum pedido esperando revisão.'],
  ] as const)('com totalPendencias %i a frase e "%s"', async (total, frase) => {
    comPainel({ totalPendencias: total, pendencias: total ? PAINEL.pendencias : [] })
    renderizarTela('/rh/painel')
    expect(await screen.findByText(new RegExp(frase.replace('.', '\\.')))).toBeInTheDocument()
  })

  it('a busca do topo leva a lista de colaboradores com o texto (Enter)', async () => {
    const { user, roteador } = await abrir()
    await user.type(screen.getByRole('searchbox', { name: 'Buscar colaborador' }), 'Bruno{Enter}')
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/rh/colaboradores'))
    expect(roteador.state.location.search).toBe('?busca=Bruno')
  })

  describe('"Precisa da sua atenção"', () => {
    it('um cartao por tipo com quantidade maior que zero, na ordem da API, com numero, titulo, resumo e "Revisar" para o destino certo', async () => {
      await abrir()
      const bloco = screen.getByRole('region', { name: 'Precisa da sua atenção' })
      const cartoes = await within(bloco).findAllByRole('listitem')
      expect(cartoes.map((c) => c.getAttribute('aria-label'))).toEqual(
        PAINEL.pendencias
          .filter((p) => p.quantidade > 0)
          .map(
            (p) =>
              ({
                AJUSTE_DE_PONTO: 'Pedidos de ajuste de ponto',
                FERIAS: 'Pedidos de férias',
                ATESTADO: 'Atestados para validar',
                ADMISSAO_COM_DOCUMENTOS_PENDENTES: 'Admissão com documentos pendentes',
                BENEFICIOS: 'Pedidos de benefícios',
              })[p.tipo],
          ),
      )
      const ajuste = within(bloco).getByRole('listitem', { name: 'Pedidos de ajuste de ponto' })
      expect(within(ajuste).getByText('4')).toBeInTheDocument()
      expect(
        within(ajuste).getByText('O mais antigo é de Bruno Lima: saída de 02/10 sem registro'),
      ).toBeInTheDocument()
      const destinos = within(bloco)
        .getAllByRole('link', { name: /^Revisar/ })
        .map((l) => l.getAttribute('href'))
      const esperado = PAINEL.pendencias
        .filter((p) => p.quantidade > 0)
        .map(
          (p) =>
            ({
              AJUSTE_DE_PONTO: `/rh/ponto-e-feriados?pedido=${p.referenciaId}`,
              FERIAS: `/rh/pedidos-de-ferias?pedido=${p.referenciaId}`,
              ATESTADO: '/rh/ponto-e-feriados/atestados',
              ADMISSAO_COM_DOCUMENTOS_PENDENTES: `/rh/colaboradores/${p.funcionarioId}?aba=documentos`,
              BENEFICIOS: '/rh/pedidos-de-beneficios',
            })[p.tipo],
        )
      expect(destinos).toEqual(esperado)
    })

    it('tipo com quantidade zero some e resumo nulo esconde a linha; sem nada diz "Nada esperando revisão."', async () => {
      comPainel({
        pendencias: [
          { ...PAINEL.pendencias[0]!, quantidade: 0 },
          { ...PAINEL.pendencias[1]!, resumo: null },
        ],
      })
      renderizarTela('/rh/painel')
      const bloco = await screen.findByRole('region', { name: 'Precisa da sua atenção' })
      const cartoes = await within(bloco).findAllByRole('listitem')
      expect(cartoes).toHaveLength(1)
      expect(within(cartoes[0]!).queryByText(/Um deles/)).toBeNull()
    })

    it('"Ver todas as pendências" leva a /rh/painel/pendencias', async () => {
      await abrir()
      expect(screen.getByRole('link', { name: 'Ver todas as pendências' })).toHaveAttribute(
        'href',
        '/rh/painel/pendencias',
      )
    })

    it('sem pendencia nenhuma', async () => {
      comPainel({ totalPendencias: 0, pendencias: [] })
      renderizarTela('/rh/painel')
      expect(await screen.findByText('Nada esperando revisão.')).toBeInTheDocument()
    })
  })

  describe('"Próximos dias" e "Treinamentos em andamento"', () => {
    it('o dia grande, o mes abreviado, o rotulo e o titulo de cada linha, na ordem da API', async () => {
      await abrir()
      const bloco = screen.getByRole('region', { name: 'Próximos dias' })
      const linhas = await within(bloco).findAllByRole('listitem')
      expect(linhas).toHaveLength(PAINEL.proximosDias.length)
      expect(within(linhas[0]!).getByText('12')).toBeInTheDocument()
      expect(within(linhas[0]!).getByText('out')).toBeInTheDocument()
      expect(within(linhas[0]!).getByText('Feriado nacional')).toBeInTheDocument()
      expect(within(linhas[0]!).getByText('Nossa Senhora Aparecida')).toBeInTheDocument()
      expect(within(bloco).getByText('Marina Costa, Financeiro')).toBeInTheDocument()
    })

    it('treinamento mostra "N de M concluíram" e a barra; o link leva a /rh/treinamentos', async () => {
      await abrir()
      const bloco = screen.getByRole('region', { name: 'Treinamentos em andamento' })
      expect(await within(bloco).findByText('18 de 24 concluíram')).toBeInTheDocument()
      expect(within(bloco).getAllByRole('progressbar')).toHaveLength(
        PAINEL.treinamentosEmAndamento.length,
      )
      expect(within(bloco).getByRole('link', { name: 'Gerenciar treinamentos' })).toHaveAttribute(
        'href',
        '/rh/treinamentos',
      )
    })

    it('total zero nao quebra a barra', async () => {
      comPainel({
        treinamentosEmAndamento: [{ treinamentoId: 9, titulo: 'Novo', concluiram: 0, total: 0 }],
      })
      renderizarTela('/rh/painel')
      expect(await screen.findByText('0 de 0 concluíram')).toBeInTheDocument()
    })

    it('listas vazias dizem uma frase curta em cada bloco', async () => {
      comPainel({ proximosDias: [], treinamentosEmAndamento: [], ultimasAlteracoes: [] })
      renderizarTela('/rh/painel')
      expect(await screen.findByText('Nada marcado para os próximos dias.')).toBeInTheDocument()
      expect(screen.getByText('Nenhum treinamento em andamento.')).toBeInTheDocument()
      expect(screen.getByText('Nenhuma alteração por enquanto.')).toBeInTheDocument()
    })
  })

  describe('"Presença agora"', () => {
    const cartao = async () => within(await screen.findByRole('region', { name: 'Presença agora' }))

    it('numero grande = trabalhando, total da API, "Atualizado às 9:42", a legenda sem "Ainda nao comecou" e o link de quem esta sem registro', async () => {
      await abrir()
      const c = await cartao()
      expect(await c.findByText('86')).toBeInTheDocument()
      expect(c.getByText('de 101 colaboradores registraram entrada hoje')).toBeInTheDocument()
      expect(c.getByText('Atualizado às 9:42')).toBeInTheDocument()
      const legenda = c.getByRole('list', { name: 'Legenda' })
      expect(
        within(legenda)
          .getAllByRole('listitem')
          .map((i) => i.textContent),
      ).toEqual(['Trabalhando 86', 'Férias 6', 'Atestado 2', 'Sem registro 7'])
      expect(c.queryByText(/Ainda não começou/)).toBeNull()
      expect(c.getByRole('link', { name: 'Ver quem está sem registro' })).toHaveAttribute(
        'href',
        '/rh/painel/sem-registro',
      )
    })

    it('a barra tem um trecho por situacao, proporcional ao total (sem style=)', async () => {
      await abrir()
      const c = await cartao()
      const barra = await c.findByRole('img', { name: 'Distribuição da presença hoje' })
      const trechos = [...barra.querySelectorAll('rect')]
      expect(trechos.map((t) => t.getAttribute('data-segmento'))).toEqual([
        'trabalhando',
        'ferias',
        'atestado',
        'semRegistro',
      ])
      expect(Number(trechos[0]!.getAttribute('width'))).toBeCloseTo((86 / 101) * 100, 5)
      expect(Number(trechos[1]!.getAttribute('x'))).toBeCloseTo((86 / 101) * 100, 5)
      expect(barra.querySelector('[style]')).toBeNull()
    })

    it('"Ainda nao comecou" aparece na legenda so quando e maior que zero', async () => {
      const p = PAINEL.presencaAgora
      comPainel({
        presencaAgora: {
          ...p,
          contagensHoje: {
            ...p.contagensHoje,
            aindaNaoComecou: 3,
            trabalhando: p.contagensHoje.trabalhando - 3,
          },
        },
      })
      renderizarTela('/rh/painel')
      const c = await cartao()
      expect(await c.findByText('Ainda não começou 3')).toBeInTheDocument()
    })

    it('sem ninguem sem registro o link some', async () => {
      const p = PAINEL.presencaAgora
      comPainel({
        presencaAgora: {
          ...p,
          contagensHoje: {
            ...p.contagensHoje,
            semRegistro: 0,
            trabalhando: p.contagensHoje.trabalhando + 7,
          },
        },
      })
      renderizarTela('/rh/painel')
      const c = await cartao()
      await c.findByText('93')
      expect(c.queryByRole('link', { name: 'Ver quem está sem registro' })).toBeNull()
    })

    it('dia sem expediente: so "Hoje não há expediente" e, se houver, quantos registraram entrada', async () => {
      const p = PAINEL.presencaAgora
      comPainel({
        presencaAgora: {
          ...p,
          diaUtil: false,
          contagensHoje: { ...p.contagensHoje, trabalhando: 2, semRegistro: 0 },
        },
      })
      renderizarTela('/rh/painel')
      const c = await cartao()
      expect(await c.findByText('Hoje não há expediente')).toBeInTheDocument()
      expect(c.getByText('2 colaboradores registraram entrada hoje')).toBeInTheDocument()
      expect(c.queryByRole('img')).toBeNull()
    })

    it('total zero nao desenha a barra', async () => {
      const p = PAINEL.presencaAgora
      comPainel({
        presencaAgora: {
          ...p,
          total: 0,
          contagensHoje: {
            trabalhando: 0,
            ferias: 0,
            atestado: 0,
            semRegistro: 0,
            aindaNaoComecou: 0,
            semExpediente: 0,
          },
        },
      })
      renderizarTela('/rh/painel')
      const c = await cartao()
      await c.findByText('de 0 colaboradores registraram entrada hoje')
      expect(c.queryByRole('img')).toBeNull()
    })
  })

  it('"Últimas alterações": quando, quem e o que aconteceu como texto; o link abre o log de auditoria', async () => {
    await abrir()
    const tabela = await screen.findByRole('table', { name: 'Últimas alterações' })
    expect(
      within(tabela)
        .getAllByRole('columnheader')
        .map((c) => c.textContent),
    ).toEqual(['Quando', 'Quem', 'O que aconteceu'])
    expect(within(tabela).getAllByRole('row')).toHaveLength(PAINEL.ultimasAlteracoes.length + 1)
    expect(within(tabela).getByText('07/10/2026 09:41')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Abrir log de auditoria' })).toHaveAttribute(
      'href',
      '/rh/auditoria',
    )
  })

  it('o texto vindo da API nunca vira HTML', async () => {
    comPainel({
      ultimasAlteracoes: [
        {
          quandoUtc: '2026-10-07T12:00:00Z',
          autor: 'Ana',
          descricao: '<img src=x onerror=alert(1)> alterou',
        },
      ],
    })
    renderizarTela('/rh/painel')
    expect(await screen.findByText(/<img src=x onerror=alert\(1\)> alterou/)).toBeInTheDocument()
    expect(document.querySelector('img[src="x"]')).toBeNull()
  })

  it('os textos cortados do Figma nao existem na tela', async () => {
    await abrir()
    const rotulos = within(screen.getByRole('main'))
    for (const texto of [
      /Folga ou compensação/,
      /Fim do período de experiência/,
      /Treinamento presencial/,
    ])
      expect(rotulos.queryByText(texto)).toBeNull()
  })

  it('erro no painel mostra o aviso com "Tentar de novo" em cada bloco', async () => {
    capturar('obterPainelDoRh', problema(500, { title: 'Erro interno.', status: 500 }))
    renderizarTela('/rh/painel')
    const avisos = await screen.findAllByText('Erro no servidor. Tente novamente em instantes.')
    expect(avisos.length).toBeGreaterThanOrEqual(2)
    expect(screen.getAllByRole('button', { name: 'Tentar de novo' }).length).toBeGreaterThanOrEqual(
      2,
    )
  })
})

describe('Pendências do RH (tela 7.5)', { timeout: 20000 }, () => {
  async function abrirLista(rota = '/rh/painel/pendencias') {
    const tela = renderizarTela(rota)
    await screen.findByRole('heading', { level: 1, name: 'Pendências', hidden: true })
    const lista = await screen.findByRole('list', { name: 'Pendências' })
    return { ...tela, lista }
  }

  it('uma linha por pendencia (tipo, pessoa, frase, "Desde ...") e "Revisar" com o destino do tipo; chama com pagina, conforme', async () => {
    const lista = capturar('listarPendenciasDoRh')
    const { lista: itens } = await abrirLista()
    expect(lista.ultima().consulta.get('pagina')).toBe('1')
    expect(lista.ultima().consulta.has('tipo')).toBe(false)
    lista.conforme()
    expect(within(itens).getAllByRole('listitem')).toHaveLength(PENDENCIAS.itens.length)
    const primeira = PENDENCIAS.itens[0]!
    const linha = within(itens).getAllByRole('listitem')[0]!
    expect(within(linha).getByText(primeira.funcionario.nome)).toBeInTheDocument()
    expect(within(linha).getByText(primeira.descricao)).toBeInTheDocument()
    expect(within(linha).getByText(/^Desde \d{2}\/\d{2}\/\d{4}/)).toBeInTheDocument()
    expect(within(linha).getByRole('link', { name: /^Revisar/ })).toBeInTheDocument()
  })

  it('na admissao com documentos pendentes a data e "Começa em dd/mm" (nao "Desde")', async () => {
    capturar('listarPendenciasDoRh', {
      corpo: {
        ...PENDENCIAS,
        itens: [
          {
            tipo: 'ADMISSAO_COM_DOCUMENTOS_PENDENTES',
            referenciaId: 30,
            funcionario: { id: 30, nome: 'Rafael Torres', ativo: true },
            descricao: 'Ainda não enviou o formulário de admissão',
            desdeEmUtc: '2026-10-13T03:00:00Z',
          },
        ],
      },
    })
    const { lista } = await abrirLista()
    expect(within(lista).getByText('Começa em 13/10')).toBeInTheDocument()
    expect(within(lista).queryByText(/^Desde/)).toBeNull()
    expect(within(lista).getByRole('link', { name: /^Revisar/ })).toHaveAttribute(
      'href',
      '/rh/colaboradores/30?aba=documentos',
    )
  })

  it('o filtro Tipo vira parametro da chamada e fica na URL', async () => {
    const lista = capturar('listarPendenciasDoRh')
    const { user, roteador } = await abrirLista()
    await escolherOpcao(user, 'Tipo', 'Pedidos de férias')
    await waitFor(() => expect(lista.ultima().consulta.get('tipo')).toBe('FERIAS'))
    expect(roteador.state.location.search).toContain('tipo=FERIAS')
  })

  it('vazio e erro', async () => {
    capturar('listarPendenciasDoRh', {
      corpo: { ...PENDENCIAS, itens: [], total: 0, totalPaginas: 0 },
    })
    const { unmount } = renderizarTela('/rh/painel/pendencias')
    expect(await screen.findByText('Nada esperando o RH.')).toBeInTheDocument()
    unmount()
    capturar('listarPendenciasDoRh', problema(500, { title: 'Erro interno.', status: 500 }))
    renderizarTela('/rh/painel/pendencias')
    expect(
      await screen.findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
  })
})

describe('Pessoas sem registro hoje (tela 7.6)', { timeout: 20000 }, () => {
  it('lista em ordem da API com iniciais, nome, cargo e setor, sem botao de lembrete; chama com pagina, conforme', async () => {
    const lista = capturar('listarPessoasSemRegistroHoje')
    renderizarTela('/rh/painel/sem-registro')
    await screen.findByRole('heading', {
      level: 1,
      name: 'Sem registro de ponto hoje',
      hidden: true,
    })
    const itens = await screen.findByRole('list', { name: 'Pessoas sem registro' })
    expect(lista.ultima().consulta.get('pagina')).toBe('1')
    lista.conforme()
    expect(within(itens).getAllByRole('listitem').length).toBeGreaterThan(0)
    expect(screen.getByText('Quarta-feira, 7 de outubro')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /lembrar|lembrete/i })).toBeNull()
  })

  it('vazio: "Ninguém sem registro agora." e, em dia sem expediente, "Hoje não há expediente."', async () => {
    capturar('listarPessoasSemRegistroHoje', {
      corpo: { pagina: 1, tamanhoPagina: 10, total: 0, totalPaginas: 0, itens: [] },
    })
    const { unmount } = renderizarTela('/rh/painel/sem-registro')
    expect(await screen.findByText('Ninguém sem registro agora.')).toBeInTheDocument()
    unmount()
    capturar('listarPessoasSemRegistroHoje', {
      corpo: { pagina: 1, tamanhoPagina: 10, total: 0, totalPaginas: 0, itens: [] },
    })
    const p = PAINEL.presencaAgora
    comPainel({ presencaAgora: { ...p, diaUtil: false } })
    renderizarTela('/rh/painel/sem-registro')
    expect(await screen.findByText('Hoje não há expediente.')).toBeInTheDocument()
  })

  it('erro mostra o aviso', async () => {
    capturar('listarPessoasSemRegistroHoje', problema(500, { title: 'Erro interno.', status: 500 }))
    renderizarTela('/rh/painel/sem-registro')
    expect(
      await screen.findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
  })
})
