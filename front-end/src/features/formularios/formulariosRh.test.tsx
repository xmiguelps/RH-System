import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Esquema } from '../../shared/api/tipos'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { renderizarTela } from '../../test/telas'

type Formulario = Esquema<'FormularioDetalheRh'>
const FORMULARIO = exemploDoContrato<Formulario>('obterFormularioRh')
const ex = <T,>(op: string, status?: number, nome?: string) =>
  exemploDoContrato<T>(op, status, nome)
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

async function abrirLista(rota = '/rh/formularios') {
  const tela = renderizarTela(rota)
  await screen.findByRole('heading', { level: 1, name: 'Formulários', hidden: true })
  return tela
}
const linha = async (titulo: string) =>
  (
    await within(await screen.findByRole('table', { name: 'Formulários' })).findByText(titulo)
  ).closest('tr')!

describe('Formularios do RH (tela 16)', { timeout: 20000 }, () => {
  it('abas por tipo com contagemPorTipo (com Pesquisas e Sugestoes, sem Quizzes), conforme', async () => {
    const lista = capturar('listarFormulariosRh')
    await abrirLista()
    await linha('Pesquisa de clima 2026')
    for (const nome of [
      'Todos 10',
      'Entrevistas 2',
      'Admissão 1',
      'Avaliações 3',
      'Pesquisas 3',
      'Sugestões 1',
    ])
      expect(screen.getByRole('tab', { name: nome })).toBeInTheDocument()
    expect(screen.queryByRole('tab', { name: /Quiz/ })).toBeNull()
    expect(
      screen.getByText('Entrevistas, admissão, avaliações, pesquisas e sugestões'),
    ).toBeInTheDocument()
    lista.conforme()
  })

  it('a aba Sugestoes manda tipo=OUTRO e Avaliacoes manda tipo=AVALIACAO; busca e situacao, conforme', async () => {
    const lista = capturar('listarFormulariosRh')
    const { user } = await abrirLista()
    await linha('Pesquisa de clima 2026')
    await user.click(screen.getByRole('tab', { name: 'Sugestões 1' }))
    await waitFor(() => expect(lista.ultima().consulta.get('tipo')).toBe('OUTRO'))
    await user.click(screen.getByRole('tab', { name: 'Avaliações 3' }))
    await waitFor(() => expect(lista.ultima().consulta.get('tipo')).toBe('AVALIACAO'))
    await user.click(screen.getByRole('combobox', { name: 'Situação' }))
    await user.click(await screen.findByRole('option', { name: 'Encerrado' }))
    await waitFor(() => expect(lista.ultima().consulta.get('situacao')).toBe('ENCERRADO'))
    await user.type(screen.getByRole('searchbox', { name: 'Buscar formulário' }), 'clima')
    await waitFor(() => expect(lista.ultima().consulta.get('busca')).toBe('clima'))
    lista.conforme()
  })

  it('anonimo aberto nao mostra numero de respostas nem barra: so "N esperados"; anonimo encerrado mostra numeros exatos', async () => {
    await abrirLista()
    const clima = await linha('Pesquisa de clima 2026')
    expect(within(clima).getByText('101 esperados, resultado ao encerrar')).toBeInTheDocument()
    expect(within(clima).queryByRole('progressbar')).toBeNull()
    const gestor = await linha('Avaliação do gestor, ciclo 2026.2')
    expect(within(gestor).getByText('24 esperados, resultado ao encerrar')).toBeInTheDocument()
    expect(within(gestor).queryByText(/de 24/)).toBeNull()
    const encerrado = await linha('Pesquisa de clima 2025')
    expect(within(encerrado).getByText('71 de 94')).toBeInTheDocument()
    expect(within(encerrado).getByRole('progressbar')).toBeInTheDocument()
    expect(within(encerrado).getByText('Encerrou em 15/11')).toBeInTheDocument()
    expect(
      within(await linha('Avaliação da equipe, ciclo 2026.2')).getByText('17 de 24'),
    ).toBeInTheDocument()
    expect(
      within(await linha('Entrevista: vendedor')).getByText('41 respostas'),
    ).toBeInTheDocument()
    expect(within(await linha('Sugestões para o RH')).getByText('3 sugestões')).toBeInTheDocument()
    expect(within(await linha('Sugestões para o RH')).getByText('Sem prazo')).toBeInTheDocument()
  })

  it('"Encerrar" com 409 pede confirmacao com pendentes e repete com confirmar=true', async () => {
    const encerrar = capturar(
      'encerrarFormulario',
      problemaDoContrato('encerrarFormulario', 409, 'comNome'),
    )
    const { user } = await abrirLista()
    const linhaDaPesquisa = await linha('Pesquisa de segurança no trabalho')
    await user.click(within(linhaDaPesquisa).getByRole('button', { name: /Mais ações/ }))
    await user.click(await screen.findByRole('menuitem', { name: 'Encerrar' }))
    const confirmacao = await screen.findByRole('alertdialog', {
      name: 'Encerrar este formulário?',
    })
    expect(
      within(confirmacao).getByText('63 pessoas ainda não responderam. Encerrar mesmo assim?'),
    ).toBeInTheDocument()
    expect(encerrar.ultima().consulta.has('confirmar')).toBe(false)
    capturar('encerrarFormulario')
    const segundo = capturar('encerrarFormulario')
    await user.click(within(confirmacao).getByRole('button', { name: 'Encerrar' }))
    await waitFor(() => expect(segundo.quantas()).toBe(1))
    expect(segundo.ultima().consulta.get('confirmar')).toBe('true')
    segundo.conforme()
    expect(await screen.findByText('Formulário encerrado.')).toBeInTheDocument()
  })

  it('anonimo: a confirmacao vem sempre e sem numero', async () => {
    capturar('encerrarFormulario', problemaDoContrato('encerrarFormulario', 409, 'anonimo'))
    const { user } = await abrirLista()
    await user.click(
      within(await linha('Pesquisa de clima 2026')).getByRole('button', { name: /Mais ações/ }),
    )
    await user.click(await screen.findByRole('menuitem', { name: 'Encerrar' }))
    const confirmacao = await screen.findByRole('alertdialog', {
      name: 'Encerrar este formulário?',
    })
    expect(
      within(confirmacao).getByText(
        'Este formulário é anônimo: o sistema não diz quantas pessoas ainda não responderam. Encerrar mesmo assim?',
      ),
    ).toBeInTheDocument()
    expect(confirmacao.textContent).not.toMatch(/\d/)
  })

  it('"Excluir" pergunta antes; com 409 pede de novo e repete com confirmar=true', async () => {
    const excluir = capturar(
      'excluirFormulario',
      problemaDoContrato('excluirFormulario', 409, 'comNome'),
    )
    const { user } = await abrirLista()
    await user.click(
      within(await linha('Admissão: outubro de 2026')).getByRole('button', { name: /Mais ações/ }),
    )
    await user.click(await screen.findByRole('menuitem', { name: 'Excluir' }))
    const primeira = await screen.findByRole('alertdialog', { name: 'Excluir este formulário?' })
    expect(excluir.quantas()).toBe(0)
    await user.click(within(primeira).getByRole('button', { name: 'Excluir' }))
    await waitFor(() => expect(excluir.quantas()).toBe(1))
    expect(
      await screen.findByText(
        '63 pessoas ainda não responderam. Excluir apaga as respostas. Excluir mesmo assim?',
      ),
    ).toBeInTheDocument()
    const segundo = capturar('excluirFormulario', { status: 204 })
    await user.click(
      within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Excluir' }),
    )
    await waitFor(() => expect(segundo.quantas()).toBe(1))
    expect(segundo.ultima().consulta.get('confirmar')).toBe('true')
    segundo.conforme()
  })

  it('"Ver resultado" fica desligado no anonimo aberto; "Editar" fica desligado no encerrado', async () => {
    const { user } = await abrirLista()
    await user.click(
      within(await linha('Pesquisa de clima 2026')).getByRole('button', { name: /Mais ações/ }),
    )
    expect(await screen.findByRole('menuitem', { name: 'Ver resultado' })).toHaveAttribute(
      'aria-disabled',
      'true',
    )
    const encerrado = await linha('Entrevista: vendedor')
    expect(
      within(encerrado).queryByRole('link', { name: 'Editar Entrevista: vendedor' }),
    ).toBeNull()
    expect(within(encerrado).getByText('Editar')).toHaveAttribute('aria-disabled', 'true')
  })
})

