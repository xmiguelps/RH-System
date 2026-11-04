import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Esquema } from '../../shared/api/tipos'
import { servidor } from '../../shared/mocks/servidor'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { renderizarTela } from '../../test/telas'

const problemaDoContrato = (op: string, status: number, nome?: string) =>
  problema(status, exemploDoContrato<Record<string, unknown>>(op, status, nome))

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
})

afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
})

async function abrir(rota = '/gestor/ponto-da-equipe') {
  const tela = renderizarTela(rota, { perfil: 'Gestor' })
  await screen.findByRole('heading', { level: 1, name: 'Ponto da equipe', hidden: true })
  return tela
}

describe('Ponto da equipe (tela 54)', { timeout: 15000 }, () => {
  it('subtitulo do setor, os quatro numeros e a tabela na ordem da API', async () => {
    const dia = capturar('obterPontoDaEquipe')
    await abrir()
    expect(
      await screen.findByText(
        'Setor Comercial, 8 pessoas. Os pedidos de ajuste do setor chegam primeiro para você analisar.',
      ),
    ).toBeInTheDocument()
    const numeros = await screen.findByRole('region', { name: 'Números do setor' })
    expect(await within(numeros).findByText('6')).toBeInTheDocument()
    expect(within(numeros).getByText('Diego Farias')).toBeInTheDocument()
    expect(within(numeros).getByText('Larissa Moura, até 17/10')).toBeInTheDocument()
    expect(within(numeros).getByText('2')).toHaveAttribute('data-tom', 'alerta')
    expect([...dia.ultima().consulta.keys()]).toEqual([])
    dia.conforme()
    const tabela = screen.getByRole('table', { name: 'Hoje no setor' })
    const nomes = within(tabela)
      .getAllByRole('row')
      .slice(1)
      .map((l) => within(l).getAllByRole('cell')[0]!.textContent)
    expect(nomes[0]).toMatch(/Diego Farias/)
    expect(nomes[7]).toMatch(/Pedro Santos/)
    const diego = within(tabela).getByText('Diego Farias').closest('tr')!
    expect(within(diego).getByText('Nenhum registro')).toBeInTheDocument()
    expect(within(diego).getByText('Sem registro')).toBeInTheDocument()
    expect(within(diego).getByText('−3h45')).toBeInTheDocument()
    expect(within(tabela).getAllByText('Trabalhando')).toHaveLength(6)
    expect(screen.queryByRole('button', { name: /Baixar espelho do setor/ })).toBeNull()
  })

  it('TRABALHANDO com periodoAberto false vira "Saiu"; dia passado usa situacaoDoDia e manda data', async () => {
    const exemplo = exemploDoContrato<Esquema<'PontoEquipeDia'>>('obterPontoDaEquipe')
    servidor.use(
      http.get('*/api/gestor/ponto/equipe', ({ request }) => {
        const data = new URL(request.url).searchParams.get('data')
        if (data) {
          return HttpResponse.json({
            ...exemplo,
            data,
            resumo: { ...exemplo.resumo, contagensHoje: null },
            itens: exemplo.itens.map((i) => ({
              ...i,
              situacaoHoje: null,
              situacaoDoDia: 'NORMAL',
            })),
          })
        }
        return HttpResponse.json({
          ...exemplo,
          itens: exemplo.itens.map((i, n) => (n === 1 ? { ...i, periodoAberto: false } : i)),
        })
      }),
    )
    await abrir()
    const tabela = await screen.findByRole('table', { name: 'Hoje no setor' })
    expect(await within(tabela).findByText('Saiu')).toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Hoje, 7 de outubro'), {
      target: { value: '2026-10-06' },
    })
    expect(await within(tabela).findAllByText('Normal')).toHaveLength(8)
    const numeros = screen.getByRole('region', { name: 'Números do setor' })
    expect(within(numeros).getByText('Com registro')).toBeInTheDocument()
  })

  it('os cartoes "Aguardando você" vem de status=PENDENTE, etapa=GESTOR, CRIADO_EM ASC e abrem a tela 55', async () => {
    const pedidos = capturar('listarPedidosDeAjusteDaEquipe')
    const equipe = capturar('listarEquipeDoGestor')
    const { user, roteador } = await abrir()
    const lista = await screen.findByRole('list', { name: 'Pedidos para analisar' })
    expect(within(lista).getAllByText('Aguardando você')).toHaveLength(2)
    expect(within(lista).getByText('Incluir o período 13:05 - 17:50')).toBeInTheDocument()
    expect(within(lista).getByText('Pedido em 06/10, 10:10')).toBeInTheDocument()
    const c = pedidos.ultima().consulta
    expect(c.get('status')).toBe('PENDENTE')
    expect(c.get('etapa')).toBe('GESTOR')
    expect(c.get('ordenarPor')).toBe('CRIADO_EM')
    expect(c.get('direcao')).toBe('ASC')
    pedidos.conforme()
    equipe.conforme()
    await user.click(within(lista).getByRole('link', { name: 'Pedido de Júlia Nunes' }))
    await waitFor(() =>
      expect(roteador.state.location.pathname).toBe('/gestor/ponto-da-equipe/pedidos/303'),
    )
  })

  it('RH e Funcionario veem o 403', async () => {
    for (const perfil of ['RH', 'Funcionario'] as const) {
      const t = renderizarTela('/gestor/ponto-da-equipe', { perfil })
      expect(
        await screen.findByText('Você não tem permissão para ver esta página.'),
      ).toBeInTheDocument()
      t.unmount()
    }
  })
})

