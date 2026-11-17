import { screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Esquema } from '../../shared/api/tipos'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { escolherOpcao } from '../../test/formulario'
import { renderizarTela } from '../../test/telas'

type Lista = Esquema<'BeneficioAdesaoRhLista'>
type Adesao = Esquema<'BeneficioAdesaoRh'>
const ex = <T,>(op: string, status?: number, nome?: string) =>
  exemploDoContrato<T>(op, status, nome)
const LISTA = ex<Lista>('listarPedidosDeBeneficio')
const ADESAO = LISTA.itens[0]!
const problemaDoContrato = (op: string, status: number, nome?: string) =>
  problema(status, ex<Record<string, unknown>>(op, status, nome))
const comItens = (itens: Adesao[]) =>
  capturar('listarPedidosDeBeneficio', { corpo: { ...LISTA, itens } })

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
})
afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
})

async function abrir(rota = '/rh/pedidos-de-beneficios') {
  const tela = renderizarTela(rota)
  await screen.findByRole('heading', { level: 1, name: 'Pedidos de benefícios', hidden: true })
  const lista = await screen.findByRole('list', { name: 'Pedidos de benefícios' })
  return { ...tela, lista }
}
const cartao = (lista: HTMLElement) =>
  within(lista).getByRole('listitem', {
    name: `Benefício ${ADESAO.beneficio.nome} de ${ADESAO.funcionario.nome}`,
  })

