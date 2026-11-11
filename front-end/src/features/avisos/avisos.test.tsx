import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Esquema } from '../../shared/api/tipos'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { renderizarTela } from '../../test/telas'

type AvisoRh = Esquema<'AvisoDetalheRh'>
type AvisoEu = Esquema<'AvisoDetalheEu'>
const DETALHE_RH = exemploDoContrato<AvisoRh>('obterAviso')
const DETALHE_EU = exemploDoContrato<AvisoEu>('obterMeuAviso')
const problemaDoContrato = (op: string, status: number, nome?: string) =>
  problema(status, exemploDoContrato<Record<string, unknown>>(op, status, nome))

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
})

afterEach(() => {
  vi.useRealTimers()
})

async function abrirMeusAvisos(
  perfil: 'Funcionario' | 'Gestor' | 'RH' = 'Funcionario',
  rota = '/eu/avisos',
) {
  const tela = renderizarTela(rota, { perfil })
  // com o modal aberto (deep link), a página atrás fica inerte
  await screen.findByRole('heading', { level: 1, name: 'Avisos', hidden: true })
  return tela
}

describe('Avisos da pessoa (telas 37, 38 e 50)', { timeout: 15000 }, () => {
  it.each(['Funcionario', 'Gestor'] as const)(
    '%s: a lista mostra os 4 avisos, "Nao lidos 2" e "Todos 4", com "Todos" escolhida ao abrir',
    async (perfil) => {
      const lista = capturar('listarMeusAvisos')
      await abrirMeusAvisos(perfil)
      const itens = within(
        await screen.findByRole('list', { name: 'Lista de avisos' }),
      ).getAllByRole('listitem')
      expect(itens).toHaveLength(4)
      expect(screen.getByRole('tab', { name: 'Não lidos 2' })).toHaveAttribute(
        'aria-selected',
        'false',
      )
      expect(screen.getByRole('tab', { name: 'Todos 4' })).toHaveAttribute('aria-selected', 'true')
      expect(lista.ultima().consulta.has('apenasNaoLidos')).toBe(false)
      lista.conforme()
      expect(screen.getAllByText('Não lido')).toHaveLength(2)
      expect(screen.getByText('Comunicados do RH para você')).toBeInTheDocument()
    },
  )

  it('a aba "Nao lidos" envia apenasNaoLidos=true e a busca envia busca, conforme', async () => {
    const lista = capturar('listarMeusAvisos')
    const { user } = await abrirMeusAvisos()
    await screen.findByRole('list', { name: 'Lista de avisos' })
    await user.click(screen.getByRole('tab', { name: 'Não lidos 2' }))
    await waitFor(() => expect(lista.ultima().consulta.get('apenasNaoLidos')).toBe('true'))
    lista.conforme()
    await user.type(screen.getByRole('searchbox', { name: 'Buscar aviso' }), 'cantina')
    await waitFor(() => expect(lista.ultima().consulta.get('busca')).toBe('cantina'))
    expect(lista.ultima().consulta.get('apenasNaoLidos')).toBe('true')
    lista.conforme()
  })

  it('abrir o aviso 409 chama obterMeuAviso e marcarAvisoComoLido e depois refaz a lista e as bolinhas', async () => {
    const lista = capturar('listarMeusAvisos')
    const detalhe = capturar('obterMeuAviso')
    const marcar = capturar('marcarAvisoComoLido')
    const contadores = capturar('obterContadoresDoMenuDoFuncionario')
    const { user } = await abrirMeusAvisos()
    await screen.findByRole('list', { name: 'Lista de avisos' })
    const listasAntes = lista.quantas()
    const contadoresAntes = contadores.quantas()
    await user.click(
      screen.getByRole('link', { name: 'Abrir o aviso Política de trabalho remoto atualizada' }),
    )
    const janela = await screen.findByRole('dialog', {
      name: 'Política de trabalho remoto atualizada',
    })
    await waitFor(() => expect(marcar.quantas()).toBe(1))
    expect(marcar.ultima().parametrosDoCaminho).toEqual({ avisoId: '409' })
    expect(detalhe.ultima().parametrosDoCaminho).toEqual({ avisoId: '409' })
    marcar.conforme()
    detalhe.conforme()
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(listasAntes))
    await waitFor(() => expect(contadores.quantas()).toBeGreaterThan(contadoresAntes))
    expect(
      await within(janela).findByRole('heading', { level: 3, name: 'O que mudou' }),
    ).toBeInTheDocument()
    expect(
      within(janela).getByText('Publicado por Ana Souza em 06/10/2026, 09:00.'),
    ).toBeInTheDocument()
    expect(
      within(janela).getByText('Ao abrir, o aviso fica marcado como lido para o RH.'),
    ).toBeInTheDocument()
    await user.click(within(janela).getByText('Fechar', { selector: 'button' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(marcar.quantas()).toBe(1)
  })

  it('o aviso simples aparece como texto puro: <script> e <img onerror> nunca viram HTML', async () => {
    const perigoso = '<script>alert(1)</script> e <img src=x onerror=alert(2)>'
    capturar('obterMeuAviso', {
      corpo: { ...DETALHE_EU, tipo: 'SIMPLES', conteudo: perigoso } satisfies AvisoEu,
    })
    await abrirMeusAvisos('Funcionario', '/eu/avisos/409')
    const janela = await screen.findByRole('dialog', {
      name: 'Política de trabalho remoto atualizada',
    })
    expect(await within(janela).findByText(perigoso)).toBeInTheDocument()
    expect(janela.querySelector('script')).toBeNull()
    expect(janela.querySelector('img')).toBeNull()
  })

  it('o aviso detalhado com <script> passa pelo Markdown e continua texto', async () => {
    capturar('obterMeuAviso', {
      corpo: {
        ...DETALHE_EU,
        conteudo: '## Seção\n<script>alert(1)</script><img src=x onerror=alert(2)>',
      } satisfies AvisoEu,
    })
    await abrirMeusAvisos('Funcionario', '/eu/avisos/409')
    const janela = await screen.findByRole('dialog', {
      name: 'Política de trabalho remoto atualizada',
    })
    await within(janela).findByRole('heading', { level: 3, name: 'Seção' })
    expect(janela.querySelector('script')).toBeNull()
    expect(janela.querySelector('img')).toBeNull()
  })

  it('404 (aviso que nao esta mais vigente) avisa e volta para a lista', async () => {
    capturar('obterMeuAviso', problemaDoContrato('obterMeuAviso', 404))
    capturar('marcarAvisoComoLido', problemaDoContrato('marcarAvisoComoLido', 404))
    const { roteador } = await abrirMeusAvisos('Funcionario', '/eu/avisos/409')
    expect(await screen.findByText('Este aviso não está mais disponível.')).toBeInTheDocument()
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/eu/avisos'))
  })

  it('se marcarAvisoComoLido falha, a leitura segue; ao fechar tenta de novo', async () => {
    const marcar = capturar('marcarAvisoComoLido', problemaDoContrato('marcarAvisoComoLido', 500))
    const { user } = await abrirMeusAvisos('Funcionario', '/eu/avisos/409')
    const janela = await screen.findByRole('dialog', {
      name: 'Política de trabalho remoto atualizada',
    })
    await within(janela).findByRole('heading', { level: 3, name: 'O que mudou' })
    await waitFor(() => expect(marcar.quantas()).toBe(1))
    await user.click(within(janela).getByText('Fechar', { selector: 'button' }))
    await waitFor(() => expect(marcar.quantas()).toBe(2))
    expect(
      await screen.findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
  })
})

async function abrirAvisosDoRh(rota = '/rh/avisos') {
  const tela = renderizarTela(rota)
  await screen.findByRole('heading', { level: 1, name: 'Avisos' })
  return tela
}

const detalhe = () => screen.getByRole('region', { name: 'Detalhe do aviso' })

describe('Avisos do RH (tela 27)', { timeout: 15000 }, () => {
  it('os quatro numeros, as abas com contagens e o primeiro aviso escolhido com "41 de 101 viram"', async () => {
    const resumo = capturar('obterResumoDosAvisos')
    const lista = capturar('listarAvisos')
    const aberto = capturar('obterAviso')
    await abrirAvisosDoRh()
    const numeros = await screen.findByRole('region', { name: 'Números dos avisos' })
    expect(await within(numeros).findByText('74%')).toBeInTheDocument()
    expect(
      within(numeros).getByText('Expediente no feriado de 12/10, em 12/10'),
    ).toBeInTheDocument()
    for (const nome of ['Ativos 4', 'Agendados 1', 'Expirados 12', 'Rascunhos 1'])
      expect(screen.getByRole('tab', { name: nome })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'Ativos 4' })).toHaveAttribute('aria-selected', 'true')
    const primeiro = await screen.findByRole('button', { name: /Cantina com novo horário/ })
    expect(primeiro).toHaveAttribute('aria-pressed', 'true')
    expect(within(primeiro).getByText('41 de 101 viram')).toBeInTheDocument()
    expect(
      within(primeiro).getByText('Publicado em 07/10, 08:00, expira em 20/10'),
    ).toBeInTheDocument()
    await waitFor(() => expect(aberto.ultima().parametrosDoCaminho).toEqual({ avisoId: '412' }))
    expect(lista.ultima().consulta.get('situacao')).toBe('ATIVO')
    lista.conforme()
    resumo.conforme()
    aberto.conforme()
    expect(screen.getByText('Comunicados para a empresa toda')).toBeInTheDocument()
  })

  it('aba, tipo e busca vao para listarAvisos, conforme', async () => {
    const lista = capturar('listarAvisos')
    const { user } = await abrirAvisosDoRh()
    await screen.findByRole('button', { name: /Cantina com novo horário/ })
    await user.click(screen.getByRole('tab', { name: 'Expirados 12' }))
    await waitFor(() => expect(lista.ultima().consulta.get('situacao')).toBe('EXPIRADO'))
    await user.click(screen.getByRole('combobox', { name: 'Tipo' }))
    await user.click(await screen.findByRole('option', { name: 'Detalhado' }))
    await waitFor(() => expect(lista.ultima().consulta.get('tipo')).toBe('DETALHADO'))
    await user.type(screen.getByRole('searchbox', { name: 'Buscar aviso' }), 'vacina')
    await waitFor(() => expect(lista.ultima().consulta.get('busca')).toBe('vacina'))
    expect(lista.ultima().consulta.get('situacao')).toBe('EXPIRADO')
    lista.conforme()
  })

  it('"Quem viu" pede NAO_LIDO com 5 por pagina e mostra os totais e o resto', async () => {
    const leituras = capturar('listarLeiturasDoAviso')
    await abrirAvisosDoRh()
    const painel = await screen.findByRole('region', { name: 'Detalhe do aviso' })
    expect(await within(painel).findByText('73')).toBeInTheDocument()
    expect(within(painel).getByText('28')).toBeInTheDocument()
    expect(within(painel).getByText('Diego Farias')).toBeInTheDocument()
    expect(within(painel).getByText('E mais 26 pessoas.')).toBeInTheDocument()
    expect(leituras.ultima().consulta.get('situacao')).toBe('NAO_LIDO')
    expect(leituras.ultima().consulta.get('tamanhoPagina')).toBe('5')
    leituras.conforme()
  })

  it('"Lembrar quem nao viu" chama lembrarQuemNaoViuAviso e mostra a quantidade devolvida', async () => {
    const lembrar = capturar('lembrarQuemNaoViuAviso')
    const { user } = await abrirAvisosDoRh()
    await user.click(await within(detalhe()).findByRole('button', { name: 'Lembrar quem não viu' }))
    expect(await screen.findByText('Lembrete na fila para 28 pessoas.')).toBeInTheDocument()
    expect(lembrar.ultima().parametrosDoCaminho).toEqual({ avisoId: '412' })
    lembrar.conforme()
  })

  it('lembrete com 0 e-mails diz "Todos ja viram este aviso."', async () => {
    capturar('lembrarQuemNaoViuAviso', { status: 202, corpo: { quantidadeDeEmails: 0 } })
    const { user } = await abrirAvisosDoRh()
    await user.click(await within(detalhe()).findByRole('button', { name: 'Lembrar quem não viu' }))
    expect(await screen.findByText('Todos já viram este aviso.')).toBeInTheDocument()
  })

  it('"Encerrar agora" confirma e chama encerrarAviso; 422 AVISO_NAO_ESTA_ATIVO mostra o detail', async () => {
    const encerrar = capturar('encerrarAviso')
    const { user } = await abrirAvisosDoRh()
    await user.click(await within(detalhe()).findByRole('button', { name: 'Encerrar agora' }))
    const confirmacao = await screen.findByRole('alertdialog', {
      name: 'Encerrar este aviso agora?',
    })
    await user.click(within(confirmacao).getByRole('button', { name: 'Encerrar agora' }))
    expect(await screen.findByText('Aviso encerrado.')).toBeInTheDocument()
    expect(encerrar.ultima().parametrosDoCaminho).toEqual({ avisoId: '412' })
    encerrar.conforme()

    capturar('encerrarAviso', problemaDoContrato('encerrarAviso', 422))
    await user.click(await within(detalhe()).findByRole('button', { name: 'Encerrar agora' }))
    const outra = await screen.findByRole('alertdialog', { name: 'Encerrar este aviso agora?' })
    await user.click(within(outra).getByRole('button', { name: 'Encerrar agora' }))
    expect(
      await within(outra).findByText('Só um aviso ativo pode ser encerrado.'),
    ).toBeInTheDocument()
  })

  it('rascunho e agendado mostram "Excluir" (excluirAviso) e nunca "Encerrar agora"', async () => {
    capturar('obterAviso', {
      corpo: { ...DETALHE_RH, situacao: 'RASCUNHO', leitura: null } satisfies AvisoRh,
    })
    const excluir = capturar('excluirAviso', { status: 204 })
    const { user } = await abrirAvisosDoRh('/rh/avisos?situacao=RASCUNHO')
    const botao = await within(detalhe()).findByRole('button', { name: 'Excluir' })
    expect(within(detalhe()).getByRole('button', { name: 'Publicar' })).toBeInTheDocument()
    expect(within(detalhe()).queryByRole('button', { name: 'Encerrar agora' })).toBeNull()
    expect(within(detalhe()).getByText('Este aviso ainda não foi publicado.')).toBeInTheDocument()
    await user.click(botao)
    const confirmacao = await screen.findByRole('alertdialog', { name: 'Excluir este aviso?' })
    await user.click(within(confirmacao).getByRole('button', { name: 'Excluir' }))
    expect(await screen.findByText('Aviso excluído.')).toBeInTheDocument()
    excluir.conforme()
  })

  it('agendado: "Excluir", sem "Encerrar agora", e "Sera publicado em ..."', async () => {
    capturar('obterAviso', {
      corpo: {
        ...DETALHE_RH,
        situacao: 'AGENDADO',
        leitura: null,
        publicacaoEmUtc: '2026-10-09T11:00:00Z',
      } satisfies AvisoRh,
    })
    await abrirAvisosDoRh('/rh/avisos?situacao=AGENDADO')
    expect(await within(detalhe()).findByRole('button', { name: 'Excluir' })).toBeInTheDocument()
    expect(within(detalhe()).queryByRole('button', { name: 'Encerrar agora' })).toBeNull()
    expect(within(detalhe()).getByText('Será publicado em 09/10, 08:00.')).toBeInTheDocument()
  })

  it('"Publicar" o rascunho chama publicarAviso e mostra a data do agendamento', async () => {
    capturar('obterAviso', {
      corpo: { ...DETALHE_RH, situacao: 'RASCUNHO', leitura: null } satisfies AvisoRh,
    })
    const publicar = capturar('publicarAviso')
    const { user } = await abrirAvisosDoRh('/rh/avisos?situacao=RASCUNHO')
    await user.click(await within(detalhe()).findByRole('button', { name: 'Publicar' }))
    expect(await screen.findByText('Aviso agendado para 09/10, 08:00.')).toBeInTheDocument()
    publicar.conforme()
  })

  it('excluir aviso ja publicado: 422 AVISO_NAO_PODE_SER_EXCLUIDO mostra o detail', async () => {
    capturar('obterAviso', {
      corpo: { ...DETALHE_RH, situacao: 'AGENDADO', leitura: null } satisfies AvisoRh,
    })
    capturar('excluirAviso', problemaDoContrato('excluirAviso', 422))
    const { user } = await abrirAvisosDoRh('/rh/avisos?situacao=AGENDADO')
    await user.click(await within(detalhe()).findByRole('button', { name: 'Excluir' }))
    const confirmacao = await screen.findByRole('alertdialog', { name: 'Excluir este aviso?' })
    await user.click(within(confirmacao).getByRole('button', { name: 'Excluir' }))
    expect(
      await within(confirmacao).findByText(
        'Só é possível excluir um aviso que ainda não foi publicado. Para tirar um aviso do ar, encerre-o.',
      ),
    ).toBeInTheDocument()
  })
})

const MUITO_LONGO = 'a'.repeat(281)

async function abrirNovoAviso() {
  const tela = renderizarTela('/rh/avisos/novo')
  const painel = await screen.findByRole('dialog', { name: 'Novo aviso' })
  return { ...tela, painel }
}

describe('Novo aviso (tela 28)', { timeout: 15000 }, () => {
  it('simples com mais de 280 caracteres nao envia', async () => {
    const criar = capturar('criarAviso')
    const { user, painel } = await abrirNovoAviso()
    await user.type(within(painel).getByRole('textbox', { name: 'Título' }), 'Aviso')
    fireEvent.change(within(painel).getByRole('textbox', { name: 'Mensagem' }), {
      target: { value: MUITO_LONGO },
    })
    expect(within(painel).getByText('281/280')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Publicar aviso' }))
    expect(
      await within(painel).findByText('O aviso simples tem até 280 caracteres.'),
    ).toBeInTheDocument()
    expect(criar.quantas()).toBe(0)
  })

  it('"Salvar rascunho" envia publicar: false; "Publicar aviso" envia publicar: true, conforme', async () => {
    const criar = capturar('criarAviso')
    const { user, painel } = await abrirNovoAviso()
    await user.type(
      within(painel).getByRole('textbox', { name: 'Título' }),
      'Inventário de estoque no sábado, 17/10',
    )
    await user.type(
      within(painel).getByRole('textbox', { name: 'Mensagem' }),
      'O inventário começa às 07:00.',
    )
    fireEvent.change(within(painel).getByLabelText('Expira em'), {
      target: { value: '2026-10-18' },
    })
    await user.click(screen.getByRole('button', { name: 'Salvar rascunho' }))
    await waitFor(() => expect(criar.quantas()).toBe(1))
    expect(criar.ultima().corpo).toEqual({
      tipo: 'SIMPLES',
      titulo: 'Inventário de estoque no sábado, 17/10',
      conteudo: 'O inventário começa às 07:00.',
      publicarEmUtc: null,
      expiraEm: '2026-10-18',
      fixado: false,
      publicar: false,
    })
    criar.conforme()
  })

  it('"Publicar aviso" com data e hora converte para UTC pelo -03:00 e manda publicar: true', async () => {
    const criar = capturar('criarAviso')
    const { user, painel, roteador } = await abrirNovoAviso()
    await user.click(within(painel).getByRole('radio', { name: /Detalhado/ }))
    await user.type(within(painel).getByRole('textbox', { name: 'Título' }), 'Vacinação')
    fireEvent.change(within(painel).getByRole('textbox', { name: 'Conteúdo' }), {
      target: { value: '## Quando\nDias 13 e 14/10.' },
    })
    fireEvent.change(within(painel).getByLabelText('Publicar em'), {
      target: { value: '2026-10-09' },
    })
    fireEvent.change(within(painel).getByLabelText('Hora da publicação'), {
      target: { value: '09:00' },
    })
    await user.click(within(painel).getByRole('checkbox', { name: /Fixar no topo/ }))
    await user.click(screen.getByRole('button', { name: 'Publicar aviso' }))
    await waitFor(() => expect(criar.quantas()).toBe(1))
    expect(criar.ultima().corpo).toMatchObject({
      tipo: 'DETALHADO',
      publicarEmUtc: '2026-10-09T12:00:00Z',
      expiraEm: null,
      fixado: true,
      publicar: true,
    })
    criar.conforme()
    expect(await screen.findByText('Aviso publicado.')).toBeInTheDocument()
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/rh/avisos'))
    expect(roteador.state.location.search).toBe('?aviso=413')
  })

  it('400 do servidor aparece no campo conteudo', async () => {
    capturar(
      'criarAviso',
      problema(400, {
        title: 'Dados inválidos.',
        detail: 'Confira os campos.',
        errors: { conteudo: ['O aviso simples tem até 280 caracteres.'] },
      }),
    )
    const { user, painel } = await abrirNovoAviso()
    await user.type(within(painel).getByRole('textbox', { name: 'Título' }), 'Aviso')
    await user.type(within(painel).getByRole('textbox', { name: 'Mensagem' }), 'Texto')
    await user.click(screen.getByRole('button', { name: 'Publicar aviso' }))
    expect(
      await within(painel).findByText('O aviso simples tem até 280 caracteres.'),
    ).toBeInTheDocument()
    expect(within(painel).getByRole('textbox', { name: 'Mensagem' })).toHaveAttribute(
      'aria-invalid',
      'true',
    )
  })

  it('a previa do detalhado mostra <script> e <img onerror> como texto, nunca HTML', async () => {
    const { painel } = await abrirNovoAviso()
    fireEvent.click(within(painel).getByRole('radio', { name: /Detalhado/ }))
    fireEvent.change(await within(painel).findByRole('textbox', { name: 'Conteúdo' }), {
      target: { value: '## Título\n<script>alert(1)</script><img src=x onerror=alert(2)>' },
    })
    const previa = await within(painel).findByRole('region', { name: 'Prévia do aviso' })
    expect(within(previa).getByRole('heading', { level: 3, name: 'Título' })).toBeInTheDocument()
    expect(previa.querySelector('script')).toBeNull()
    expect(previa.querySelector('img')).toBeNull()
  })

  it('editar: tipo travado, "Salvar alteracoes" chama editarAviso sem publicarEmUtc no aviso ativo', async () => {
    const editar = capturar('editarAviso')
    const { user } = renderizarTela('/rh/avisos/409/editar')
    const painel = await screen.findByRole('dialog', { name: 'Editar aviso' })
    const titulo = await within(painel).findByRole('textbox', { name: 'Título' })
    expect(titulo).toHaveValue('Política de trabalho remoto atualizada')
    expect(within(painel).getByRole('radio', { name: /Simples/ })).toHaveAttribute(
      'aria-disabled',
      'true',
    )
    expect(within(painel).getByLabelText('Publicar em')).toBeDisabled()
    expect(within(painel).getByLabelText('Expira em')).toHaveValue('2026-11-06')
    expect(screen.getByText('Editar não envia o e-mail de novo.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }))
    await waitFor(() => expect(editar.quantas()).toBe(1))
    expect(editar.ultima().corpo).toEqual({
      titulo: 'Política de trabalho remoto atualizada',
      conteudo: DETALHE_RH.conteudo.trim(),
      expiraEm: '2026-11-06',
      fixado: true,
    })
    editar.conforme()
  })

  it('editar um rascunho e "Publicar aviso" chama editarAviso e depois publicarAviso', async () => {
    capturar('obterAviso', {
      corpo: {
        ...DETALHE_RH,
        situacao: 'RASCUNHO',
        leitura: null,
        publicacaoEmUtc: null,
      } satisfies AvisoRh,
    })
    const editar = capturar('editarAviso')
    const publicar = capturar('publicarAviso')
    const { user } = renderizarTela('/rh/avisos/409/editar')
    const painel = await screen.findByRole('dialog', { name: 'Editar aviso' })
    await within(painel).findByRole('textbox', { name: 'Título' })
    await user.click(screen.getByRole('button', { name: 'Publicar aviso' }))
    await waitFor(() => expect(publicar.quantas()).toBe(1))
    expect(editar.quantas()).toBe(1)
    expect(editar.ultima().corpo).toMatchObject({ publicarEmUtc: null })
    publicar.conforme()
    editar.conforme()
  })
})