describe('Pedido de ajuste do gestor (tela 55)', { timeout: 15000 }, () => {
  it('aprovar envia a observacao, avisa e volta a tela 54', async () => {
    const detalhe = capturar('obterPedidoDeAjusteDaEquipe')
    const aprovar = capturar('aprovarPedidoDeAjusteComoGestor')
    const lista = capturar('listarPedidosDeAjusteDaEquipe')
    const { user, roteador } = await abrir('/gestor/ponto-da-equipe/pedidos/303')
    const modal = await screen.findByRole('dialog', { name: 'Pedido de ajuste de ponto' })
    expect(await within(modal).findByText('Júlia Nunes')).toBeInTheDocument()
    expect(
      within(modal).getByText('Ajuste do dia 05/10, pedido em 06/10, 10:10'),
    ).toBeInTheDocument()
    expect(screen.getByText('Depois de você, o RH confirma o ajuste.')).toBeInTheDocument()
    expect(detalhe.ultima().parametrosDoCaminho).toEqual({ pedidoId: '303' })
    detalhe.conforme()
    await user.type(within(modal).getByLabelText('Observação para Júlia (opcional)'), 'Confirmo.')
    const antes = lista.quantas()
    await user.click(screen.getByRole('button', { name: 'Aprovar' }))
    expect(await screen.findByText('Pedido aprovado. Agora o RH confirma.')).toBeInTheDocument()
    expect(aprovar.ultima().corpo).toEqual({ observacao: 'Confirmo.' })
    aprovar.conforme()
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/gestor/ponto-da-equipe'))
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(antes))
  })

  it('recusar envia observacao null', async () => {
    const recusar = capturar('recusarPedidoDeAjusteComoGestor')
    const { user } = await abrir('/gestor/ponto-da-equipe/pedidos/303')
    await screen.findByText('Júlia Nunes', { selector: 'span' })
    await user.click(screen.getByRole('button', { name: 'Recusar' }))
    expect(await screen.findByText('Pedido recusado.')).toBeInTheDocument()
    expect(recusar.ultima().corpo).toEqual({ observacao: null })
    recusar.conforme()
  })

  it.each(['jaDecidido', 'etapaIncorreta'])('409 %s mostra o detail', async (nome) => {
    capturar(
      'aprovarPedidoDeAjusteComoGestor',
      problemaDoContrato('aprovarPedidoDeAjusteComoGestor', 409, nome),
    )
    const { user } = await abrir('/gestor/ponto-da-equipe/pedidos/303')
    await screen.findByText('Júlia Nunes', { selector: 'span' })
    await user.click(screen.getByRole('button', { name: 'Aprovar' }))
    const detail = exemploDoContrato<{ detail: string }>(
      'aprovarPedidoDeAjusteComoGestor',
      409,
      nome,
    ).detail
    expect(await screen.findByText(detail)).toBeInTheDocument()
  })

  it('404 (outra equipe) fecha e avisa', async () => {
    capturar(
      'obterPedidoDeAjusteDaEquipe',
      problema(404, { detail: 'O registro não foi encontrado.' }),
    )
    const { roteador } = renderizarTela('/gestor/ponto-da-equipe/pedidos/999', { perfil: 'Gestor' })
    expect(await screen.findByText(/não foi encontrado/)).toBeInTheDocument()
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/gestor/ponto-da-equipe'))
  })
})

