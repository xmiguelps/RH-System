import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { escolherOpcao } from '../../test/formulario'
import { renderizarTela } from '../../test/telas'

const problemaDoContrato = (op: string, status: number, nome?: string) =>
  problema(status, exemploDoContrato<Record<string, unknown>>(op, status, nome))

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
})

afterEach(() => {
  vi.useRealTimers()
})

async function abrirMinhas(perfil: 'Funcionario' | 'Gestor' | 'RH' = 'Funcionario') {
  const tela = renderizarTela('/eu/advertencias', { perfil })
  await screen.findByRole('heading', { level: 1, name: 'Minhas advertências' })
  const cartao = await screen.findByRole('region', {
    name: 'Advertência escrita de 05/10/2026',
  })
  return { ...tela, cartao }
}

describe('Minhas advertencias (tela 43)', { timeout: 15000 }, () => {
  it.each(['Funcionario', 'Gestor', 'RH'] as const)(
    '%s: o cartao em destaque so para a PENDENTE e o historico com as demais',
    async (perfil) => {
      const lista = capturar('listarMinhasAdvertencias')
      const { cartao } = await abrirMinhas(perfil)
      expect(within(cartao).getByText('Aguardando sua ciência')).toBeInTheDocument()
      expect(within(cartao).getByText('Carlos Ribeiro, seu gestor')).toBeInTheDocument()
      expect(within(cartao).getByText('Atrasos repetidos sem justificativa')).toBeInTheDocument()
      expect(screen.getAllByRole('region', { name: /^Advertência / })).toHaveLength(1)
      const historico = screen.getByRole('table', { name: 'Histórico' })
      expect(within(historico).getByText('11/07/2026')).toBeInTheDocument()
      expect(within(historico).getByText('Ciente')).toBeInTheDocument()
      expect(screen.getByText('Dúvidas sobre uma advertência? Fale com o RH')).toBeInTheDocument()
      lista.conforme()
    },
  )

  it('"Confirmar ciencia" chama confirmarCienciaDeAdvertencia e recarrega a lista', async () => {
    const lista = capturar('listarMinhasAdvertencias')
    const ciencia = capturar('confirmarCienciaDeAdvertencia')
    const { user, cartao } = await abrirMinhas()
    const antes = lista.quantas()
    await user.click(within(cartao).getByRole('button', { name: 'Confirmar ciência' }))
    expect(await screen.findByText('Ciência registrada.')).toBeInTheDocument()
    expect(ciencia.ultima().parametrosDoCaminho).toEqual({ advertenciaId: '118' })
    ciencia.conforme()
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(antes))
  })

  it('422 ADVERTENCIA_NAO_ESTA_PENDENTE mostra a mensagem e recarrega a lista', async () => {
    const lista = capturar('listarMinhasAdvertencias')
    capturar(
      'confirmarCienciaDeAdvertencia',
      problemaDoContrato('confirmarCienciaDeAdvertencia', 422),
    )
    const { user, cartao } = await abrirMinhas()
    const antes = lista.quantas()
    await user.click(within(cartao).getByRole('button', { name: 'Confirmar ciência' }))
    expect(
      await screen.findByText('Esta advertência já teve ciência ou contestação.'),
    ).toBeInTheDocument()
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(antes))
  })

  it('"Contestar" pede de 10 a 2000 caracteres e chama contestarAdvertencia com { texto }', async () => {
    const contestar = capturar('contestarAdvertencia')
    const { user, cartao } = await abrirMinhas()
    await user.click(within(cartao).getByRole('button', { name: 'Contestar' }))
    const modal = await screen.findByRole('dialog', { name: 'Contestar advertência' })
    const campo = within(modal).getByRole('textbox', { name: 'O que você quer explicar' })
    await user.type(campo, 'Curto')
    await user.click(within(modal).getByRole('button', { name: 'Enviar contestação' }))
    expect(await within(modal).findByText('Escreva pelo menos 10 caracteres.')).toBeInTheDocument()
    expect(contestar.quantas()).toBe(0)
    expect(campo).toHaveAttribute('maxlength', '2000')
    await user.type(campo, ' demais: eu avisei o gestor por mensagem.')
    await user.click(within(modal).getByRole('button', { name: 'Enviar contestação' }))
    expect(
      await screen.findByText('Contestação enviada. O RH vai analisar e responder por e-mail.'),
    ).toBeInTheDocument()
    expect(contestar.ultima().corpo).toEqual({
      texto: 'Curto demais: eu avisei o gestor por mensagem.',
    })
    expect(contestar.ultima().parametrosDoCaminho).toEqual({ advertenciaId: '118' })
    contestar.conforme()
  })

  it('"Ver detalhes" mostra o que o item traz, sem outra chamada', async () => {
    const { user } = await abrirMinhas()
    await user.click(
      screen.getByRole('button', { name: 'Ver detalhes da advertência de 11/07/2026' }),
    )
    const modal = await screen.findByRole('dialog', { name: 'Advertência de 11/07/2026' })
    expect(within(modal).getByText('Atraso na abertura da loja')).toBeInTheDocument()
    expect(within(modal).getByText('Ciência em 11/07/2026, 16:20')).toBeInTheDocument()
  })
})

