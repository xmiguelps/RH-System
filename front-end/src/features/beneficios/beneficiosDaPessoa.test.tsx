import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Esquema } from '../../shared/api/tipos'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { renderizarTela } from '../../test/telas'

type Meus = Esquema<'BeneficioAdesaoEuResposta'>
const ex = <T,>(op: string, status?: number, nome?: string) =>
  exemploDoContrato<T>(op, status, nome)
const MEUS = ex<Meus>('obterMeusBeneficios')
const ODONTO = MEUS.disponiveis[0]!
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

async function abrir(perfil: 'Funcionario' | 'Gestor' | 'RH' = 'Funcionario') {
  const tela = renderizarTela('/eu/beneficios', { perfil })
  await screen.findByRole('heading', { level: 1, name: 'Benefícios' })
  await screen.findByRole('list', { name: '' }).catch(() => undefined)
  return tela
}
const cartao = async (nome: string) => {
  const secao = await screen.findByRole('region', { name: 'Seus benefícios' })
  return within(secao).findByRole('listitem', { name: nome })
}

describe('Benefícios da pessoa (tela 44)', { timeout: 20000 }, () => {
  it('cartoes dos beneficios ativos, disponiveis, pedidos e o rodape, conforme o contrato (sem parametros)', async () => {
    const meus = capturar('obterMeusBeneficios')
    await abrir()
    const plano = await cartao('Plano de saúde')
    expect(within(plano).getByText('Ativo')).toBeInTheDocument()
    expect(
      within(plano).getByText('Consultas, exames e internação, com coparticipação'),
    ).toBeInTheDocument()
    expect(within(plano).getByText('Quem está no plano: Você e 1 dependente')).toBeInTheDocument()
    expect(within(await cartao('Vale-refeição')).queryByRole('button')).toBeNull()
    const disponiveis = screen.getByRole('region', { name: 'Disponíveis para você' })
    expect(within(disponiveis).getAllByRole('listitem')).toHaveLength(MEUS.disponiveis.length)
    expect(
      within(disponiveis).getByText(
        /O RH analisa e responde em até 5 dias úteis; você vê a resposta em Seus pedidos\./,
      ),
    ).toBeInTheDocument()
    const pedidos = screen.getByRole('table', { name: 'Seus pedidos' })
    expect(
      within(pedidos).getByText('Inclusão de dependente no plano de saúde'),
    ).toBeInTheDocument()
    expect(within(pedidos).getAllByText('Aprovado')).toHaveLength(2)
    expect(screen.getByText('Dúvida sobre algum benefício? Fale com o RH')).toBeInTheDocument()
    expect(meus.ultima().consulta.size).toBe(0)
    meus.conforme()
  })

  it('o trajeto mostra so as linhas ou os meios: a origem (endereco de casa) nao aparece na tela', async () => {
    await abrir()
    const vt = await cartao('Vale-transporte')
    expect(within(vt).getByText('Trajeto: Ônibus 175T-10 e Metrô Linha 2')).toBeInTheDocument()
    expect(document.body.textContent).not.toContain('Rua das Acácias')
    expect(document.body.textContent).not.toContain('Av. Paulista')
  })

  it('"Incluir dependente" so existe no beneficio que aceita dependentes e "Atualizar trajeto" so no que aceita trajeto', async () => {
    await abrir()
    expect(
      within(await cartao('Plano de saúde')).getByRole('button', { name: 'Incluir dependente' }),
    ).toBeInTheDocument()
    expect(
      within(await cartao('Plano de saúde')).queryByRole('button', { name: 'Atualizar trajeto' }),
    ).toBeNull()
    expect(
      within(await cartao('Vale-transporte')).getByRole('button', { name: 'Atualizar trajeto' }),
    ).toBeInTheDocument()
    expect(
      within(await cartao('Vale-transporte')).queryByRole('button', { name: 'Incluir dependente' }),
    ).toBeNull()
  })

  it('o gestor e o RH veem a mesma tela (rota /api/eu)', async () => {
    const meus = capturar('obterMeusBeneficios')
    await abrir('Gestor')
    await cartao('Plano de saúde')
    expect(meus.quantas()).toBeGreaterThan(0)
  })

  it('em "Seus pedidos" o pendente mostra ate quando o RH responde e o recusado mostra o motivo', async () => {
    capturar('obterMeusBeneficios', {
      corpo: {
        ...MEUS,
        pedidos: [
          {
            tipo: 'ADESAO',
            id: 90,
            adesaoId: 44,
            titulo: 'Adesão ao plano odontológico',
            pedidoEmUtc: '2026-10-05T12:00:00Z',
            situacao: 'PENDENTE',
            motivoRecusa: null,
            respostaPrevistaAte: '2026-10-12',
          },
          {
            tipo: 'DEPENDENTE',
            id: 91,
            adesaoId: 42,
            titulo: 'Inclusão de dependente no plano de saúde',
            pedidoEmUtc: '2026-09-01T12:00:00Z',
            situacao: 'RECUSADO',
            motivoRecusa: 'Faltou a certidão de nascimento.',
            respostaPrevistaAte: '2026-09-08',
          },
        ],
      },
    })
    await abrir()
    const pedidos = await screen.findByRole('table', { name: 'Seus pedidos' })
    expect(await within(pedidos).findByText('Resposta até 12/10/2026')).toBeInTheDocument()
    expect(within(pedidos).getByText('Pendente')).toBeInTheDocument()
    expect(
      within(pedidos).getByText('Motivo: Faltou a certidão de nascimento.'),
    ).toBeInTheDocument()
    expect(within(pedidos).getByText('Recusado')).toBeInTheDocument()
  })

  it('o disponivel com ultimo pedido recusado mostra o motivo e continua podendo pedir', async () => {
    capturar('obterMeusBeneficios', {
      corpo: {
        ...MEUS,
        disponiveis: [
          {
            ...ODONTO,
            ultimoPedido: {
              situacao: 'RECUSADO',
              motivoRecusa: 'Pedido fora do prazo.',
              respondidoEmUtc: '2026-09-10T12:00:00Z',
            },
          },
        ],
      },
    })
    await abrir()
    const odonto = await within(
      await screen.findByRole('region', { name: 'Disponíveis para você' }),
    ).findByRole('listitem', {
      name: 'Plano odontológico',
    })
    expect(
      within(odonto).getByText('Seu último pedido foi recusado: Pedido fora do prazo.'),
    ).toBeInTheDocument()
    expect(within(odonto).getByRole('link', { name: 'Quero aderir' })).toBeInTheDocument()
  })

  it('sem beneficios, sem disponiveis e sem pedidos: textos vazios e a secao "Disponiveis" some', async () => {
    capturar('obterMeusBeneficios', {
      corpo: { beneficios: [], disponiveis: [], pedidos: [], dependentesCadastrados: [] },
    })
    renderizarTela('/eu/beneficios', { perfil: 'Funcionario' })
    expect(await screen.findByText('Você ainda não tem benefícios ativos.')).toBeInTheDocument()
    expect(screen.getByText('Você ainda não fez pedidos.')).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Disponíveis para você' })).toBeNull()
  })

  it('erro ao carregar mostra o aviso com "Tentar de novo"', async () => {
    capturar('obterMeusBeneficios', problema(500, { title: 'Erro interno.', status: 500 }))
    renderizarTela('/eu/beneficios', { perfil: 'Funcionario' })
    expect(
      await screen.findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
  })

  it('nenhum texto promete e-mail de resposta de beneficio (a lista de e-mails e fechada)', async () => {
    await abrir()
    await cartao('Plano de saúde')
    expect(
      within(screen.getByRole('main')).queryByText(/por e-mail|responde por e-mail/i),
    ).toBeNull()
  })

  describe('"Incluir dependente"', () => {
    async function abrirModal() {
      const tela = await abrir()
      await tela.user.click(
        within(await cartao('Plano de saúde')).getByRole('button', { name: 'Incluir dependente' }),
      )
      const modal = await screen.findByRole('dialog', { name: 'Incluir dependente' })
      return { ...tela, modal }
    }

    it('pede nome, parentesco e nascimento (nao futuro) antes de chamar a API', async () => {
      const pedir = capturar('pedirInclusaoDeDependente')
      const { user, modal } = await abrirModal()
      expect(
        within(modal).getByText(/Enquanto o RH não aprovar, o dependente não tem cobertura\./),
      ).toBeInTheDocument()
      await user.click(within(modal).getByRole('button', { name: 'Enviar pedido' }))
      expect(await within(modal).findByText('Escreva o nome do dependente.')).toBeInTheDocument()
      expect(within(modal).getByText('Escreva o parentesco.')).toBeInTheDocument()
      expect(within(modal).getByText('Escolha a data de nascimento.')).toBeInTheDocument()
      await user.type(within(modal).getByLabelText('Nome'), 'Caio Lima')
      await user.type(within(modal).getByLabelText('Parentesco'), 'FILHO')
      fireEvent.change(within(modal).getByLabelText('Data de nascimento'), {
        target: { value: '2027-01-01' },
      })
      await user.click(within(modal).getByRole('button', { name: 'Enviar pedido' }))
      expect(
        await within(modal).findByText('A data de nascimento não pode ser no futuro.'),
      ).toBeInTheDocument()
      expect(pedir.quantas()).toBe(0)
    })

    it('envia { nome, parentesco, dataNascimento } para a adesao certa e pede os beneficios de novo', async () => {
      const pedir = capturar('pedirInclusaoDeDependente')
      const meus = capturar('obterMeusBeneficios')
      const { user, modal } = await abrirModal()
      await user.type(within(modal).getByLabelText('Nome'), '  Caio Lima ')
      await user.type(within(modal).getByLabelText('Parentesco'), 'FILHO')
      fireEvent.change(within(modal).getByLabelText('Data de nascimento'), {
        target: { value: '2021-03-04' },
      })
      const antes = meus.quantas()
      await user.click(within(modal).getByRole('button', { name: 'Enviar pedido' }))
      await waitFor(() => expect(pedir.quantas()).toBe(1))
      expect(pedir.ultima().parametrosDoCaminho).toEqual({ adesaoId: '42' })
      expect(pedir.ultima().corpo).toEqual({
        nome: 'Caio Lima',
        parentesco: 'FILHO',
        dataNascimento: '2021-03-04',
      })
      pedir.conforme()
      expect(await screen.findByText('Pedido enviado. O RH vai analisar.')).toBeInTheDocument()
      await waitFor(() => expect(meus.quantas()).toBeGreaterThan(antes))
      await waitFor(() =>
        expect(screen.queryByRole('dialog', { name: 'Incluir dependente' })).toBeNull(),
      )
    })

    it.each([
      [409, 'padrao'],
      [422, 'naoAceitaDependentes'],
      [422, 'funcionarioInativo'],
    ] as const)('%i %s mostra o texto do contrato dentro da janela', async (status, nome) => {
      capturar(
        'pedirInclusaoDeDependente',
        problemaDoContrato('pedirInclusaoDeDependente', status, nome),
      )
      const { user, modal } = await abrirModal()
      await user.type(within(modal).getByLabelText('Nome'), 'Caio Lima')
      await user.type(within(modal).getByLabelText('Parentesco'), 'FILHO')
      fireEvent.change(within(modal).getByLabelText('Data de nascimento'), {
        target: { value: '2021-03-04' },
      })
      await user.click(within(modal).getByRole('button', { name: 'Enviar pedido' }))
      const detalhe = ex<{ detail: string }>('pedirInclusaoDeDependente', status, nome).detail
      expect(await within(modal).findByText(detalhe)).toBeInTheDocument()
    })
  })

  describe('"Atualizar trajeto"', () => {
    async function abrirModal() {
      const tela = await abrir()
      await tela.user.click(
        within(await cartao('Vale-transporte')).getByRole('button', { name: 'Atualizar trajeto' }),
      )
      const modal = await screen.findByRole('dialog', { name: 'Atualizar trajeto' })
      return { ...tela, modal }
    }

    it('abre com o trajeto atual, avisa que ele continua valendo e envia os tres campos para a adesao certa', async () => {
      const pedir = capturar('pedirAtualizacaoDeTrajeto')
      const { user, modal } = await abrirModal()
      expect(
        within(modal).getByText('O trajeto atual continua valendo até o RH aprovar o novo.'),
      ).toBeInTheDocument()
      expect(within(modal).getByLabelText('Linhas ou meios')).toHaveValue(
        'Ônibus 175T-10 e Metrô Linha 2',
      )
      await user.clear(within(modal).getByLabelText('Linhas ou meios'))
      await user.type(within(modal).getByLabelText('Linhas ou meios'), 'Metrô Linha 1')
      await user.click(within(modal).getByRole('button', { name: 'Enviar pedido' }))
      await waitFor(() => expect(pedir.quantas()).toBe(1))
      expect(pedir.ultima().parametrosDoCaminho).toEqual({ adesaoId: '43' })
      expect(pedir.ultima().corpo).toEqual({
        origem: 'Rua das Acácias, 120, Vila Mariana, São Paulo',
        destino: 'Av. Paulista, 1000, São Paulo',
        linhasMeios: 'Metrô Linha 1',
      })
      pedir.conforme()
      expect(await screen.findByText('Pedido enviado. O RH vai analisar.')).toBeInTheDocument()
    })

    it('campo vazio avisa e nao chama a API; 422 mostra o texto do contrato', async () => {
      const pedir = capturar('pedirAtualizacaoDeTrajeto')
      const { user, modal } = await abrirModal()
      await user.clear(within(modal).getByLabelText('Origem'))
      await user.click(within(modal).getByRole('button', { name: 'Enviar pedido' }))
      expect(await within(modal).findByText('Escreva a origem.')).toBeInTheDocument()
      expect(pedir.quantas()).toBe(0)
      capturar(
        'pedirAtualizacaoDeTrajeto',
        problemaDoContrato('pedirAtualizacaoDeTrajeto', 422, 'naoAceitaTrajeto'),
      )
      await user.type(within(modal).getByLabelText('Origem'), 'Rua Nova, 1')
      await user.click(within(modal).getByRole('button', { name: 'Enviar pedido' }))
      const detalhe = ex<{ detail: string }>(
        'pedirAtualizacaoDeTrajeto',
        422,
        'naoAceitaTrajeto',
      ).detail
      expect(await within(modal).findByText(detalhe)).toBeInTheDocument()
    })
  })
})

describe('Painel "Aderir ao benefício" (tela 45)', { timeout: 20000 }, () => {
  const abrirPainel = async (id = ODONTO.beneficio.id) => {
    const tela = renderizarTela(`/eu/beneficios/aderir/${id}`, { perfil: 'Funcionario' })
    const painel = await screen.findByRole('dialog', { name: /^Aderir ao / })
    await within(painel)
      .findByRole('button', { name: 'Enviar pedido' })
      .catch(() => undefined)
    return { ...tela, painel }
  }

  it('"Quero aderir" abre o painel com o nome e a descricao do beneficio, "Você" marcado, a data sugerida (1o do proximo mes) e o prazo de resposta', async () => {
    const tela = await abrir()
    await tela.user.click(
      within(await screen.findByRole('region', { name: 'Disponíveis para você' })).getAllByRole(
        'link',
        { name: 'Quero aderir' },
      )[0]!,
    )
    const painel = await screen.findByRole('dialog', { name: 'Aderir ao plano odontológico' })
    expect(tela.roteador.state.location.pathname).toBe(
      `/eu/beneficios/aderir/${ODONTO.beneficio.id}`,
    )
    expect(
      within(painel).getByText('Consultas e tratamentos com dentistas da rede'),
    ).toBeInTheDocument()
    expect(within(painel).getByRole('checkbox', { name: 'Você' })).toBeChecked()
    expect(within(painel).getByRole('checkbox', { name: 'Você' })).toHaveAttribute(
      'aria-disabled',
      'true',
    )
    expect(within(painel).getByLabelText('Começar em')).toHaveValue('2026-11-01')
    expect(
      within(painel).getByText(
        'O RH confere o pedido e responde em até 5 dias úteis. Você vê a resposta em Seus pedidos.',
      ),
    ).toBeInTheDocument()
    expect(within(painel).queryByText(/por e-mail/)).toBeNull()
  })

  it('envia o corpo do contrato (so os dependentes marcados e os novos), fecha e pede os beneficios de novo', async () => {
    const pedir = capturar('pedirAdesaoABeneficio')
    const meus = capturar('obterMeusBeneficios')
    const { user, painel, roteador } = await abrirPainel()
    await user.click(within(painel).getByRole('checkbox', { name: /Helena Lima/ }))
    await user.click(within(painel).getByRole('button', { name: 'Incluir outro dependente' }))
    await user.type(within(painel).getByLabelText('Nome do dependente'), 'Caio Lima')
    await user.type(within(painel).getByLabelText('Parentesco'), 'FILHO')
    fireEvent.change(within(painel).getByLabelText('Data de nascimento'), {
      target: { value: '2021-03-04' },
    })
    await user.click(
      within(painel).getByRole('checkbox', { name: 'Autorizo o desconto mensal em folha' }),
    )
    const antes = meus.quantas()
    await user.click(within(painel).getByRole('button', { name: 'Enviar pedido' }))
    await waitFor(() => expect(pedir.quantas()).toBe(1))
    expect(pedir.ultima().corpo).toEqual({
      beneficioId: ODONTO.beneficio.id,
      dataInicioDesejada: '2026-11-01',
      autorizaDescontoEmFolha: true,
      dependentes: [
        { nome: 'Helena Lima', parentesco: 'FILHA', dataNascimento: '2019-05-21' },
        { nome: 'Caio Lima', parentesco: 'FILHO', dataNascimento: '2021-03-04' },
      ],
    })
    pedir.conforme()
    expect(await screen.findByText('Pedido enviado. O RH vai analisar.')).toBeInTheDocument()
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/eu/beneficios'))
    await waitFor(() => expect(meus.quantas()).toBeGreaterThan(antes))
  })

  it('o desconto em folha nao e obrigatorio: sem marcar, vai false; sem dependentes, vai a lista vazia', async () => {
    const pedir = capturar('pedirAdesaoABeneficio')
    const { user, painel } = await abrirPainel()
    await user.click(within(painel).getByRole('button', { name: 'Enviar pedido' }))
    await waitFor(() => expect(pedir.quantas()).toBe(1))
    expect(pedir.ultima().corpo).toEqual({
      beneficioId: ODONTO.beneficio.id,
      dataInicioDesejada: '2026-11-01',
      autorizaDescontoEmFolha: false,
      dependentes: [],
    })
  })

  it('data de inicio anterior a hoje avisa e nao envia', async () => {
    const pedir = capturar('pedirAdesaoABeneficio')
    const { user, painel } = await abrirPainel()
    fireEvent.change(within(painel).getByLabelText('Começar em'), {
      target: { value: '2026-10-01' },
    })
    await user.click(within(painel).getByRole('button', { name: 'Enviar pedido' }))
    expect(
      await within(painel).findByText('A data de início não pode ser anterior a hoje.'),
    ).toBeInTheDocument()
    expect(pedir.quantas()).toBe(0)
  })

  it('dependente novo incompleto avisa; "Tirar este dependente" o remove', async () => {
    const pedir = capturar('pedirAdesaoABeneficio')
    const { user, painel } = await abrirPainel()
    await user.click(within(painel).getByRole('button', { name: 'Incluir outro dependente' }))
    await user.type(within(painel).getByLabelText('Nome do dependente'), 'Caio')
    await user.click(within(painel).getByRole('button', { name: 'Enviar pedido' }))
    expect(
      await within(painel).findByText('Preencha nome, parentesco e data de nascimento.'),
    ).toBeInTheDocument()
    expect(pedir.quantas()).toBe(0)
    await user.click(within(painel).getByRole('button', { name: 'Tirar este dependente' }))
    expect(within(painel).queryByLabelText('Nome do dependente')).toBeNull()
  })

  it('beneficio que aceita trajeto mostra origem, destino e linhas; sem dependentes o bloco "Quem vai entrar" some; o trajeto vai no corpo', async () => {
    capturar('obterMeusBeneficios', {
      corpo: {
        ...MEUS,
        disponiveis: [
          {
            beneficio: { id: 4, nome: 'Vale-transporte', descricao: 'Crédito para o deslocamento' },
            aceitaDependentes: false,
            aceitaTrajeto: true,
            ultimoPedido: null,
          },
        ],
      },
    })
    const pedir = capturar('pedirAdesaoABeneficio')
    const { user, painel } = await abrirPainel(4)
    expect(within(painel).queryByText('Quem vai entrar no plano')).toBeNull()
    await user.type(within(painel).getByLabelText('Origem'), 'Rua A, 1')
    await user.type(within(painel).getByLabelText('Destino'), 'Av. B, 2')
    await user.type(within(painel).getByLabelText('Linhas ou meios'), 'Metrô Linha 2')
    await user.click(within(painel).getByRole('button', { name: 'Enviar pedido' }))
    await waitFor(() => expect(pedir.quantas()).toBe(1))
    expect(pedir.ultima().corpo).toEqual({
      beneficioId: 4,
      dataInicioDesejada: '2026-11-01',
      autorizaDescontoEmFolha: false,
      trajeto: { origem: 'Rua A, 1', destino: 'Av. B, 2', linhasMeios: 'Metrô Linha 2' },
    })
  })

  it('trajeto pela metade avisa; em branco nao manda trajeto', async () => {
    capturar('obterMeusBeneficios', {
      corpo: {
        ...MEUS,
        disponiveis: [
          {
            beneficio: { id: 4, nome: 'Vale-transporte', descricao: null },
            aceitaDependentes: false,
            aceitaTrajeto: true,
            ultimoPedido: null,
          },
        ],
      },
    })
    const pedir = capturar('pedirAdesaoABeneficio')
    const { user, painel } = await abrirPainel(4)
    await user.type(within(painel).getByLabelText('Origem'), 'Rua A, 1')
    await user.click(within(painel).getByRole('button', { name: 'Enviar pedido' }))
    expect(
      await within(painel).findByText(
        'Preencha origem, destino e linhas ou meios, ou deixe os três em branco.',
      ),
    ).toBeInTheDocument()
    expect(pedir.quantas()).toBe(0)
    await user.clear(within(painel).getByLabelText('Origem'))
    await user.click(within(painel).getByRole('button', { name: 'Enviar pedido' }))
    await waitFor(() => expect(pedir.quantas()).toBe(1))
    expect(pedir.ultima().corpo).not.toHaveProperty('trajeto')
  })

  it.each([
    [409, 'padrao'],
    [422, 'naoOpcional'],
    [422, 'naoAceitaDependentes'],
    [422, 'funcionarioInativo'],
  ] as const)('%i %s mostra o texto do contrato dentro do painel', async (status, nome) => {
    capturar('pedirAdesaoABeneficio', problemaDoContrato('pedirAdesaoABeneficio', status, nome))
    const { user, painel } = await abrirPainel()
    await user.click(within(painel).getByRole('button', { name: 'Enviar pedido' }))
    const detalhe = ex<{ detail: string }>('pedirAdesaoABeneficio', status, nome).detail
    expect(await within(painel).findByText(detalhe)).toBeInTheDocument()
    expect(screen.getByRole('dialog', { name: /^Aderir ao / })).toBeInTheDocument()
  })

  it('beneficio que nao esta mais disponivel (ja pedido) mostra o aviso em vez do formulario', async () => {
    const { painel } = await abrirPainel(999)
    expect(
      await within(painel).findByText('Este benefício não está disponível para você.'),
    ).toBeInTheDocument()
    expect(within(painel).queryByRole('button', { name: 'Enviar pedido' })).toBeNull()
  })
})