describe('Espelho da pessoa (tela 56), aba Ponto da equipe e atestados', { timeout: 15000 }, () => {
  it('Ver espelho abre o painel com ano e mes, Baixar PDF e os atestados da pessoa', async () => {
    const espelho = capturar('obterEspelhoDePontoDaPessoaDaEquipe')
    const pdf = capturar('baixarEspelhoDePontoPdfDaEquipe')
    const atestados = capturar('listarAtestadosDaEquipe')
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    const { user, roteador } = await abrir()
    const tabela = await screen.findByRole('table', { name: 'Hoje no setor' })
    await user.click(
      await within(tabela).findByRole('link', { name: 'Ver espelho de Diego Farias' }),
    )
    await waitFor(() =>
      expect(roteador.state.location.pathname).toBe('/gestor/ponto-da-equipe/espelho/21'),
    )
    const painel = await screen.findByRole('dialog', { name: 'Espelho de ponto' })
    expect(
      await within(painel).findByText('Vendedor, jornada de 44 h semanais'),
    ).toBeInTheDocument()
    const numeros = within(painel).getByRole('region', { name: 'Números do espelho' })
    expect(await within(numeros).findByText('−3h45')).toHaveAttribute('data-tom', 'alerta')
    expect(espelho.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '21' })
    expect(espelho.ultima().consulta.get('ano')).toBe('2026')
    expect(espelho.ultima().consulta.get('mes')).toBe('10')
    espelho.conforme()
    expect(
      within(painel).getByText(
        'Você pode ver o espelho, mas só o RH altera marcações. Ajustes chegam como pedidos.',
      ),
    ).toBeInTheDocument()
    expect(within(painel).queryByRole('button', { name: /Editar/ })).toBeNull()
    await user.click(within(painel).getByRole('button', { name: 'Baixar PDF' }))
    await waitFor(() => expect(pdf.quantas()).toBe(1))
    pdf.conforme()
    await waitFor(() => expect(atestados.ultima().consulta.get('funcionarioId')).toBe('21'))
    atestados.conforme()
  })

  it('a aba Ponto da ficha da equipe usa a rota do gestor, sem folgas', async () => {
    const espelho = capturar('obterEspelhoDePontoDaPessoaDaEquipe')
    renderizarTela('/gestor/equipe/21?aba=ponto', { perfil: 'Gestor' })
    const tabela = await screen.findByRole('table', { name: 'Espelho da pessoa' })
    await waitFor(() => expect(espelho.quantas()).toBeGreaterThan(0))
    expect(within(tabela).queryByText('Folga')).toBeNull()
    expect(screen.getByRole('tab', { name: 'Ponto' })).toHaveAttribute('aria-selected', 'true')
  })

  it('atestados da equipe: painel com a coluna Pessoa e Ver arquivo pelo link do gestor', async () => {
    const baixar = capturar('baixarAtestadoDaEquipe')
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    const { user } = await abrir('/gestor/ponto-da-equipe/atestados')
    const painel = await screen.findByRole('dialog', { name: 'Atestados da equipe' })
    const tabela = await within(painel).findByRole('table', { name: 'Atestados da equipe' })
    expect(within(tabela).getAllByRole('columnheader')[0]).toHaveTextContent('Pessoa')
    const botoes = await within(tabela).findAllByRole('button', { name: /^Ver arquivo/ })
    await user.click(botoes[0]!)
    await waitFor(() => expect(baixar.quantas()).toBe(1))
    baixar.conforme()
    expect(within(painel).queryByRole('button', { name: /Validar|Recusar/ })).toBeNull()
  })
})
