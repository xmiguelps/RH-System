import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Esquema } from '../../shared/api/tipos'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { renderizarTela } from '../../test/telas'

type Ficha = Esquema<'FeriasFicha'>
const ex = <T,>(op: string, status?: number, nome?: string) =>
  exemploDoContrato<T>(op, status, nome)
const FICHA = ex<Ficha>('obterFeriasDaFichaDoFuncionario')

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
})
afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
})

const abrirAba = async () => {
  const tela = renderizarTela('/rh/colaboradores/12?aba=ferias')
  const tabela = await screen.findByRole('table', { name: 'Períodos de férias' })
  return { ...tela, tabela }
}

describe('Ficha do RH, aba Férias (tela 05)', { timeout: 20000 }, () => {
  it('tres cartoes, a tabela com periodo, dias e situacao e o rodape; chama so a rota da aba, conforme', async () => {
    const aba = capturar('obterFeriasDaFichaDoFuncionario')
    const { tabela } = await abrirAba()
    const resumo = await screen.findByRole('region', { name: 'Resumo das férias' })
    expect(await within(resumo).findByText('18 dias')).toBeInTheDocument()
    expect(within(resumo).getByText('14/03/2026 a 13/03/2027')).toBeInTheDocument()
    expect(within(resumo).getByText('13/03/2027')).toBeInTheDocument()
    expect(within(resumo).queryByText(/Pedidos em análise|Próximas férias/)).toBeNull()
    expect(
      within(tabela)
        .getAllByRole('columnheader')
        .map((c) => c.textContent),
    ).toEqual(['Período', 'Dias', 'Situação'])
    const primeira = FICHA.ferias[0]!
    expect(
      (await within(tabela).findAllByText(/\d{2}\/\d{2}\/\d{4} a \d{2}\/\d{2}\/\d{4}/)).length,
    ).toBeGreaterThan(0)
    expect(within(tabela).getAllByRole('row')).toHaveLength(FICHA.ferias.length + 1)
    expect(primeira).toBeDefined()
    expect(
      screen.getByText(
        'Pedidos feitos pelo colaborador aparecem aqui depois que o gestor analisa.',
      ),
    ).toBeInTheDocument()
    expect(aba.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '12' })
    aba.conforme()
  })

  it('sem nenhum pedido diz "Nenhum período de férias registrado ainda."', async () => {
    capturar('obterFeriasDaFichaDoFuncionario', { corpo: { ...FICHA, ferias: [] } })
    await abrirAba()
    expect(
      await screen.findByText('Nenhum período de férias registrado ainda.'),
    ).toBeInTheDocument()
  })

  it('periodo atual e prazo nulos aparecem como traco', async () => {
    capturar('obterFeriasDaFichaDoFuncionario', {
      corpo: {
        ...FICHA,
        resumo: { ...FICHA.resumo, periodoAquisitivoAtual: null, prazoParaTirarSaldo: null },
      },
    })
    await abrirAba()
    const resumo = await screen.findByRole('region', { name: 'Resumo das férias' })
    await waitFor(() => expect(within(resumo).getAllByText('—')).toHaveLength(2))
  })

  it('a linha traz a frase de cada decisao, com o motivo da recusa', async () => {
    capturar('obterFeriasDaFichaDoFuncionario', {
      corpo: {
        ...FICHA,
        ferias: [
          {
            ...FICHA.ferias[0]!,
            situacao: 'REJEITADA',
            decisaoRh: {
              decisao: 'REJEITADA',
              decididaPor: { id: 1, nome: 'Ana Souza', ativo: true },
              decididaEmUtc: '2026-10-08T12:00:00Z',
              automatica: false,
              observacao: 'Fechamento da folha.',
            },
          },
        ],
      },
    })
    const { tabela } = await abrirAba()
    expect(
      await within(tabela).findByText('Recusado por Ana Souza em 08/10: Fechamento da folha.'),
    ).toBeInTheDocument()
  })

  it('pessoa inativa: o botao "Registrar férias" nao existe', async () => {
    capturar('obterFichaDoFuncionario', {
      corpo: { ...ex<Esquema<'FuncionarioFichaRh'>>('obterFichaDoFuncionario'), ativo: false },
    })
    renderizarTela('/rh/colaboradores/12?aba=ferias')
    await screen.findByRole('table', { name: 'Períodos de férias' })
    await new Promise((r) => setTimeout(r, 300))
    expect(screen.queryByRole('button', { name: 'Registrar férias' })).toBeNull()
  })

  it('erro ao carregar a aba mostra o aviso com "Tentar de novo"', async () => {
    capturar(
      'obterFeriasDaFichaDoFuncionario',
      problema(500, { title: 'Erro interno.', status: 500 }),
    )
    renderizarTela('/rh/colaboradores/12?aba=ferias')
    expect(
      await screen.findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
  })
})

