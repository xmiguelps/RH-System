import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Esquema } from '../../../shared/api/tipos'
import { exemploDoContrato } from '../../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../../test/capturar'
import { escolherOpcao } from '../../../test/formulario'
import { renderizarTela } from '../../../test/telas'

type Calendario = Esquema<'PagamentoCalendario'>
type Excecoes = Esquema<'PagamentoExcecaoLista'>
const ex = <T,>(op: string, status?: number, nome?: string) =>
  exemploDoContrato<T>(op, status, nome)
const CALENDARIO = ex<Calendario>('obterCalendarioDePagamento')
const EXCECOES = ex<Excecoes>('listarExcecoesDePagamento')

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
})
afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
})

async function abrir() {
  const tela = renderizarTela('/rh/cadastros/pagamento')
  const tabela = await screen.findByRole('table', { name: 'Calendário de pagamento' })
  return { ...tela, tabela }
}

describe('Cadastros: Pagamento (tela 7.4)', { timeout: 20000 }, () => {
  it('"Pagamento" e um item da lista de Cadastros e abre a tela', async () => {
    const tela = renderizarTela('/rh/cadastros/cargos')
    await tela.user.click(await screen.findByRole('link', { name: /^Pagamento/ }))
    await waitFor(() =>
      expect(tela.roteador.state.location.pathname).toBe('/rh/cadastros/pagamento'),
    )
    expect(
      await screen.findByRole('table', { name: 'Calendário de pagamento' }),
    ).toBeInTheDocument()
  })

  it('regra padrao "Todo dia 6" (da empresa) com o link para alterar em Empresa', async () => {
    await abrir()
    expect(await screen.findByText('Todo dia 6')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Alterar o dia em Empresa' })).toHaveAttribute(
      'href',
      '/rh/cadastros/empresa',
    )
  })

  it('sem dia definido diz "O dia de pagamento ainda não foi definido." e mantem o link', async () => {
    const empresa = ex<Esquema<'EmpresaConfiguracao'>>('obterEmpresa')
    capturar('obterEmpresa', {
      corpo: { ...empresa, diaPagamento: null } as unknown as Esquema<'EmpresaConfiguracao'>,
    })
    await abrir()
    expect(
      await screen.findByText('O dia de pagamento ainda não foi definido.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Alterar o dia em Empresa' })).toBeInTheDocument()
  })

  it('calendario do ano atual: mes, data, origem e os ajustes em frase; pede com ano, conforme o contrato', async () => {
    const calendario = capturar('obterCalendarioDePagamento')
    const { tabela } = await abrir()
    await within(tabela).findAllByText('Regra')
    expect(calendario.ultima().consulta.get('ano')).toBe('2026')
    calendario.conforme()
    expect(within(tabela).getAllByRole('row')).toHaveLength(CALENDARIO.meses.length + 1)
    for (const mes of CALENDARIO.meses) {
      const linha = within(tabela)
        .getByText(
          new RegExp(
            `^${['Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho', 'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro'][mes.mes - 1]}$`,
          ),
        )
        .closest('tr')!
      expect(
        within(linha).getByText(
          `${mes.data.slice(8, 10)}/${mes.data.slice(5, 7)}/${mes.data.slice(0, 4)}`,
        ),
      ).toBeInTheDocument()
    }
  })

  it.each([
    ['ULTIMO_DIA_DO_MES', 'O mês não tem o dia 6: vale o último dia'],
    ['ANTECIPADO_FIM_DE_SEMANA', 'Antecipado: caiu no fim de semana'],
    ['ANTECIPADO_FERIADO', 'Antecipado: caiu em feriado'],
  ] as const)(
    'ajuste %s aparece como "%s"; "EXCECAO" aparece como Exceção',
    async (ajuste, frase) => {
      capturar('obterCalendarioDePagamento', {
        corpo: {
          ano: 2026,
          meses: [
            { ano: 2026, mes: 2, data: '2026-02-06', origem: 'EXCECAO', ajustes: [ajuste] },
            { ano: 2026, mes: 3, data: '2026-03-06', origem: 'REGRA', ajustes: [] },
          ],
        },
      })
      const { tabela } = await abrir()
      expect(await within(tabela).findByText(frase)).toBeInTheDocument()
      expect(within(tabela).getByText('Exceção')).toBeInTheDocument()
      expect(within(tabela).getByText('—')).toBeInTheDocument()
    },
  )

  it('trocar o ano pede o calendario do ano escolhido', async () => {
    const calendario = capturar('obterCalendarioDePagamento')
    const { user } = await abrir()
    await escolherOpcao(user, 'Ano', '2027')
    await waitFor(() => expect(calendario.ultima().consulta.get('ano')).toBe('2027'))
  })

  it('exceções: mes e ano, data e "Remover"; a lista chama sem ano (do mes atual em diante), conforme', async () => {
    const lista = capturar('listarExcecoesDePagamento')
    await abrir()
    const tabela = await screen.findByRole('table', { name: 'Exceções de pagamento' })
    await within(tabela).findAllByRole('button', { name: /^Remover a exceção de / })
    expect(lista.ultima().consulta.size).toBe(0)
    lista.conforme()
    expect(within(tabela).getAllByRole('row')).toHaveLength(EXCECOES.itens.length + 1)
  })

  it('sem excecoes diz "Nenhuma exceção cadastrada."', async () => {
    capturar('listarExcecoesDePagamento', { corpo: { itens: [] } })
    await abrir()
    expect(await screen.findByText('Nenhuma exceção cadastrada.')).toBeInTheDocument()
  })

  it('"Remover" pede confirmacao, chama removerExcecaoDePagamento com ano e mes e recarrega o calendario, as excecoes e o painel', async () => {
    const remover = capturar('removerExcecaoDePagamento', { status: 204 })
    const calendario = capturar('obterCalendarioDePagamento')
    const excecoes = capturar('listarExcecoesDePagamento')
    const primeira = EXCECOES.itens[0]!
    const { user } = await abrir()
    const tabela = await screen.findByRole('table', { name: 'Exceções de pagamento' })
    await user.click(
      await within(tabela)
        .findAllByRole('button', { name: /^Remover a exceção de / })
        .then((b) => b[0]!),
    )
    const confirmacao = await screen.findByRole('alertdialog')
    expect(
      within(confirmacao).getByText('O pagamento desse mês volta a seguir a regra.'),
    ).toBeInTheDocument()
    expect(remover.quantas()).toBe(0)
    const [antesCalendario, antesExcecoes] = [calendario.quantas(), excecoes.quantas()]
    await user.click(within(confirmacao).getByRole('button', { name: 'Remover' }))
    await waitFor(() => expect(remover.quantas()).toBe(1))
    expect(remover.ultima().parametrosDoCaminho).toEqual({
      ano: String(primeira.ano),
      mes: String(primeira.mes),
    })
    expect(await screen.findByText('Exceção removida.')).toBeInTheDocument()
    await waitFor(() => expect(calendario.quantas()).toBeGreaterThan(antesCalendario))
    await waitFor(() => expect(excecoes.quantas()).toBeGreaterThan(antesExcecoes))
  })

  describe('"Nova exceção"', () => {
    async function abrirModal() {
      const tela = await abrir()
      await tela.user.click(screen.getByRole('button', { name: 'Nova exceção' }))
      const modal = await screen.findByRole('dialog', { name: 'Nova exceção de pagamento' })
      return { ...tela, modal }
    }

    it('avisa que a data vale como esta (nao antecipa) e que substitui a que ja existir', async () => {
      const { modal } = await abrirModal()
      expect(
        within(modal).getByText(
          /A data da exceção vale como está: não é antecipada por fim de semana nem por feriado/,
        ),
      ).toBeInTheDocument()
      expect(
        within(modal).getByText(/Se já existir uma exceção para o mês, esta a substitui\./),
      ).toBeInTheDocument()
    })

    it('data fora do mes e do ano escolhidos avisa e nao chama a API', async () => {
      const definir = capturar('definirExcecaoDePagamento')
      const { user, modal } = await abrirModal()
      await user.clear(within(modal).getByLabelText('Ano'))
      await user.type(within(modal).getByLabelText('Ano'), '2026')
      await escolherOpcao(user, 'Mês', 'Novembro')
      fireEvent.change(within(modal).getByLabelText('Data do pagamento'), {
        target: { value: '2026-12-04' },
      })
      await user.click(within(modal).getByRole('button', { name: 'Salvar exceção' }))
      expect(
        await within(modal).findByText('A data tem de cair dentro do mês e do ano escolhidos.'),
      ).toBeInTheDocument()
      expect(definir.quantas()).toBe(0)
    })

    it('salvar manda ano e mes no caminho e { dataPagamento } no corpo, avisa e recarrega calendario, excecoes e painel', async () => {
      const definir = capturar('definirExcecaoDePagamento')
      const calendario = capturar('obterCalendarioDePagamento')
      const { user, modal } = await abrirModal()
      await escolherOpcao(user, 'Mês', 'Novembro')
      fireEvent.change(within(modal).getByLabelText('Data do pagamento'), {
        target: { value: '2026-11-04' },
      })
      const antes = calendario.quantas()
      await user.click(within(modal).getByRole('button', { name: 'Salvar exceção' }))
      await waitFor(() => expect(definir.quantas()).toBe(1))
      expect(definir.ultima().parametrosDoCaminho).toEqual({ ano: '2026', mes: '11' })
      expect(definir.ultima().corpo).toEqual({ dataPagamento: '2026-11-04' })
      definir.conforme()
      expect(await screen.findByText('Exceção salva.')).toBeInTheDocument()
      await waitFor(() => expect(calendario.quantas()).toBeGreaterThan(antes))
      await waitFor(() =>
        expect(screen.queryByRole('dialog', { name: 'Nova exceção de pagamento' })).toBeNull(),
      )
    })

    it('400 do servidor mostra o texto do campo da data dentro da janela', async () => {
      capturar(
        'definirExcecaoDePagamento',
        problema(400, {
          title: 'Confira os dados informados.',
          status: 400,
          errors: { dataPagamento: ['A data tem de estar dentro do mês.'] },
        }),
      )
      const { user, modal } = await abrirModal()
      await escolherOpcao(user, 'Mês', 'Outubro')
      fireEvent.change(within(modal).getByLabelText('Data do pagamento'), {
        target: { value: '2026-10-09' },
      })
      await user.click(within(modal).getByRole('button', { name: 'Salvar exceção' }))
      expect(
        await within(modal).findByText('A data tem de estar dentro do mês.'),
      ).toBeInTheDocument()
    })
  })

  it('erro ao carregar o calendario mostra o aviso com "Tentar de novo"', async () => {
    capturar('obterCalendarioDePagamento', problema(500, { title: 'Erro interno.', status: 500 }))
    renderizarTela('/rh/cadastros/pagamento')
    expect(
      await screen.findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
  })
})
