import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { Esquema } from '../../shared/api/tipos'
import { exemplos } from '../../shared/mocks/gerado/handlers'
import { servidor } from '../../shared/mocks/servidor'
import { capturar, problema } from '../../test/capturar'
import { renderizarTela } from '../../test/telas'

type Ficha = Esquema<'FuncionarioFichaGestor'>
const FICHA = exemplos.obterFichaDaEquipe.corpo as Ficha

async function abrir(rota = '/gestor/equipe/21') {
  const tela = renderizarTela(rota, { perfil: 'Gestor' })
  await screen.findByRole('dialog', { name: 'Ficha do colaborador' })
  return tela
}

describe('Ficha da pessoa da equipe (tela 52)', { timeout: 15000 }, () => {
  it('cabecalho: "Vendedor, setor Comercial" e a etiqueta Ativo (SEM_REGISTRO mostra Ativo), sem Editar dados, Inativar nem menu', async () => {
    await abrir()
    expect(await screen.findByText('Diego Farias', { selector: 'p' })).toBeInTheDocument()
    const janela = screen.getByRole('dialog', { name: 'Ficha do colaborador' })
    expect(within(janela).getByText('Vendedor, setor Comercial')).toBeInTheDocument()
    expect(within(janela).getByText('Ativo')).toBeInTheDocument()
    expect(within(janela).getByRole('link', { name: 'Equipe Comercial' })).toBeInTheDocument()
    expect(within(janela).queryByRole('button', { name: 'Editar dados' })).toBeNull()
    expect(within(janela).queryByRole('button', { name: /Inativar/ })).toBeNull()
    expect(within(janela).queryByRole('button', { name: /Mais ações/ })).toBeNull()
  })

  it('abas Dados, Ponto (fatia 3), Treinamentos (fatia 6) e Advertencias (fatia 4); ?aba desconhecida vale Dados; o botao Registrar advertencia (fatia 4)', async () => {
    await abrir('/gestor/equipe/21?aba=xyz')
    const abas = await screen.findByRole('tablist', { name: 'Partes da ficha' })
    expect(
      within(abas)
        .getAllByRole('tab')
        .map((t) => t.textContent),
    ).toEqual(['Dados', 'Ponto', 'Treinamentos', 'Advertências'])
    expect(within(abas).getByRole('tab', { name: 'Dados' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
    expect(screen.getByRole('button', { name: 'Registrar advertência' })).toBeInTheDocument()
  })

  it('Contato e contrato: e-mail, telefone mascarado como veio, cargo, admissao, contrato e jornada', async () => {
    await abrir()
    const cartao = (await screen.findByRole('heading', { name: 'Contato e contrato' }))
      .parentElement!
    expect(within(cartao).getByText('diego.farias@empresa.com.br')).toBeInTheDocument()
    expect(within(cartao).getByText('(11) 9****-7783')).toBeInTheDocument()
    expect(within(cartao).getByText('Vendedor')).toBeInTheDocument()
    expect(within(cartao).getByText('08/08/2023')).toBeInTheDocument()
    expect(within(cartao).getByText('CLT')).toBeInTheDocument()
    expect(within(cartao).getByText('44 h semanais')).toBeInTheDocument()
    const janela = screen.getByRole('dialog', { name: 'Ficha do colaborador' })
    for (const rotulo of ['CPF', 'RG', 'Endereço', 'Matrícula', 'Benefícios']) {
      expect(within(janela).queryByText(rotulo)).toBeNull()
    }
  })

  it('campos nulos mostram traco', async () => {
    capturar('obterFichaDaEquipe', {
      corpo: {
        ...FICHA,
        emailCorporativo: null,
        telefoneMascarado: null,
        tipoContrato: null,
        jornadaSemanalHoras: null,
      },
    })
    await abrir()
    const cartao = (await screen.findByRole('heading', { name: 'Contato e contrato' }))
      .parentElement!
    expect(within(cartao).getAllByText('—')).toHaveLength(4)
  })

  it('os quatro numeros vem de outra rota: tons certos, null mostra traco e a falha fica so no quadro', async () => {
    const { unmount } = await abrir()
    const quadro = await screen.findByRole('region', { name: 'Números da pessoa' })
    const banco = await within(quadro).findByText(/3h45/)
    expect(banco.textContent).toMatch(/^[−-]3h45$/)
    expect(banco).toHaveAttribute('data-tom', 'alerta')
    expect(within(quadro).getByText('22 dias')).toHaveAttribute('data-tom', 'neutro')
    const [treinamentos, advertencias] = within(quadro).getAllByText('2')
    expect(treinamentos).toHaveAttribute('data-tom', 'alerta')
    expect(advertencias).toHaveAttribute('data-tom', 'erro')
    expect(within(quadro).getByText('Treinamentos pendentes')).toBeInTheDocument()
    expect(within(quadro).getByText('Advertências')).toBeInTheDocument()
    unmount()

    capturar('obterIndicadoresDaEquipe', {
      corpo: {
        bancoDeHorasMinutos: null,
        feriasDisponiveisDias: null,
        treinamentosPendentes: null,
        advertenciasQuantidade: null,
      },
    })
    const b = await abrir()
    const vazio = await screen.findByRole('region', { name: 'Números da pessoa' })
    await waitFor(() => expect(within(vazio).getAllByText('—')).toHaveLength(4))
    b.unmount()

    capturar('obterIndicadoresDaEquipe', problema(500))
    await abrir()
    const falho = await screen.findByRole('region', { name: 'Números da pessoa' })
    expect(await within(falho).findByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
    expect(within(falho).getAllByText('—')).toHaveLength(4)
    expect(screen.getByText('diego.farias@empresa.com.br')).toBeInTheDocument()
  })

  it('a nota do pe continua', async () => {
    await abrir()
    expect(
      await screen.findByText(
        'Só o RH altera dados cadastrais. CPF, endereço, holerites e documentos ficam visíveis apenas para o RH.',
      ),
    ).toBeInTheDocument()
  })

  it('as requisicoes sao conformes e usam o id do caminho', async () => {
    const ficha = capturar('obterFichaDaEquipe')
    const indicadores = capturar('obterIndicadoresDaEquipe')
    await abrir()
    await waitFor(() => expect(indicadores.quantas()).toBeGreaterThan(0))
    expect(ficha.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '21' })
    expect(indicadores.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '21' })
    ficha.conforme()
    indicadores.conforme()
  })

  it('404 (fora do escopo) fecha o painel e avisa; id invalido tambem', async () => {
    capturar('obterFichaDaEquipe', problema(404, { detail: 'O registro não foi encontrado.' }))
    const { roteador, unmount } = renderizarTela('/gestor/equipe/21', { perfil: 'Gestor' })
    expect(await screen.findByText(/não foi encontrado/)).toBeInTheDocument()
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/gestor/equipe'))
    unmount()
    const invalido = renderizarTela('/gestor/equipe/abc', { perfil: 'Gestor' })
    await waitFor(() => expect(invalido.roteador.state.location.pathname).toBe('/gestor/equipe'))
  })

  it('erro com Tentar de novo e carregando com esqueleto', async () => {
    let soltar: () => void = () => {}
    const espera = new Promise<void>((r) => {
      soltar = r
    })
    servidor.use(
      http.get('*/api/gestor/equipe/:funcionarioId', async () => {
        await espera
        return HttpResponse.json(FICHA)
      }),
    )
    const a = renderizarTela('/gestor/equipe/21', { perfil: 'Gestor' })
    expect(await screen.findByRole('status', { name: 'Carregando a ficha' })).toBeInTheDocument()
    soltar()
    expect(await screen.findByText('Diego Farias', { selector: 'p' })).toBeInTheDocument()
    a.unmount()

    const ficha = capturar('obterFichaDaEquipe', problema(500))
    const { user } = await abrir()
    expect(
      await screen.findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
    const antes = ficha.quantas()
    await user.click(screen.getByRole('button', { name: 'Tentar de novo' }))
    await waitFor(() => expect(ficha.quantas()).toBe(antes + 1))
  })

  it.each(['RH', 'Funcionario'] as const)(
    '%s ve o 403 da guarda e nada e pedido',
    async (perfil) => {
      const ficha = capturar('obterFichaDaEquipe')
      renderizarTela('/gestor/equipe/21', { perfil })
      expect(
        await screen.findByText('Você não tem permissão para ver esta página.'),
      ).toBeInTheDocument()
      expect(ficha.quantas()).toBe(0)
    },
  )

  it('o gestor nunca pede rotas do RH', async () => {
    const doRh: string[] = []
    servidor.events.on('request:start', ({ request }) => {
      if (new URL(request.url).pathname.startsWith('/api/rh/')) doRh.push(request.url)
    })
    const indicadores = capturar('obterIndicadoresDaEquipe')
    await abrir()
    await waitFor(() => expect(indicadores.quantas()).toBeGreaterThan(0))
    await screen.findByText('22 dias')
    servidor.events.removeAllListeners('request:start')
    expect(doRh).toEqual([])
  })
})