describe('Modal "Registrar férias" (tela 06)', { timeout: 20000 }, () => {
  async function abrirModal() {
    const tela = await abrirAba()
    const botao = await screen.findByRole('button', { name: 'Registrar férias' })
    await waitFor(() => expect(botao).toBeEnabled())
    await tela.user.click(botao)
    const modal = await screen.findByRole('dialog', { name: /^Registrar férias de / })
    return { ...tela, modal }
  }
  const datas = (modal: HTMLElement, inicio: string, fim: string) => {
    fireEvent.change(within(modal).getByLabelText('Primeiro dia'), { target: { value: inicio } })
    fireEvent.change(within(modal).getByLabelText('Último dia'), { target: { value: fim } })
  }

  it('titulo com o primeiro nome, tres numeros do resumo (periodo do SALDO), sem "Avisar" por e-mail e sem "Sobram"', async () => {
    const { modal } = await abrirModal()
    expect(modal).toHaveAccessibleName('Registrar férias de Bruno')
    const numeros = within(modal).getByRole('region', { name: 'Saldo para registrar' })
    expect(within(numeros).getByText('18 dias')).toBeInTheDocument()
    expect(within(numeros).getByText('14/03/2025 a 13/03/2026')).toBeInTheDocument()
    expect(within(numeros).getByText('13/03/2027')).toBeInTheDocument()
    expect(within(modal).queryByText(/Avisar|por e-mail/)).toBeNull()
    expect(
      within(modal).getByText('As férias aparecem na aba Férias e no ponto como dias de férias.'),
    ).toBeInTheDocument()
    expect(within(modal).getByLabelText('Observação (opcional)')).toBeInTheDocument()
    expect(within(modal).getByRole('button', { name: 'Registrar férias' })).toBeDisabled()
    datas(modal, '2026-12-07', '2026-12-24')
    const conf = ex<Esquema<'FeriasConferencia'>>('conferirRegistroDeFerias')
    expect(await within(modal).findByText(`${conf.totalDias} dias corridos.`)).toBeInTheDocument()
    expect(within(modal).queryByText(/Sobram/)).toBeNull()
  })

  it('confere pela rota do RH com a pessoa no caminho, so com as duas datas; mostra o aviso do pagamento e todas as regras', async () => {
    const conferir = capturar('conferirRegistroDeFerias')
    const { modal } = await abrirModal()
    datas(modal, '2026-12-24', '2026-12-07')
    await new Promise((r) => setTimeout(r, 450))
    expect(conferir.quantas()).toBe(0)
    datas(modal, '2026-12-07', '2026-12-24')
    await waitFor(() => expect(conferir.quantas()).toBe(1))
    expect(conferir.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '12' })
    expect(conferir.ultima().corpo).toEqual({
      dataInicio: '2026-12-07',
      dataFim: '2026-12-24',
      abonoPecuniario: false,
      diasAbono: 0,
    })
    conferir.conforme()
    const conf = ex<Esquema<'FeriasConferencia'>>('conferirRegistroDeFerias')
    for (const regra of conf.regras)
      expect(await within(modal).findByText(regra.mensagem)).toBeInTheDocument()
    if (conf.pagamentoAte)
      expect(
        within(modal).getByText(/O pagamento das férias precisa sair até 2 dias antes do início/),
      ).toBeInTheDocument()
  })

  it('pagamentoAte nulo esconde o aviso', async () => {
    const conf = ex<Esquema<'FeriasConferencia'>>('conferirRegistroDeFerias')
    capturar('conferirRegistroDeFerias', { corpo: { ...conf, pagamentoAte: null } })
    const { modal } = await abrirModal()
    datas(modal, '2026-12-07', '2026-12-24')
    await within(modal).findByText(`${conf.totalDias} dias corridos.`)
    expect(within(modal).queryByText(/O pagamento das férias precisa sair/)).toBeNull()
  })

  it('registrar manda o corpo do contrato (com observacao), avisa, fecha e pede a ficha e a aba de novo', async () => {
    const registrar = capturar('registrarFeriasPeloRh')
    const aba = capturar('obterFeriasDaFichaDoFuncionario')
    const ficha = capturar('obterFichaDoFuncionario')
    const { user, modal } = await abrirModal()
    datas(modal, '2026-12-07', '2026-12-24')
    await user.type(
      within(modal).getByLabelText('Observação (opcional)'),
      'Combinado com a gestora em 06/10',
    )
    await waitFor(() =>
      expect(within(modal).getByRole('button', { name: 'Registrar férias' })).toBeEnabled(),
    )
    const antesAba = aba.quantas()
    const antesFicha = ficha.quantas()
    await user.click(within(modal).getByRole('button', { name: 'Registrar férias' }))
    await waitFor(() => expect(registrar.quantas()).toBe(1))
    expect(registrar.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '12' })
    expect(registrar.ultima().corpo).toEqual({
      dataInicio: '2026-12-07',
      dataFim: '2026-12-24',
      abonoPecuniario: false,
      diasAbono: 0,
      adiantar13Salario: false,
      observacao: 'Combinado com a gestora em 06/10',
    })
    registrar.conforme()
    expect(await screen.findByText('Férias registradas.')).toBeInTheDocument()
    await waitFor(() =>
      expect(screen.queryByRole('dialog', { name: /^Registrar férias de / })).toBeNull(),
    )
    await waitFor(() => expect(aba.quantas()).toBeGreaterThan(antesAba))
    await waitFor(() => expect(ficha.quantas()).toBeGreaterThan(antesFicha))
  })

  it.each(['padrao'] as const)(
    '422 FERIAS_REGRAS_NAO_ATENDIDAS (%s) mostra o texto e as regras do erro e mantem o modal aberto',
    async (nome) => {
      capturar(
        'registrarFeriasPeloRh',
        problema(422, ex<Record<string, unknown>>('registrarFeriasPeloRh', 422, nome)),
      )
      const { user, modal } = await abrirModal()
      datas(modal, '2026-12-07', '2026-12-24')
      await waitFor(() =>
        expect(within(modal).getByRole('button', { name: 'Registrar férias' })).toBeEnabled(),
      )
      await user.click(within(modal).getByRole('button', { name: 'Registrar férias' }))
      const erro = ex<{ detail: string; regras?: { mensagem: string }[] }>(
        'registrarFeriasPeloRh',
        422,
        nome,
      )
      expect(await within(modal).findByText(erro.detail)).toBeInTheDocument()
      for (const regra of erro.regras ?? [])
        expect(within(modal).getByText(regra.mensagem)).toBeInTheDocument()
      expect(screen.getByRole('dialog', { name: /^Registrar férias de / })).toBeInTheDocument()
    },
  )

  it('"Vender parte" abre "Quantos dias" (1 a 10) e a conferencia leva o abono', async () => {
    const conferir = capturar('conferirRegistroDeFerias')
    const { user, modal } = await abrirModal()
    datas(modal, '2026-12-07', '2026-12-24')
    await user.click(
      within(modal).getByRole('checkbox', {
        name: 'Vender parte das férias (abono de até 10 dias)',
      }),
    )
    const campo = within(modal).getByLabelText('Quantos dias')
    expect(campo).toHaveAttribute('min', '1')
    expect(campo).toHaveAttribute('max', '10')
    fireEvent.change(campo, { target: { value: '4' } })
    await waitFor(() =>
      expect(conferir.ultima().corpo).toMatchObject({ abonoPecuniario: true, diasAbono: 4 }),
    )
  })
})

describe('Aba Férias da ficha da equipe (tela 7.1)', { timeout: 20000 }, () => {
  it('lista todos os pedidos da pessoa pela rota do gestor, sem "Registrar férias" e com as frases das decisoes', async () => {
    const aba = capturar('obterFeriasDaPessoaDaEquipe')
    renderizarTela('/gestor/equipe/21?aba=ferias', { perfil: 'Gestor' })
    const tabela = await screen.findByRole('table', { name: 'Períodos de férias' })
    await within(tabela).findAllByText(/\d{2}\/\d{2}\/\d{4} a \d{2}\/\d{2}\/\d{4}/)
    expect(screen.queryByRole('button', { name: 'Registrar férias' })).toBeNull()
    expect(aba.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '21' })
    aba.conforme()
  })

  it('o pedido que espera a resposta de uma sugestao mostra o motivo, e 404 (outro setor) mostra o erro', async () => {
    capturar(
      'obterFeriasDaPessoaDaEquipe',
      problema(404, { title: 'O registro não foi encontrado.', status: 404 }),
    )
    renderizarTela('/gestor/equipe/21?aba=ferias', { perfil: 'Gestor' })
    expect(await screen.findByText('O registro não foi encontrado.')).toBeInTheDocument()
  })
})