async function abrirAdvertenciasDoRh(rota = '/rh/advertencias') {
  const tela = renderizarTela(rota)
  await screen.findByRole('heading', { level: 1, name: 'Advertências', hidden: true })
  return tela
}

describe('Advertencias do RH (tela 14) e o detalhe', { timeout: 15000 }, () => {
  it('numeros, abas com contagens e "RH" em "Aplicada por" quando o RH registrou', async () => {
    const resumo = capturar('obterResumoDasAdvertencias')
    const lista = capturar('listarAdvertencias')
    await abrirAdvertenciasDoRh()
    const numeros = await screen.findByRole('region', { name: 'Números das advertências' })
    expect(await within(numeros).findByText('Registradas em 2026')).toBeInTheDocument()
    expect(within(numeros).getByText('9')).toBeInTheDocument()
    expect(within(numeros).getByText('Logística')).toBeInTheDocument()
    expect(within(numeros).getByText('4 advertências')).toBeInTheDocument()
    for (const nome of ['Todas 9', 'Aguardando ciência 2', 'Ciente 6', 'Contestada 1'])
      expect(screen.getByRole('tab', { name: nome })).toBeInTheDocument()
    const tabela = screen.getByRole('table', { name: 'Advertências' })
    const vanessa = (await within(tabela).findByText('Vanessa Lima')).closest('tr')!
    expect(within(vanessa).getByText('RH')).toBeInTheDocument()
    expect(within(vanessa).getByText('Contestada')).toBeInTheDocument()
    expect(screen.getByText('Mostrando 3 de 9 em 2026')).toBeInTheDocument()
    resumo.conforme()
    lista.conforme()
  })

  it('aba, busca, setor e tipo enviam os parametros certos, conforme', async () => {
    const lista = capturar('listarAdvertencias')
    const { user } = await abrirAdvertenciasDoRh()
    await screen.findByText('Vanessa Lima')
    await user.click(screen.getByRole('tab', { name: 'Aguardando ciência 2' }))
    await waitFor(() => expect(lista.ultima().consulta.get('situacao')).toBe('PENDENTE'))
    await escolherOpcao(user, 'Setor', 'Comercial')
    await waitFor(() => expect(lista.ultima().consulta.get('setorId')).toBe('1'))
    await escolherOpcao(user, 'Tipo', 'Escrita')
    await waitFor(() => expect(lista.ultima().consulta.get('tipo')).toBe('ESCRITA'))
    await user.type(
      screen.getByRole('searchbox', { name: 'Buscar colaborador ou motivo' }),
      'Diego',
    )
    await waitFor(() => expect(lista.ultima().consulta.get('busca')).toBe('Diego'))
    expect(lista.ultima().consulta.get('situacao')).toBe('PENDENTE')
    lista.conforme()
  })

  it('"Abrir" leva ao detalhe com a contestacao; "Responder contestacao" envia { decisao, resposta }', async () => {
    const detalhe = capturar('obterAdvertencia')
    const responder = capturar('responderContestacaoDeAdvertencia')
    const { user, roteador } = await abrirAdvertenciasDoRh()
    await user.click(
      await screen.findByRole('button', {
        name: 'Abrir a advertência de Vanessa Lima de 15/09/2026',
      }),
    )
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/rh/advertencias/109'))
    const painel = await screen.findByRole('dialog', { name: 'Advertência de 15/09/2026' })
    const contestacao = await within(painel).findByRole('region', { name: 'Contestação' })
    expect(within(contestacao).getByText(/Troquei o plantão de 12\/09/)).toBeInTheDocument()
    detalhe.conforme()
    const resposta = within(painel).getByRole('region', { name: 'Responder contestação' })
    await user.click(within(resposta).getByRole('button', { name: 'Enviar resposta' }))
    expect(await within(resposta).findByText('Escolha manter ou acatar.')).toBeInTheDocument()
    await user.click(within(resposta).getByRole('radio', { name: /Acatar a contestação/ }))
    await user.type(
      within(resposta).getByRole('textbox', { name: 'Resposta para Vanessa' }),
      'Conferimos as mensagens.',
    )
    expect(
      within(resposta).getByText(
        'A resposta vai por e-mail para Vanessa. Ela não fica guardada no sistema e a pessoa não a vê na tela, só a nova situação.',
      ),
    ).toBeInTheDocument()
    await user.click(within(resposta).getByRole('button', { name: 'Enviar resposta' }))
    await waitFor(() => expect(responder.quantas()).toBe(1))
    expect(responder.ultima().corpo).toEqual({
      decisao: 'ACATAR',
      resposta: 'Conferimos as mensagens.',
    })
    responder.conforme()
  })

  it('422 ADVERTENCIA_NAO_ESTA_CONTESTADA mostra o detail', async () => {
    capturar(
      'responderContestacaoDeAdvertencia',
      problemaDoContrato('responderContestacaoDeAdvertencia', 422),
    )
    const { user } = await abrirAdvertenciasDoRh('/rh/advertencias/109')
    const painel = await screen.findByRole('dialog', { name: 'Advertência de 15/09/2026' })
    const resposta = await within(painel).findByRole('region', { name: 'Responder contestação' })
    await user.click(within(resposta).getByRole('radio', { name: /Manter a advertência/ }))
    await user.type(within(resposta).getByRole('textbox', { name: 'Resposta para Vanessa' }), 'Ok')
    await user.click(within(resposta).getByRole('button', { name: 'Enviar resposta' }))
    expect(
      await within(resposta).findByText('Só uma advertência contestada pode receber resposta.'),
    ).toBeInTheDocument()
  })
})