describe('Pedidos de benefícios para o RH (tela 7.3)', { timeout: 20000 }, () => {
  it('abre em "Pendente" e mostra pessoa, beneficio, datas, desconto e os dependentes; chama a rota com situacao e pagina, conforme', async () => {
    const lista = capturar('listarPedidosDeBeneficio')
    const { lista: itens } = await abrir()
    expect(lista.ultima().consulta.get('situacao')).toBe('PENDENTE')
    expect(lista.ultima().consulta.get('pagina')).toBe('1')
    expect(lista.ultima().consulta.get('tamanhoPagina')).toBe('8')
    lista.conforme()
    const c = cartao(itens)
    expect(within(c).getByText('Bruno Lima')).toBeInTheDocument()
    expect(within(c).getByText(/Analista pleno, Financeiro/)).toBeInTheDocument()
    expect(within(c).getByText('Pedido em 07/10/2026')).toBeInTheDocument()
    expect(within(c).getByText('Responder até 15/10/2026')).toBeInTheDocument()
    expect(within(c).getByText('Começar em 01/11/2026')).toBeInTheDocument()
    expect(within(c).getByText('Autoriza o desconto em folha: sim')).toBeInTheDocument()
    const dependentes = within(c).getByRole('list', { name: 'Dependentes' })
    expect(
      within(dependentes).getByText('Helena Lima, FILHA, nascido em 21/05/2019'),
    ).toBeInTheDocument()
    expect(within(c).getByText(/Aprovar o pedido aprova junto os dependentes/)).toBeInTheDocument()
  })

  it('"Responder até" ja passado fica em destaque; sem pedido (dado pelo RH) diz isso', async () => {
    comItens([{ ...ADESAO, respostaPrevistaAte: '2026-10-01' }])
    const { lista } = await abrir()
    expect(within(cartao(lista)).getByText('Responder até 01/10/2026').className).toMatch(
      /text-erro-texto/,
    )
    comItens([{ ...ADESAO, situacao: 'APROVADO', pedidoEmUtc: null, respostaPrevistaAte: null }])
    renderizarTela('/rh/pedidos-de-beneficios?situacao=APROVADO')
    expect(await screen.findByText('Dado pelo RH, sem pedido')).toBeInTheDocument()
  })

  it('"Aprovar" a adesao chama aprovarAdesaoABeneficio sem corpo e recarrega a lista e os contadores', async () => {
    const aprovar = capturar('aprovarAdesaoABeneficio')
    const lista = capturar('listarPedidosDeBeneficio')
    const contadores = capturar('obterContadoresDoMenuDoRh')
    const { user, lista: itens } = await abrir()
    const antes = lista.quantas()
    await user.click(
      within(cartao(itens)).getByRole('button', {
        name: 'Aprovar o pedido de Plano odontológico de Bruno Lima',
      }),
    )
    await waitFor(() => expect(aprovar.quantas()).toBe(1))
    expect(aprovar.ultima().parametrosDoCaminho).toEqual({ adesaoId: String(ADESAO.adesaoId) })
    aprovar.conforme()
    expect(await screen.findByText('Pedido aprovado.')).toBeInTheDocument()
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(antes))
    expect(contadores.quantas()).toBeGreaterThan(0)
  })

  it('"Recusar" a adesao exige o motivo (ate 500) e manda { motivo }', async () => {
    const recusar = capturar('recusarAdesaoABeneficio')
    const { user, lista: itens } = await abrir()
    await user.click(
      within(cartao(itens)).getByRole('button', {
        name: 'Recusar o pedido de Plano odontológico de Bruno Lima',
      }),
    )
    const modal = await screen.findByRole('dialog', { name: 'Recusar pedido de benefício' })
    await user.click(within(modal).getByRole('button', { name: 'Recusar' }))
    expect(await within(modal).findByText('Escreva o motivo da recusa.')).toBeInTheDocument()
    expect(recusar.quantas()).toBe(0)
    await user.type(
      within(modal).getByLabelText('Motivo'),
      'O plano só aceita quem tem mais de um ano de casa.',
    )
    await user.click(within(modal).getByRole('button', { name: 'Recusar' }))
    await waitFor(() => expect(recusar.quantas()).toBe(1))
    expect(recusar.ultima().corpo).toEqual({
      motivo: 'O plano só aceita quem tem mais de um ano de casa.',
    })
    expect(recusar.ultima().parametrosDoCaminho).toEqual({ adesaoId: String(ADESAO.adesaoId) })
    recusar.conforme()
  })

  describe('dependente e trajeto de uma adesao ja aprovada', () => {
    const aprovada: Adesao = {
      ...ADESAO,
      situacao: 'APROVADO',
      trajetos: [
        {
          id: 7,
          origem: 'Rua A, 1',
          destino: 'Av. B, 2',
          linhasMeios: 'Metrô Linha 2',
          situacao: 'PENDENTE',
          pedidoEmUtc: '2026-10-06T12:00:00Z',
          respondidoEmUtc: null,
          motivoRecusa: null,
        },
      ],
    }
    async function abrirAprovada() {
      comItens([aprovada])
      return abrir('/rh/pedidos-de-beneficios?situacao=APROVADO')
    }

    it('a adesao aprovada nao tem "Aprovar" nem "Recusar" proprios; cada dependente e trajeto pendente tem os seus', async () => {
      const { lista } = await abrirAprovada()
      const c = cartao(lista)
      expect(
        within(c).queryByRole('button', { name: /de Plano odontológico de Bruno Lima/ }),
      ).toBeNull()
      expect(
        within(c).getByRole('button', { name: 'Aprovar o dependente Helena Lima' }),
      ).toBeInTheDocument()
      expect(
        within(c).getByRole('button', { name: 'Aprovar o trajeto de Bruno Lima' }),
      ).toBeInTheDocument()
    })

    it('aprovar o dependente chama aprovarDependenteDeBeneficio com a adesao e o dependente', async () => {
      const aprovar = capturar('aprovarDependenteDeBeneficio')
      const { user, lista } = await abrirAprovada()
      await user.click(
        within(cartao(lista)).getByRole('button', { name: 'Aprovar o dependente Helena Lima' }),
      )
      await waitFor(() => expect(aprovar.quantas()).toBe(1))
      expect(aprovar.ultima().parametrosDoCaminho).toEqual({
        adesaoId: String(ADESAO.adesaoId),
        dependenteId: String(ADESAO.dependentes[0]!.id),
      })
      aprovar.conforme()
    })

    it('recusar o dependente pede o motivo e chama recusarDependenteDeBeneficio', async () => {
      const recusar = capturar('recusarDependenteDeBeneficio')
      const { user, lista } = await abrirAprovada()
      await user.click(
        within(cartao(lista)).getByRole('button', { name: 'Recusar o dependente Helena Lima' }),
      )
      const modal = await screen.findByRole('dialog', { name: 'Recusar pedido de benefício' })
      await user.type(within(modal).getByLabelText('Motivo'), 'Falta a certidão')
      await user.click(within(modal).getByRole('button', { name: 'Recusar' }))
      await waitFor(() => expect(recusar.quantas()).toBe(1))
      expect(recusar.ultima().corpo).toEqual({ motivo: 'Falta a certidão' })
      recusar.conforme()
    })

    it('aprovar e recusar o trajeto chamam as rotas de trajeto', async () => {
      const aprovar = capturar('aprovarTrajetoDeBeneficio')
      const recusar = capturar('recusarTrajetoDeBeneficio')
      const { user, lista } = await abrirAprovada()
      await user.click(
        within(cartao(lista)).getByRole('button', { name: 'Aprovar o trajeto de Bruno Lima' }),
      )
      await waitFor(() => expect(aprovar.quantas()).toBe(1))
      expect(aprovar.ultima().parametrosDoCaminho).toEqual({
        adesaoId: String(ADESAO.adesaoId),
        trajetoId: '7',
      })
      await user.click(
        within(cartao(lista)).getByRole('button', { name: 'Recusar o trajeto de Bruno Lima' }),
      )
      const modal = await screen.findByRole('dialog', { name: 'Recusar pedido de benefício' })
      await user.type(within(modal).getByLabelText('Motivo'), 'Trajeto incompleto')
      await user.click(within(modal).getByRole('button', { name: 'Recusar' }))
      await waitFor(() => expect(recusar.quantas()).toBe(1))
      expect(recusar.ultima().corpo).toEqual({ motivo: 'Trajeto incompleto' })
      expect(recusar.ultima().parametrosDoCaminho).toEqual({
        adesaoId: String(ADESAO.adesaoId),
        trajetoId: '7',
      })
    })

    it('409 mostra o texto do contrato e pede a lista de novo (outra pessoa decidiu antes)', async () => {
      capturar(
        'aprovarDependenteDeBeneficio',
        problemaDoContrato('aprovarDependenteDeBeneficio', 409, 'adesaoNaoAtiva'),
      )
      const lista = capturar('listarPedidosDeBeneficio', { corpo: { ...LISTA, itens: [aprovada] } })
      const { user, lista: itens } = await abrir('/rh/pedidos-de-beneficios?situacao=APROVADO')
      const antes = lista.quantas()
      await user.click(
        within(cartao(itens)).getByRole('button', { name: 'Aprovar o dependente Helena Lima' }),
      )
      const detalhe = ex<{ detail: string }>(
        'aprovarDependenteDeBeneficio',
        409,
        'adesaoNaoAtiva',
      ).detail
      expect(await screen.findByText(detalhe)).toBeInTheDocument()
      await waitFor(() => expect(lista.quantas()).toBeGreaterThan(antes))
    })
  })

  it('422 FUNCIONARIO_INATIVO ao aprovar a adesao mostra o texto do contrato dentro do cartao', async () => {
    capturar('aprovarAdesaoABeneficio', problemaDoContrato('aprovarAdesaoABeneficio', 422))
    const { user, lista: itens } = await abrir()
    const c = cartao(itens)
    await user.click(
      within(c).getByRole('button', {
        name: 'Aprovar o pedido de Plano odontológico de Bruno Lima',
      }),
    )
    const detalhe = ex<{ detail: string }>('aprovarAdesaoABeneficio', 422).detail
    expect(await within(c).findByText(detalhe)).toBeInTheDocument()
  })

  it('situacao, beneficio e busca viram parametros da chamada', async () => {
    const lista = capturar('listarPedidosDeBeneficio')
    const { user, roteador } = await abrir()
    await escolherOpcao(user, 'Situação', 'Recusado')
    await waitFor(() => expect(lista.ultima().consulta.get('situacao')).toBe('RECUSADO'))
    expect(roteador.state.location.search).toContain('situacao=RECUSADO')
    await escolherOpcao(user, 'Benefício', 'Plano odontológico')
    await waitFor(() => expect(lista.ultima().consulta.get('beneficioId')).toBe('3'))
    await user.type(screen.getByRole('searchbox', { name: 'Buscar colaborador' }), 'Bruno')
    await waitFor(() => expect(lista.ultima().consulta.get('busca')).toBe('Bruno'))
  })

  it('vazio e erro', async () => {
    capturar('listarPedidosDeBeneficio', {
      corpo: { ...LISTA, itens: [], total: 0, totalPaginas: 0 },
    })
    const { unmount } = renderizarTela('/rh/pedidos-de-beneficios')
    expect(
      await screen.findByText('Nenhum pedido de benefício esperando o RH.'),
    ).toBeInTheDocument()
    unmount()
    capturar('listarPedidosDeBeneficio', problema(500, { title: 'Erro interno.', status: 500 }))
    renderizarTela('/rh/pedidos-de-beneficios')
    expect(
      await screen.findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
  })
})