async function abrirAssistente(rota: string) {
  const tela = renderizarTela(rota)
  const janela = await screen.findByRole('dialog', { name: /formulário$/ })
  return { ...tela, janela }
}

describe('Assistente (telas 17 a 20)', { timeout: 20000 }, () => {
  it('etapa Detalhes: seis tipos (com Pesquisa e Sugestao, sem Quiz); Sugestao esconde "Respostas anonimas"', async () => {
    const { user, janela } = await abrirAssistente('/rh/formularios/novo')
    for (const t of [
      'Entrevista automática',
      'Formulário de admissão',
      'Avaliação da equipe',
      'Avaliação do gestor',
      'Pesquisa',
      'Sugestão',
    ])
      expect(within(janela).getByRole('radio', { name: new RegExp(t) })).toBeInTheDocument()
    expect(within(janela).queryByText(/Quiz/)).toBeNull()
    expect(within(janela).getByRole('checkbox', { name: /Respostas anônimas/ })).toBeInTheDocument()
    await user.click(within(janela).getByRole('radio', { name: /Sugestão/ }))
    expect(within(janela).queryByRole('checkbox', { name: /Respostas anônimas/ })).toBeNull()
  })

  it('"Proximo" cria o rascunho com criarFormulario (avaliacao com sentidoAvaliacao) e vai para Perguntas', async () => {
    const criar = capturar('criarFormulario')
    const { user, janela, roteador } = await abrirAssistente('/rh/formularios/novo')
    await user.type(
      within(janela).getByRole('textbox', { name: 'Título' }),
      'Avaliação do gestor 2027',
    )
    await user.click(within(janela).getByRole('radio', { name: /Avaliação do gestor/ }))
    await user.click(within(janela).getByRole('checkbox', { name: /Respostas anônimas/ }))
    await user.click(within(janela).getByRole('button', { name: 'Próximo' }))
    await waitFor(() => expect(criar.quantas()).toBe(1))
    expect(criar.ultima().corpo).toEqual({
      titulo: 'Avaliação do gestor 2027',
      descricao: null,
      tipo: 'AVALIACAO',
      sentidoAvaliacao: 'EQUIPE_AVALIA_GESTOR',
      anonimo: true,
    })
    criar.conforme()
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/rh/formularios/13/editar'))
    expect(roteador.state.location.search).toContain('etapa=perguntas')
  })

  it('400 do servidor aparece no campo titulo', async () => {
    capturar(
      'criarFormulario',
      problema(400, { title: 'Dados inválidos.', errors: { Titulo: ['O título é obrigatório.'] } }),
    )
    const { user, janela } = await abrirAssistente('/rh/formularios/novo')
    await user.type(within(janela).getByRole('textbox', { name: 'Título' }), 'x')
    await user.click(within(janela).getByRole('button', { name: 'Próximo' }))
    expect(await within(janela).findByText('O título é obrigatório.')).toBeInTheDocument()
  })

  it('editar: as saidas seguintes chamam editarFormulario', async () => {
    const editar = capturar('editarFormulario')
    const { user, janela } = await abrirAssistente('/rh/formularios/12/editar')
    const titulo = await within(janela).findByRole('textbox', { name: 'Título' })
    expect(titulo).toHaveValue('Entrevista: auxiliar de logística')
    await user.click(within(janela).getByRole('button', { name: 'Salvar rascunho e sair' }))
    await waitFor(() => expect(editar.quantas()).toBe(1))
    expect(editar.ultima().parametrosDoCaminho).toEqual({ formularioId: '12' })
    editar.conforme()
  })

  it('anonimo publicado abre travado (tipo e anonimato) e as perguntas so leitura', async () => {
    capturar('obterFormularioRh', {
      corpo: {
        ...FORMULARIO,
        anonimo: true,
        situacao: 'PUBLICADO',
        temRespostas: null,
        perguntas: FORMULARIO.perguntas.slice(0, 3),
      } satisfies Formulario,
    })
    const { janela } = await abrirAssistente('/rh/formularios/12/editar')
    await within(janela).findByRole('textbox', { name: 'Título' })
    expect(within(janela).getByRole('radio', { name: /Pesquisa/ })).toHaveAttribute(
      'aria-disabled',
      'true',
    )
    expect(within(janela).getByRole('checkbox', { name: /Respostas anônimas/ })).toHaveAttribute(
      'aria-disabled',
      'true',
    )
    expect(
      within(janela).getByText(
        'Depois de publicado, o formulário não muda entre anônimo e identificado.',
      ),
    ).toBeInTheDocument()
  })

  it('anonimo publicado: as perguntas abrem so para leitura e sem "Envio de arquivo"', async () => {
    capturar('obterFormularioRh', {
      corpo: {
        ...FORMULARIO,
        anonimo: true,
        situacao: 'PUBLICADO',
        temRespostas: null,
        perguntas: FORMULARIO.perguntas.slice(0, 3),
      } satisfies Formulario,
    })
    const { janela } = await abrirAssistente('/rh/formularios/12/editar?etapa=perguntas')
    expect(
      await within(janela).findByText('As perguntas de um formulário anônimo publicado não mudam.'),
    ).toBeInTheDocument()
    expect(within(janela).queryByRole('button', { name: /Editar a pergunta/ })).toBeNull()
    expect(within(janela).queryByRole('button', { name: 'Envio de arquivo' })).toBeNull()
  })

  it('anonimo em rascunho: "Envio de arquivo" desligado com a dica', async () => {
    capturar('obterFormularioRh', {
      corpo: {
        ...FORMULARIO,
        anonimo: true,
        perguntas: FORMULARIO.perguntas.slice(0, 3),
      } satisfies Formulario,
    })
    const { janela } = await abrirAssistente('/rh/formularios/12/editar?etapa=perguntas')
    expect(await within(janela).findByRole('button', { name: 'Envio de arquivo' })).toBeDisabled()
    expect(
      within(janela).getByText('Formulário anônimo não tem envio de arquivo.'),
    ).toBeInTheDocument()
  })

  it('etapa Perguntas: sem Quiz; adicionar multipla escolha envia { texto, tipo, obrigatoria, alternativas }; anonimo desliga "Envio de arquivo"', async () => {
    const adicionar = capturar('adicionarPerguntaFormulario')
    const { user, janela } = await abrirAssistente('/rh/formularios/12/editar?etapa=perguntas')
    await within(janela).findByRole('list', { name: 'Perguntas do formulário' })
    expect(within(janela).queryByText(/Quiz|Eliminatória|Nota dada pelo RH|Correta:/)).toBeNull()
    await user.click(within(janela).getByRole('button', { name: 'Múltipla escolha' }))
    const editor = within(janela).getByRole('region', { name: 'Nova pergunta' })
    await user.type(
      within(editor).getByRole('textbox', { name: 'Texto da pergunta' }),
      'Turno preferido?',
    )
    await user.type(within(editor).getByRole('textbox', { name: 'Opção 1' }), 'Manhã')
    await user.type(within(editor).getByRole('textbox', { name: 'Opção 2' }), 'Tarde')
    await user.click(within(editor).getByRole('button', { name: 'Salvar pergunta' }))
    await waitFor(() => expect(adicionar.quantas()).toBe(1))
    expect(adicionar.ultima().corpo).toEqual({
      texto: 'Turno preferido?',
      tipo: 'MULTIPLA_ESCOLHA',
      obrigatoria: true,
      alternativas: [{ texto: 'Manhã' }, { texto: 'Tarde' }],
    })
    adicionar.conforme()
  })

  it('editar pergunta mantem os ids das alternativas; descer manda ordem; apagar chama excluirPerguntaFormulario', async () => {
    const editar = capturar('editarPerguntaFormulario')
    const excluir = capturar('excluirPerguntaFormulario', { status: 204 })
    const { user, janela } = await abrirAssistente('/rh/formularios/12/editar?etapa=perguntas')
    await within(janela).findByRole('list', { name: 'Perguntas do formulário' })
    await user.click(within(janela).getByRole('button', { name: 'Descer a pergunta 1' }))
    await waitFor(() => expect(editar.quantas()).toBe(1))
    expect(editar.ultima().parametrosDoCaminho).toEqual({ formularioId: '12', perguntaId: '501' })
    expect(editar.ultima().corpo).toMatchObject({
      ordem: 2,
      alternativas: [
        { id: 901, texto: 'Sim' },
        { id: 902, texto: 'Não' },
      ],
    })
    editar.conforme()
    await user.click(within(janela).getByRole('button', { name: 'Editar a pergunta 2' }))
    await user.click(within(janela).getByRole('button', { name: 'Apagar pergunta' }))
    await waitFor(() => expect(excluir.quantas()).toBe(1))
    expect(excluir.ultima().parametrosDoCaminho).toEqual({ formularioId: '12', perguntaId: '502' })
    excluir.conforme()
  })

  it('etapa Verificacao: tres situacoes; com BLOQUEIO o "Proximo" fica desligado', async () => {
    const verificar = capturar('verificarFormulario', {
      corpo: ex('verificarFormulario', 200, 'comAvisoEBloqueio'),
    })
    const { janela } = await abrirAssistente('/rh/formularios/12/editar?etapa=verificacao')
    const lista = await within(janela).findByRole('list', { name: 'Conferência' })
    const itens = within(lista).getAllByRole('listitem')
    expect(itens.map((i) => i.getAttribute('data-situacao'))).toEqual([
      'AVISO',
      'OK',
      'BLOQUEIO',
      'AVISO',
      'BLOQUEIO',
      'AVISO',
    ])
    expect(within(janela).getByRole('button', { name: 'Próximo' })).toBeDisabled()
    expect(within(janela).queryByText(/quiz|nota/i)).toBeNull()
    verificar.conforme()
  })

  it('etapa Publicacao: sem as tres caixas cortadas; lembrete ligado por padrao e escondido no link; publicar mostra o link do token', async () => {
    const publicar = capturar('publicarFormulario', {
      corpo: ex('publicarFormulario', 200, 'porLinkAgora'),
    })
    const { user, janela } = await abrirAssistente('/rh/formularios/12/editar?etapa=publicacao')
    await within(janela).findByRole('radio', { name: /Candidatos, por link/ })
    for (const cortado of [/Nota mínima/, /Enviar o resultado por e-mail/, /Convidar aprovados/])
      expect(within(janela).queryByText(cortado)).toBeNull()
    expect(
      within(janela).queryByRole('checkbox', { name: /Lembrar quem não respondeu/ }),
    ).toBeNull()
    await user.click(within(janela).getByRole('radio', { name: /Pessoas escolhidas/ }))
    expect(
      within(janela).getByRole('checkbox', {
        name: /Lembrar quem não respondeu 7 dias antes do prazo/,
      }),
    ).toHaveAttribute('aria-checked', 'true')
    await user.click(within(janela).getByRole('radio', { name: /Candidatos, por link/ }))
    await user.click(within(janela).getByRole('button', { name: 'Publicar formulário' }))
    await waitFor(() => expect(publicar.quantas()).toBe(1))
    expect(publicar.ultima().corpo).toEqual({
      alvo: 'LINK',
      setorIds: [],
      funcionarioIds: [],
      agendarPara: null,
      prazo: '2026-10-31',
      recorrencia: null,
      lembreteAutomatico: false,
    })
    publicar.conforme()
    expect(
      await within(janela).findByText(/\/formulario\/k3Qz9vT1mP8xLw2RbN7cYdEa5HuJ0sFgXo4iBtVq6Zc$/),
    ).toBeInTheDocument()
    expect(within(janela).getByRole('button', { name: 'Copiar link' })).toBeInTheDocument()
  })

  it('"Candidatos, por link" fica desligado fora da entrevista; 422 da publicacao mostra o detail', async () => {
    capturar('obterFormularioRh', {
      corpo: {
        ...FORMULARIO,
        tipo: 'PESQUISA',
        publicacao: { ...FORMULARIO.publicacao, alvo: 'SETORES' },
      } satisfies Formulario,
    })
    capturar(
      'publicarFormulario',
      problemaDoContrato('publicarFormulario', 422, 'anonimoPublicoPequeno'),
    )
    const { user, janela } = await abrirAssistente('/rh/formularios/12/editar?etapa=publicacao')
    expect(
      await within(janela).findByRole('radio', { name: /Candidatos, por link/ }),
    ).toHaveAttribute('aria-disabled', 'true')
    await user.click(within(janela).getByRole('button', { name: 'Publicar formulário' }))
    const detail = ex<{ detail: string }>('publicarFormulario', 422, 'anonimoPublicoPequeno').detail
    expect(await within(janela).findByText(detail)).toBeInTheDocument()
  })

  it('"Salvar rascunho e sair" com data de agendamento avisa que ela so e guardada ao publicar', async () => {
    const salvar = capturar('salvarPublicacaoFormulario')
    const { user, janela } = await abrirAssistente('/rh/formularios/12/editar?etapa=publicacao')
    await user.click(await within(janela).findByRole('radio', { name: 'Agendar para uma data' }))
    fireEvent.change(within(janela).getByLabelText('Publicar no dia'), {
      target: { value: '2026-10-20' },
    })
    await user.click(within(janela).getByRole('button', { name: 'Salvar rascunho e sair' }))
    await waitFor(() => expect(salvar.quantas()).toBe(1))
    expect(salvar.ultima().corpo).toMatchObject({ agendarPara: '2026-10-20' })
    salvar.conforme()
    expect(
      await screen.findByText(/A data de agendamento só é guardada ao publicar/),
    ).toBeInTheDocument()
  })
})

