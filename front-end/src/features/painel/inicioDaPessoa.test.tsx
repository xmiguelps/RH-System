import { screen, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Esquema } from '../../shared/api/tipos'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { renderizarTela } from '../../test/telas'

type Painel = Esquema<'PainelFuncionario'>
type Avisos = Esquema<'AvisoListaEu'>
type Aniversariantes = Esquema<'AniversariantesResposta'>
const ex = <T,>(op: string, status?: number, nome?: string) =>
  exemploDoContrato<T>(op, status, nome)
const PAINEL = ex<Painel>('obterPainelDoFuncionario')
const AVISOS = ex<Avisos>('listarMeusAvisos')
const ANIVERSARIANTES = ex<Aniversariantes>('listarAniversariantes')

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
})
afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
})

async function abrir(perfil: 'Funcionario' | 'Gestor' = 'Funcionario') {
  const tela = renderizarTela('/eu/inicio', { perfil })
  await screen.findByRole('heading', { level: 1, name: /^Olá, / })
  return tela
}

describe('Início da pessoa (telas 32 e 46)', { timeout: 20000 }, () => {
  it('"Férias disponíveis" com o saldo, "Vencem em" e o link que abre o painel de pedir; chama o painel sem parametros, conforme', async () => {
    const painel = capturar('obterPainelDoFuncionario')
    await abrir()
    const cartao = await screen.findByRole('region', { name: 'Férias disponíveis' })
    expect(within(cartao).getByText('18 dias')).toBeInTheDocument()
    expect(within(cartao).getByText('Vencem em 13/03/2027')).toBeInTheDocument()
    expect(within(cartao).getByRole('link', { name: 'Pedir férias' })).toHaveAttribute(
      'href',
      '/eu/ferias/pedir',
    )
    expect(painel.ultima().consulta.size).toBe(0)
    painel.conforme()
  })

  it('sem prazo, o "Vencem em" some', async () => {
    capturar('obterPainelDoFuncionario', {
      corpo: { ...PAINEL, ferias: { diasDisponiveis: 0, prazoParaTirarSaldo: null } },
    })
    await abrir()
    const cartao = await screen.findByRole('region', { name: 'Férias disponíveis' })
    expect(await within(cartao).findByText('0 dias')).toBeInTheDocument()
    expect(within(cartao).queryByText(/Vencem em/)).toBeNull()
  })

  it('"Próximo pagamento" vem do painel (nao chama obterProximoPagamento), com o mes do holerite e o link para os holerites', async () => {
    const proximo = capturar('obterProximoPagamento')
    await abrir()
    const cartao = await screen.findByRole('region', { name: 'Próximo pagamento' })
    expect(within(cartao).getByText('Até 6 de novembro')).toBeInTheDocument()
    expect(within(cartao).getByText(/O holerite de outubro aparece aqui/)).toBeInTheDocument()
    expect(within(cartao).getByRole('link', { name: 'Ver holerites' })).toHaveAttribute(
      'href',
      '/eu/holerites',
    )
    expect(proximo.quantas()).toBe(0)
  })

  it('empresa sem dia de pagamento (pagamento nulo): o cartao some', async () => {
    capturar('obterPainelDoFuncionario', {
      corpo: { ...PAINEL, proximoPagamento: { pagamento: null, competencia: null } },
    })
    await abrir()
    await screen.findByRole('region', { name: 'Férias disponíveis' })
    expect(screen.queryByRole('region', { name: 'Próximo pagamento' })).toBeNull()
  })

  it('"Seus benefícios": nome e detalhe de cada um, na ordem; so o nome quando nao ha detalhe; o link leva a /eu/beneficios', async () => {
    await abrir()
    const cartao = await screen.findByRole('region', { name: 'Seus benefícios' })
    expect(within(cartao).getAllByRole('listitem')).toHaveLength(PAINEL.beneficios.length)
    expect(within(cartao).getByText('Você e 1 dependente')).toBeInTheDocument()
    expect(within(cartao).getByText('Metrô Linha 1 e Linha 2')).toBeInTheDocument()
    expect(
      within(cartao).getByRole('link', { name: 'Ver detalhes dos benefícios' }),
    ).toHaveAttribute('href', '/eu/beneficios')
  })

  it('sem beneficios ativos diz "Você ainda não tem benefícios ativos."', async () => {
    capturar('obterPainelDoFuncionario', { corpo: { ...PAINEL, beneficios: [] } })
    await abrir()
    expect(await screen.findByText('Você ainda não tem benefícios ativos.')).toBeInTheDocument()
  })

  it('"Avisos": so os dois mais recentes, com trecho, quem publicou e "Ler aviso completo" para o aviso', async () => {
    const avisos = capturar('listarMeusAvisos')
    await abrir()
    const cartao = await screen.findByRole('region', { name: 'Avisos' })
    await within(cartao).findAllByRole('link', { name: 'Ler aviso completo' })
    expect(within(cartao).getAllByRole('listitem')).toHaveLength(2)
    expect(within(cartao).getByText(AVISOS.itens[0]!.titulo)).toBeInTheDocument()
    expect(within(cartao).queryByText(AVISOS.itens[2]!.titulo)).toBeNull()
    expect(within(cartao).getAllByRole('link', { name: 'Ler aviso completo' })[0]).toHaveAttribute(
      'href',
      `/eu/avisos/${AVISOS.itens[0]!.id}`,
    )
    expect(avisos.ultima().consulta.get('tamanhoPagina')).toBe('2')
  })

  it('sem avisos diz "Nenhum aviso por enquanto."', async () => {
    capturar('listarMeusAvisos', { corpo: { ...AVISOS, itens: [], total: 0, totalPaginas: 0 } })
    await abrir()
    expect(await screen.findByText('Nenhum aviso por enquanto.')).toBeInTheDocument()
  })

  it('"Aniversariantes": ninguem hoje e a lista dos proximos dias com iniciais, nome, setor e dd/mm; chama sem parametros, conforme', async () => {
    const lista = capturar('listarAniversariantes')
    await abrir()
    const cartao = await screen.findByRole('region', { name: 'Aniversariantes' })
    expect(await within(cartao).findByText(/Ninguém faz aniversário hoje\./)).toBeInTheDocument()
    expect(within(cartao).getByText(/Nos próximos 14 dias:/)).toBeInTheDocument()
    const linhas = within(cartao).getAllByRole('listitem')
    expect(linhas).toHaveLength(ANIVERSARIANTES.itens.length)
    expect(within(linhas[0]!).getByText('Marina Costa')).toBeInTheDocument()
    expect(within(linhas[0]!).getByText('Financeiro')).toBeInTheDocument()
    expect(within(linhas[0]!).getByText('15/10')).toBeInTheDocument()
    expect(lista.ultima().consulta.size).toBe(0)
    lista.conforme()
  })

  it('quem faz aniversario hoje aparece na frase', async () => {
    capturar('listarAniversariantes', {
      corpo: {
        ...ANIVERSARIANTES,
        itens: [{ ...ANIVERSARIANTES.itens[0]!, diasAte: 0, aniversarioEm: '2026-10-07' }],
      },
    })
    await abrir()
    const cartao = await screen.findByRole('region', { name: 'Aniversariantes' })
    expect(await within(cartao).findByText(/Hoje: Marina Costa\./)).toBeInTheDocument()
  })

  it('o gestor ve a faixa dos pedidos de ajuste do setor com "Analisar agora" para o Painel da equipe', async () => {
    const contadores = capturar('obterContadoresDoMenuDoGestor')
    await abrir('Gestor')
    const faixa = (
      await screen.findByText(/Você tem 2 pedidos de ajuste de ponto do setor/)
    ).closest('[role="status"]') as HTMLElement
    expect(faixa).toHaveTextContent(
      'Você tem 2 pedidos de ajuste de ponto do setor esperando sua análise.',
    )
    expect(within(faixa).getByRole('link', { name: 'Analisar agora' })).toHaveAttribute(
      'href',
      '/gestor/equipe',
    )
    expect(contadores.quantas()).toBeGreaterThan(0)
  })

  it('com 1 pedido a frase fica no singular; com 0 a faixa nao aparece; o funcionario nunca a ve', async () => {
    const base = ex<Esquema<'PainelContadoresGestor'>>('obterContadoresDoMenuDoGestor')
    capturar('obterContadoresDoMenuDoGestor', { corpo: { ...base, pedidosDeAjusteDePonto: 1 } })
    const { unmount } = await abrir('Gestor')
    expect(
      await screen.findByText(
        'Você tem 1 pedido de ajuste de ponto do setor esperando sua análise.',
      ),
    ).toBeInTheDocument()
    unmount()
    capturar('obterContadoresDoMenuDoGestor', { corpo: { ...base, pedidosDeAjusteDePonto: 0 } })
    const outra = await abrir('Gestor')
    await screen.findByRole('region', { name: 'Férias disponíveis' })
    expect(screen.queryByText(/esperando sua análise/)).toBeNull()
    outra.unmount()
    const contadores = capturar('obterContadoresDoMenuDoGestor')
    await abrir('Funcionario')
    await screen.findByRole('region', { name: 'Férias disponíveis' })
    expect(screen.queryByText(/esperando sua análise/)).toBeNull()
    expect(contadores.quantas()).toBe(0)
  })

  it('erro no painel mostra o aviso so nos cartoes dele; o ponto, os avisos e os aniversariantes continuam', async () => {
    capturar('obterPainelDoFuncionario', problema(500, { title: 'Erro interno.', status: 500 }))
    await abrir()
    expect(
      await screen.findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Férias disponíveis' })).toBeNull()
    expect(screen.getByRole('region', { name: 'Seu ponto hoje' })).toBeInTheDocument()
  })

  it('erro nos avisos fica so no cartao de avisos', async () => {
    capturar('listarMeusAvisos', problema(500, { title: 'Erro interno.', status: 500 }))
    await abrir()
    const cartao = await screen.findByRole('region', { name: 'Avisos' })
    expect(
      await within(cartao).findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Férias disponíveis' })).toBeInTheDocument()
  })

  it('o texto "responde por e-mail" nao existe nos cartoes de beneficios', async () => {
    await abrir()
    await screen.findByRole('region', { name: 'Seus benefícios' })
    expect(within(screen.getByRole('main')).queryByText(/responde por e-mail/i)).toBeNull()
  })
})