async function abrirNovaDoRh() {
  const tela = await abrirAdvertenciasDoRh('/rh/advertencias/nova')
  const painel = await screen.findByRole('dialog', { name: 'Nova advertência' })
  return { ...tela, painel }
}

async function preencherMotivoEDescricao(
  user: ReturnType<typeof renderizarTela>['user'],
  painel: HTMLElement,
) {
  await user.type(
    within(painel).getByRole('textbox', { name: 'Motivo' }),
    'Atrasos repetidos sem justificativa',
  )
  await user.type(
    within(painel).getByRole('textbox', { name: 'Descrição' }),
    'Cinco atrasos na abertura da loja.',
  )
}

describe('Registrar advertencia do RH (telas 15 e 11)', { timeout: 20000 }, () => {
  it('envia aplicadaPor GESTOR_DO_COLABORADOR por padrao e atualiza a lista e os numeros', async () => {
    const registrar = capturar('registrarAdvertencia')
    const lista = capturar('listarAdvertencias')
    const resumo = capturar('obterResumoDasAdvertencias')
    const { user, painel } = await abrirNovaDoRh()
    await user.type(within(painel).getByRole('combobox', { name: 'Colaborador' }), 'Bru')
    await user.click(await screen.findByRole('option', { name: /Bruno Lima/ }))
    await preencherMotivoEDescricao(user, painel)
    const antes = { lista: lista.quantas(), resumo: resumo.quantas() }
    await user.click(screen.getByRole('button', { name: 'Registrar advertência' }))
    await waitFor(() => expect(registrar.quantas()).toBe(1))
    expect(registrar.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '12' })
    expect(registrar.ultima().corpo).toEqual({
      dataDoOcorrido: '2026-10-07',
      tipo: 'VERBAL',
      aplicadaPor: 'GESTOR_DO_COLABORADOR',
      motivo: 'Atrasos repetidos sem justificativa',
      descricao: 'Cinco atrasos na abertura da loja.',
    })
    registrar.conforme()
    expect(
      await screen.findByText('Advertência registrada. Diego Farias foi avisado por e-mail.'),
    ).toBeInTheDocument()
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(antes.lista))
    await waitFor(() => expect(resumo.quantas()).toBeGreaterThan(antes.resumo))
  })

  it('"Aplicada por: RH" envia aplicadaPor RH; data no futuro nao envia', async () => {
    const registrar = capturar('registrarAdvertencia')
    const { user, painel } = await abrirNovaDoRh()
    await user.type(within(painel).getByRole('combobox', { name: 'Colaborador' }), 'Bru')
    await user.click(await screen.findByRole('option', { name: /Bruno Lima/ }))
    await escolherOpcao(user, 'Aplicada por', 'RH')
    await escolherOpcao(user, 'Tipo', 'Escrita')
    await preencherMotivoEDescricao(user, painel)
    fireEvent.change(within(painel).getByLabelText('Data do ocorrido'), {
      target: { value: '2026-10-08' },
    })
    await user.click(screen.getByRole('button', { name: 'Registrar advertência' }))
    expect(
      await within(painel).findByText('A data do ocorrido não pode ser no futuro.'),
    ).toBeInTheDocument()
    expect(registrar.quantas()).toBe(0)
    fireEvent.change(within(painel).getByLabelText('Data do ocorrido'), {
      target: { value: '2026-10-05' },
    })
    await user.click(screen.getByRole('button', { name: 'Registrar advertência' }))
    await waitFor(() => expect(registrar.quantas()).toBe(1))
    expect(registrar.ultima().corpo).toMatchObject({
      aplicadaPor: 'RH',
      tipo: 'ESCRITA',
      dataDoOcorrido: '2026-10-05',
    })
  })

  it.each([
    ['semGestor', 'Esta pessoa não tem gestor ativo. Escolha "RH" em "Aplicada por".'],
    ['inativo', 'Não é possível registrar advertência para uma pessoa inativa.'],
  ])('422 %s mostra o detail', async (nome, texto) => {
    capturar('registrarAdvertencia', problemaDoContrato('registrarAdvertencia', 422, nome))
    const { user, painel } = await abrirNovaDoRh()
    await user.type(within(painel).getByRole('combobox', { name: 'Colaborador' }), 'Bru')
    await user.click(await screen.findByRole('option', { name: /Bruno Lima/ }))
    await preencherMotivoEDescricao(user, painel)
    await user.click(screen.getByRole('button', { name: 'Registrar advertência' }))
    expect(await within(painel).findByText(texto)).toBeInTheDocument()
  })

  it('422 ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO mostra o detail', async () => {
    capturar(
      'registrarAdvertencia',
      problema(422, {
        title: 'Operação recusada.',
        detail: 'O gestor não pode advertir a si mesmo.',
        codigo: 'ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO',
      }),
    )
    const { user, painel } = await abrirNovaDoRh()
    await user.type(within(painel).getByRole('combobox', { name: 'Colaborador' }), 'Bru')
    await user.click(await screen.findByRole('option', { name: /Bruno Lima/ }))
    await preencherMotivoEDescricao(user, painel)
    await user.click(screen.getByRole('button', { name: 'Registrar advertência' }))
    expect(
      await within(painel).findByText('O gestor não pode advertir a si mesmo.'),
    ).toBeInTheDocument()
  })

  it('aba da ficha (11): a lista da pessoa, "Abrir" sem caminho e o painel com o colaborador travado', async () => {
    const lista = capturar('listarAdvertenciasDoFuncionario')
    const registrar = capturar('registrarAdvertencia')
    const detalhe = capturar('obterAdvertencia')
    const { user, roteador } = renderizarTela('/rh/colaboradores/12?aba=advertencias')
    const tabela = await screen.findByRole('table', { name: 'Advertências' })
    expect(
      await within(tabela).findByText('Faltou à escala de sábado sem avisar'),
    ).toBeInTheDocument()
    expect(within(tabela).getByText('Carlos Ribeiro')).toBeInTheDocument()
    expect(lista.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '12' })
    lista.conforme()
    await user.click(
      within(tabela).getByRole('button', {
        name: 'Abrir a advertência de Pedro Santos de 02/06/2026',
      }),
    )
    expect(
      await screen.findByRole('dialog', { name: 'Advertência de 15/09/2026' }),
    ).toBeInTheDocument()
    expect(detalhe.ultima().parametrosDoCaminho).toEqual({ advertenciaId: '96' })
    expect(roteador.state.location.pathname).toBe('/rh/colaboradores/12')
    await user.click(screen.getAllByRole('button', { name: 'Fechar' }).at(-1)!)
    await user.click(screen.getByRole('button', { name: 'Registrar advertência' }))
    const painel = await screen.findByRole('dialog', { name: 'Nova advertência' })
    expect(within(painel).getByDisplayValue('Bruno Lima')).toBeDisabled()
    await preencherMotivoEDescricao(user, painel)
    await user.click(within(painel).getByRole('button', { name: 'Registrar advertência' }))
    await waitFor(() => expect(registrar.quantas()).toBe(1))
    expect(registrar.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '12' })
  })
})