async function abrirRespondentes(id = 11) {
  const tela = renderizarTela(`/rh/formularios/${id}/respondentes`)
  await screen.findByRole('heading', { level: 1, hidden: true })
  return tela
}

describe('Quem respondeu (tela 21), respostas e resultado', { timeout: 20000 }, () => {
  it('com nome: cartoes, abas, busca, setor e "Ver respostas"/"Lembrar"; parametros conforme', async () => {
    capturar('obterFormularioRh', {
      corpo: {
        ...FORMULARIO,
        id: 11,
        tipo: 'ADMISSAO',
        situacao: 'PUBLICADO',
        publicacao: { ...FORMULARIO.publicacao, alvo: 'PESSOAS' },
      } satisfies Formulario,
    })
    const respondentes = capturar('listarRespondentesFormulario', {
      corpo: ex('listarRespondentesFormulario', 200, 'identificado'),
    })
    const lembrar = capturar('lembrarQuemFaltaFormulario', {
      corpo: ex('lembrarQuemFaltaFormulario', 200, 'identificado'),
    })
    const { user } = await abrirRespondentes()
    const cartoes = await screen.findByRole('region', { name: 'Participação' })
    expect(await within(cartoes).findByText('0%')).toBeInTheDocument()
    await user.click(screen.getByRole('tab', { name: 'Faltam' }))
    await waitFor(() =>
      expect(respondentes.ultima().consulta.get('situacao')).toBe('NAO_RESPONDEU'),
    )
    await user.type(screen.getByRole('searchbox', { name: 'Buscar pessoa' }), 'Rafa')
    await waitFor(() => expect(respondentes.ultima().consulta.get('busca')).toBe('Rafa'))
    respondentes.conforme()
    await user.click(await screen.findByRole('button', { name: 'Lembrar Rafael Torres' }))
    await waitFor(() => expect(lembrar.quantas()).toBe(1))
    expect(lembrar.ultima().corpo).toEqual({ funcionarioIds: [104] })
    lembrar.conforme()
    expect(await screen.findByText('Lembrete enviado a 1 pessoa.')).toBeInTheDocument()
  })

  it('anonimo: so os cartoes, sem abas, busca, tabela nem "Ver respostas"; contagens "disponivel ao encerrar"; lembrete sem numero', async () => {
    capturar('obterFormularioRh', {
      corpo: {
        ...FORMULARIO,
        id: 8,
        tipo: 'PESQUISA',
        anonimo: true,
        situacao: 'PUBLICADO',
        temRespostas: null,
        publicacao: { ...FORMULARIO.publicacao, alvo: 'SETORES' },
      } satisfies Formulario,
    })
    capturar('listarRespondentesFormulario', {
      corpo: ex('listarRespondentesFormulario', 200, 'anonimoAberto'),
    })
    const lembrar = capturar('lembrarQuemFaltaFormulario', {
      corpo: ex('lembrarQuemFaltaFormulario', 200, 'anonimo'),
    })
    const { user } = await abrirRespondentes(8)
    const cartoes = await screen.findByRole('region', { name: 'Participação' })
    expect(await within(cartoes).findByText('101')).toBeInTheDocument()
    expect(within(cartoes).getAllByText('disponível ao encerrar')).toHaveLength(3)
    expect(screen.queryByRole('tablist', { name: 'Situação de quem responde' })).toBeNull()
    expect(screen.queryByRole('searchbox')).toBeNull()
    expect(screen.queryByRole('table')).toBeNull()
    expect(screen.getByRole('button', { name: 'Baixar respostas' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Ver resultado' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Lembrar quem falta por e-mail' }))
    await waitFor(() => expect(lembrar.quantas()).toBe(1))
    expect(lembrar.ultima().corpo).toEqual({ funcionarioIds: [] })
    const aviso = await screen.findByText('Lembrete enviado.')
    expect(aviso.textContent).not.toMatch(/\d/)
  })

  it('Sugestao: um cartao, data e hora por linha, e "Ver respostas" manda o respondidoEmUtc da linha', async () => {
    capturar('obterFormularioRh', {
      corpo: {
        ...FORMULARIO,
        id: 7,
        tipo: 'OUTRO',
        situacao: 'PUBLICADO',
        publicacao: { ...FORMULARIO.publicacao, alvo: 'SETORES', prazo: null },
      } satisfies Formulario,
    })
    capturar('listarRespondentesFormulario', {
      corpo: ex('listarRespondentesFormulario', 200, 'sugestao'),
    })
    const respostas = capturar('obterRespostasDeFuncionario')
    const { user } = await abrirRespondentes(7)
    const cartoes = await screen.findByRole('region', { name: 'Participação' })
    expect(within(cartoes).getByText('Sugestões recebidas')).toBeInTheDocument()
    const tabela = await screen.findByRole('table', { name: 'Quem respondeu' })
    expect(within(tabela).getByText('06/10/2026 11:20')).toBeInTheDocument()
    expect(screen.queryByRole('tab', { name: 'Faltam' })).toBeNull()
    await user.click(
      within(tabela).getAllByRole('button', { name: 'Ver respostas de Bruno Lima' })[1]!,
    )
    await waitFor(() => expect(respostas.quantas()).toBe(1))
    expect(respostas.ultima().consulta.get('respondidoEmUtc')).toBe('2026-10-02T11:05:09Z')
    expect(respostas.ultima().consulta.has('avaliadoId')).toBe(false)
    respostas.conforme()
    expect(await screen.findByRole('dialog', { name: /^Respostas de / })).toBeInTheDocument()
  })

  it('formulario por link: lista de candidatos, curriculo e respostas do candidato', async () => {
    const candidatos = capturar('listarCandidatosFormulario')
    const curriculo = capturar('baixarCurriculoDeCandidato')
    const respostas = capturar('obterRespostasDeCandidato')
    const arquivo = capturar('baixarArquivoDeResposta')
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    const { user } = await abrirRespondentes(12)
    const tabela = await screen.findByRole('table', { name: 'Candidatos' })
    candidatos.conforme()
    const botaoDoCurriculo = (
      await within(tabela).findAllByRole('button', { name: /^Baixar currículo de / })
    )[0]!
    await user.click(botaoDoCurriculo)
    await waitFor(() => expect(curriculo.quantas()).toBe(1))
    curriculo.conforme()
    await user.click(within(tabela).getAllByRole('button', { name: /^Ver respostas de / })[0]!)
    const modal = await screen.findByRole('dialog', { name: /^Respostas de / })
    await waitFor(() => expect(respostas.quantas()).toBe(1))
    respostas.conforme()
    const baixar = within(modal)
      .queryAllByRole('button', { name: /^Baixar / })
      .find((b) => b.textContent === 'Baixar')
    if (baixar) {
      await user.click(baixar)
      await waitFor(() => expect(arquivo.quantas()).toBe(1))
    }
    expect(screen.queryByRole('button', { name: 'Lembrar quem falta por e-mail' })).toBeNull()
  })

  it('"Baixar respostas" pede a exportacao (CSV) e "Ver respostas" de pessoa abre o modal', async () => {
    capturar('obterFormularioRh', {
      corpo: {
        ...FORMULARIO,
        id: 11,
        tipo: 'ADMISSAO',
        situacao: 'PUBLICADO',
        publicacao: { ...FORMULARIO.publicacao, alvo: 'PESSOAS' },
      } satisfies Formulario,
    })
    const exportar = capturar('exportarRespostasFormulario')
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    const criar = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:teste')
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})
    const { user } = await abrirRespondentes()
    await user.click(await screen.findByRole('button', { name: 'Baixar respostas' }))
    await waitFor(() => expect(exportar.quantas()).toBe(1))
    exportar.conforme()
    await waitFor(() => expect(criar).toHaveBeenCalled())
  })

  it('resultado: anonimo encerrado mostra secoes na ordem, textos na ordem recebida e as perguntas ocultas', async () => {
    capturar('obterFormularioRh', {
      corpo: {
        ...FORMULARIO,
        id: 4,
        tipo: 'PESQUISA',
        anonimo: true,
        situacao: 'ENCERRADO',
      } satisfies Formulario,
    })
    const anonimo = ex<Esquema<'FormularioResultado'>>(
      'obterResultadoFormulario',
      200,
      'anonimoEncerrado',
    )
    const resultado = capturar('obterResultadoFormulario', { corpo: anonimo })
    renderizarTela('/rh/formularios/4/resultado')
    const secoes = await screen.findAllByRole('region', { name: /^Pergunta / })
    expect(secoes).toHaveLength(anonimo.perguntas.length)
    const comTexto = anonimo.perguntas.find((p) => p.textos.length > 1)
    if (comTexto) {
      const itens = within(
        screen.getByRole('region', { name: `Pergunta ${comTexto.ordem}` }),
      ).getAllByRole('listitem')
      expect(itens.map((i) => i.textContent)).toEqual(comTexto.textos)
    }
    if (anonimo.perguntasOcultas > 0)
      expect(screen.getByText(/porque (teve|tiveram) menos de \d+ respostas\./)).toBeInTheDocument()
    expect(resultado.ultima().consulta.has('avaliadoId')).toBe(false)
    resultado.conforme()
  })

  it('resultado: 409 mostra o texto sem numero; avaliacao pede avaliadoId e 422 mostra "menos de 3"', async () => {
    capturar('obterFormularioRh', {
      corpo: {
        ...FORMULARIO,
        id: 8,
        tipo: 'PESQUISA',
        anonimo: true,
        situacao: 'PUBLICADO',
      } satisfies Formulario,
    })
    capturar('obterResultadoFormulario', problemaDoContrato('obterResultadoFormulario', 409))
    const { unmount } = renderizarTela('/rh/formularios/8/resultado')
    const texto = await screen.findByText(
      'O resultado de um formulário anônimo só aparece depois que ele é encerrado.',
    )
    expect(texto.textContent).not.toMatch(/\d/)
    unmount()

    capturar('obterFormularioRh', {
      corpo: {
        ...FORMULARIO,
        id: 9,
        tipo: 'AVALIACAO',
        sentidoAvaliacao: 'GESTOR_AVALIA_EQUIPE',
        situacao: 'ENCERRADO',
      } satisfies Formulario,
    })
    const avaliados = capturar('listarAvaliadosFormulario')
    const resultado = capturar(
      'obterResultadoFormulario',
      problemaDoContrato('obterResultadoFormulario', 422, 'avaliadoAbaixoDoMinimo'),
    )
    const { user } = renderizarTela('/rh/formularios/9/resultado')
    expect(
      await screen.findByText('Escolha quem foi avaliado para ver o resultado.'),
    ).toBeInTheDocument()
    avaliados.conforme()
    const lista = ex<Esquema<'FormularioAvaliadosLista'>>('listarAvaliadosFormulario')
    const liberado = lista.itens.find((a) => a.resultadoLiberado)!
    await user.click(
      await screen.findByRole('button', { name: new RegExp(liberado.funcionario.nome) }),
    )
    await waitFor(() =>
      expect(resultado.ultima().consulta.get('avaliadoId')).toBe(String(liberado.funcionario.id)),
    )
    resultado.conforme()
    const detail = ex<{ detail: string }>(
      'obterResultadoFormulario',
      422,
      'avaliadoAbaixoDoMinimo',
    ).detail
    expect(await screen.findByText(detail)).toBeInTheDocument()
    expect(detail).toMatch(/menos de 3/)
    // Quem ficou abaixo do mínimo aparece sem número de respostas.
    expect(screen.getByRole('button', { name: /Paulo Vieira/ })).toHaveTextContent(
      'menos de 3 respostas',
    )
    expect(screen.getByRole('button', { name: /Paulo Vieira/ })).toBeDisabled()
  })
})