async function abrirAdvertenciasDaEquipe(rota = '/gestor/advertencias') {
  const tela = renderizarTela(rota, { perfil: 'Gestor' })
  await screen.findByRole('heading', { level: 1, name: 'Advertências do setor', hidden: true })
  return tela
}

describe(
  'Advertencias do gestor (telas 59, 53 e 60) e a aba da ficha da equipe',
  { timeout: 20000 },
  () => {
    it('tres numeros, "Por pessoa" na ordem da API e sem a coluna "Aplicada por"', async () => {
      const resumo = capturar('obterResumoDasAdvertenciasDaEquipe')
      const lista = capturar('listarAdvertenciasDaEquipe')
      await abrirAdvertenciasDaEquipe()
      expect(
        await screen.findByText(
          'Setor Comercial. O RH acompanha todas as advertências registradas.',
        ),
      ).toBeInTheDocument()
      const numeros = await screen.findByRole('region', {
        name: 'Números das advertências do setor',
      })
      expect(await within(numeros).findByText('Registradas em 2026')).toBeInTheDocument()
      expect(within(numeros).getByText('3')).toBeInTheDocument()
      expect(within(numeros).getByText('Diego Farias')).toBeInTheDocument()
      expect(within(numeros).getByText('2 de 8')).toBeInTheDocument()
      const porPessoa = screen.getByRole('list', { name: 'Por pessoa' })
      expect(
        within(porPessoa)
          .getAllByRole('listitem')
          .map((li) => li.textContent),
      ).toEqual([
        'DFDiego Farias2',
        'PSPedro Santos1',
        'FLFernanda Lopes0',
        'GRGustavo Reis0',
        'HPHelena Prates0',
        'IBIgor Batista0',
        'JNJúlia Nunes0',
        'LMLarissa Moura0',
      ])
      const tabela = screen.getByRole('table', { name: 'Registros' })
      await within(tabela).findByText('Atraso na abertura da loja')
      expect(within(tabela).queryByRole('columnheader', { name: 'Aplicada por' })).toBeNull()
      resumo.conforme()
      lista.conforme()
    })

    it('o detalhe nao mostra o texto da contestacao; 404 de outro setor mostra "nao encontrado"', async () => {
      const detalhe = capturar('obterAdvertenciaDaEquipe')
      const { user } = await abrirAdvertenciasDaEquipe()
      await user.click(
        await screen.findByRole('button', {
          name: 'Abrir a advertência de Diego Farias de 05/10/2026',
        }),
      )
      const painel = await screen.findByRole('dialog', { name: 'Advertência de 05/10/2026' })
      expect(await within(painel).findByText('Ainda sem ciência')).toBeInTheDocument()
      expect(within(painel).queryByText('Contestação')).toBeNull()
      detalhe.conforme()
    })

    it('404 (pessoa de outro setor) mostra o erro no painel', async () => {
      capturar('obterAdvertenciaDaEquipe', problemaDoContrato('obterAdvertenciaDaEquipe', 404))
      await abrirAdvertenciasDaEquipe('/gestor/advertencias/999')
      const painel = await screen.findByRole('dialog', { name: 'Advertência' })
      expect(await within(painel).findByText('O registro não foi encontrado.')).toBeInTheDocument()
    })

    it('tela 60: escolhe a pessoa da equipe e nao envia aplicadaPor', async () => {
      const registrar = capturar('registrarAdvertenciaDaEquipe')
      const { user } = await abrirAdvertenciasDaEquipe('/gestor/advertencias/nova')
      const painel = await screen.findByRole('dialog', { name: 'Registrar advertência' })
      expect(within(painel).queryByRole('combobox', { name: 'Aplicada por' })).toBeNull()
      await escolherOpcao(user, 'Colaborador', 'Diego Farias')
      await preencherMotivoEDescricao(user, painel)
      await user.click(within(painel).getByRole('button', { name: 'Registrar advertência' }))
      await waitFor(() => expect(registrar.quantas()).toBe(1))
      expect(registrar.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '21' })
      expect(registrar.ultima().corpo).toEqual({
        dataDoOcorrido: '2026-10-07',
        tipo: 'VERBAL',
        motivo: 'Atrasos repetidos sem justificativa',
        descricao: 'Cinco atrasos na abertura da loja.',
      })
      registrar.conforme()
      expect(
        await screen.findByText(
          'Advertência registrada. Diego Farias foi avisado por e-mail e o RH recebeu uma cópia.',
        ),
      ).toBeInTheDocument()
    })

    it('422 FUNCIONARIO_INATIVO no gestor mostra o detail', async () => {
      capturar(
        'registrarAdvertenciaDaEquipe',
        problemaDoContrato('registrarAdvertenciaDaEquipe', 422),
      )
      const { user } = await abrirAdvertenciasDaEquipe('/gestor/advertencias/nova')
      const painel = await screen.findByRole('dialog', { name: 'Registrar advertência' })
      await escolherOpcao(user, 'Colaborador', 'Diego Farias')
      await preencherMotivoEDescricao(user, painel)
      await user.click(within(painel).getByRole('button', { name: 'Registrar advertência' }))
      expect(
        await within(painel).findByText(
          'Não é possível registrar advertência para uma pessoa inativa.',
        ),
      ).toBeInTheDocument()
    })

    it('tela 53 e a aba da ficha da equipe: a lista da pessoa e o registro com o colaborador travado', async () => {
      const lista = capturar('listarAdvertenciasDoFuncionarioDaEquipe')
      const registrar = capturar('registrarAdvertenciaDaEquipe')
      const { user } = renderizarTela('/gestor/equipe/21?aba=advertencias', { perfil: 'Gestor' })
      await screen.findByText('Diego Farias', { selector: 'p' })
      const tabela = await screen.findByRole('table', { name: 'Advertências' })
      expect(await within(tabela).findByText('Atraso na abertura da loja')).toBeInTheDocument()
      expect(lista.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '21' })
      lista.conforme()
      await user.click(screen.getByRole('button', { name: 'Registrar advertência' }))
      const painel = await screen.findByRole('dialog', { name: 'Registrar advertência' })
      expect(within(painel).getByDisplayValue('Diego Farias')).toBeDisabled()
      expect(
        within(painel).getByText(
          'A advertência fica registrada como aplicada por você. O RH recebe uma cópia e o colaborador confirma a ciência pelo portal, podendo contestar.',
        ),
      ).toBeInTheDocument()
      await preencherMotivoEDescricao(user, painel)
      await user.click(within(painel).getByRole('button', { name: 'Registrar advertência' }))
      await waitFor(() => expect(registrar.quantas()).toBe(1))
      expect(registrar.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '21' })
    })
  },
)
