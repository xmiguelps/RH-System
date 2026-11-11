import { screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Esquema } from '../../shared/api/tipos'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { renderizarTela } from '../../test/telas'
import { adicionarModulo } from './enviarModulo'

vi.mock('./enviarModulo', () => ({ adicionarModulo: vi.fn(), atualizarModulo: vi.fn() }))

type Treinamento = Esquema<'TreinamentoRh'>
const ex = <T,>(op: string, status?: number, nome?: string) =>
  exemploDoContrato<T>(op, status, nome)
const TREINAMENTO = ex<Treinamento>('obterTreinamento')
const problemaDoContrato = (op: string, status: number, nome?: string) =>
  problema(status, ex<Record<string, unknown>>(op, status, nome))

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
  vi.mocked(adicionarModulo).mockResolvedValue(TREINAMENTO.modulos[0]!)
})
afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
})

async function abrirLista(rota = '/rh/treinamentos') {
  const tela = renderizarTela(rota)
  await screen.findByRole('heading', { level: 1, name: 'Treinamentos', hidden: true })
  return tela
}
const linha = async (titulo: string) =>
  (
    await within(await screen.findByRole('table', { name: 'Treinamentos' })).findByText(titulo)
  ).closest('tr')!

describe('Treinamentos do RH (tela 22)', { timeout: 20000 }, () => {
  it('indicadores sem "Prazos ate", abas com Rascunhos e a conclusao por tipo, conforme', async () => {
    const lista = capturar('listarTreinamentos')
    const indicadores = capturar('obterIndicadoresDeTreinamentos')
    await abrirLista()
    const numeros = await screen.findByRole('region', { name: 'Números dos treinamentos' })
    expect(await within(numeros).findByText('Certificados emitidos em 2026')).toBeInTheDocument()
    expect(within(numeros).getByText('72')).toBeInTheDocument()
    expect(within(numeros).queryByText(/Prazos até/)).toBeNull()
    expect(await screen.findByRole('tab', { name: /^Rascunhos/ })).toBeInTheDocument()
    const lgpd = await linha('LGPD na prática')
    expect(within(lgpd).getByText('63 de 101 concluíram')).toBeInTheDocument()
    expect(within(lgpd).getByRole('progressbar')).toBeInTheDocument()
    const opcional = await linha('Atendimento ao cliente')
    expect(within(opcional).getByText('0 inscritos')).toBeInTheDocument()
    expect(within(opcional).queryByRole('progressbar')).toBeNull()
    expect(screen.queryByRole('combobox', { name: /tipos/i })).toBeNull()
    lista.conforme()
    indicadores.conforme()
  })

  it('aba e busca vao para listarTreinamentos, conforme', async () => {
    const lista = capturar('listarTreinamentos')
    const { user } = await abrirLista()
    await linha('LGPD na prática')
    await user.click(screen.getByRole('tab', { name: /^Rascunhos/ }))
    await waitFor(() => expect(lista.ultima().consulta.get('situacao')).toBe('RASCUNHO'))
    await user.type(screen.getByRole('searchbox', { name: 'Buscar treinamento' }), 'LGPD')
    await waitFor(() => expect(lista.ultima().consulta.get('busca')).toBe('LGPD'))
    lista.conforme()
  })

  it('"Excluir" fica desligado com certificado emitido; "Encerrar" so em andamento', async () => {
    const { user } = await abrirLista()
    await user.click(
      within(await linha('LGPD na prática')).getByRole('button', { name: /Mais ações/ }),
    )
    const excluir = await screen.findByRole('menuitem', { name: 'Excluir' })
    expect(excluir).toHaveAttribute('aria-disabled', 'true')
    expect(screen.getByRole('menuitem', { name: 'Encerrar' })).toBeInTheDocument()
    await user.keyboard('{Escape}')
    await user.click(
      within(await linha('Atendimento ao cliente')).getByRole('button', { name: /Mais ações/ }),
    )
    expect(await screen.findByRole('menuitem', { name: 'Excluir' })).not.toHaveAttribute(
      'aria-disabled',
      'true',
    )
    expect(screen.queryByRole('menuitem', { name: 'Encerrar' })).toBeNull()
  })

  it('"Encerrar" com 409 mostra os numeros e repete com confirmar=true', async () => {
    capturar('encerrarTreinamento', problemaDoContrato('encerrarTreinamento', 409))
    const { user } = await abrirLista()
    await user.click(
      within(await linha('LGPD na prática')).getByRole('button', { name: /Mais ações/ }),
    )
    await user.click(await screen.findByRole('menuitem', { name: 'Encerrar' }))
    const confirmacao = await screen.findByRole('alertdialog', {
      name: 'Encerrar este treinamento?',
    })
    expect(
      within(confirmacao).getByText(
        '38 pessoas ainda não concluíram este treinamento (21 em andamento, 17 não começaram). Quem já começou continua e termina; quem não começou deixa de poder começar. Encerrar mesmo assim?',
      ),
    ).toBeInTheDocument()
    const segundo = capturar('encerrarTreinamento')
    await user.click(within(confirmacao).getByRole('button', { name: 'Encerrar' }))
    await waitFor(() => expect(segundo.quantas()).toBe(1))
    expect(segundo.ultima().consulta.get('confirmar')).toBe('true')
    segundo.conforme()
    expect(
      await screen.findByText('Treinamento encerrado. Quem já começou ainda pode terminar.'),
    ).toBeInTheDocument()
  })

  it('"Excluir" com 422 TREINAMENTO_COM_CERTIFICADOS mostra o detail', async () => {
    capturar('excluirTreinamento', problemaDoContrato('excluirTreinamento', 422))
    const { user } = await abrirLista()
    await user.click(
      within(await linha('Atendimento ao cliente')).getByRole('button', { name: /Mais ações/ }),
    )
    await user.click(await screen.findByRole('menuitem', { name: 'Excluir' }))
    const confirmacao = await screen.findByRole('alertdialog', {
      name: 'Excluir este treinamento?',
    })
    await user.click(within(confirmacao).getByRole('button', { name: 'Excluir' }))
    expect(
      await within(confirmacao).findByText(
        'Este treinamento já emitiu 63 certificados e não pode ser excluído. Encerre o treinamento para que ninguém novo entre.',
      ),
    ).toBeInTheDocument()
  })

  it('"Quem fez": tamanhoPagina=10, abas e setor; sem coluna Nota; "Baixar" so com certificadoDisponivel; CSV e certificado', async () => {
    const participantes = capturar('listarParticipantesDeTreinamento')
    const exportar = capturar('exportarParticipantesDeTreinamento')
    const certificado = capturar('baixarCertificadoDeParticipante')
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:teste')
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})
    const { user } = await abrirLista()
    await user.click(
      within(await linha('LGPD na prática')).getByRole('button', {
        name: 'Ver quem fez LGPD na prática',
      }),
    )
    const tabela = await screen.findByRole('table', { name: 'Quem fez' })
    expect(participantes.ultima().consulta.get('tamanhoPagina')).toBe('10')
    expect(within(tabela).queryByRole('columnheader', { name: 'Nota' })).toBeNull()
    expect(await within(tabela).findByText('Bruno Lima')).toBeInTheDocument()
    const itens = ex<Esquema<'TreinamentoParticipantesLista'>>(
      'listarParticipantesDeTreinamento',
    ).itens
    expect(
      within(tabela).getAllByRole('button', { name: /^Baixar o certificado de / }),
    ).toHaveLength(itens.filter((p) => p.certificadoDisponivel).length)
    await user.click(screen.getByRole('tab', { name: 'Em andamento' }))
    await waitFor(() =>
      expect(participantes.ultima().consulta.get('situacao')).toBe('EM_ANDAMENTO'),
    )
    await user.click(screen.getByRole('combobox', { name: 'Setor' }))
    await user.click(await screen.findByRole('option', { name: 'Comercial' }))
    await waitFor(() => expect(participantes.ultima().consulta.get('setorId')).toBe('1'))
    participantes.conforme()
    await user.click(screen.getByRole('button', { name: 'Baixar lista' }))
    await waitFor(() => expect(exportar.quantas()).toBe(1))
    expect(exportar.ultima().consulta.get('situacao')).toBe('EM_ANDAMENTO')
    exportar.conforme()
    await user.click(screen.getByRole('tab', { name: 'Todos' }))
    await user.click(
      (await screen.findAllByRole('button', { name: /^Baixar o certificado de / }))[0]!,
    )
    await waitFor(() => expect(certificado.quantas()).toBe(1))
    certificado.conforme()
  })
})

async function abrirAssistente(rota: string) {
  const tela = renderizarTela(rota)
  const janela = await screen.findByRole('dialog', { name: /treinamento$/ })
  return { ...tela, janela }
}

describe('Assistente do treinamento (telas 23 a 26)', { timeout: 20000 }, () => {
  it('sem "Tipo" e sem "Termina em"; a etapa 3 se chama "Certificado"; criar manda o comando inteiro', async () => {
    const criar = capturar('criarTreinamento')
    const { user, janela } = await abrirAssistente('/rh/treinamentos/novo')
    expect(within(janela).queryByText(/Termina em|Tipo/)).toBeNull()
    expect(within(janela).getByText('Certificado', { selector: 'span' })).toBeInTheDocument()
    expect(within(janela).queryByText('Avaliação')).toBeNull()
    await user.type(
      within(janela).getByRole('textbox', { name: 'Título' }),
      'Prevenção de assédio no trabalho',
    )
    await user.click(within(janela).getByRole('button', { name: 'Próximo' }))
    await waitFor(() => expect(criar.quantas()).toBe(1))
    expect(criar.ultima().corpo).toEqual({
      titulo: 'Prevenção de assédio no trabalho',
      descricao: null,
      comecaEm: null,
      obrigatorio: true,
      onboarding: false,
      emiteCertificado: true,
      publico: 'TODOS',
      setorIds: [],
      funcionarioIds: [],
      incluiAdmitidos: true,
    })
    criar.conforme()
  })

  it('editar: o PUT envia o comando inteiro lido do treinamento', async () => {
    const atualizar = capturar('atualizarTreinamento')
    const { user, janela } = await abrirAssistente('/rh/treinamentos/6/editar')
    await within(janela).findByDisplayValue('Prevenção de assédio no trabalho')
    await user.click(within(janela).getByRole('button', { name: 'Salvar rascunho e sair' }))
    await waitFor(() => expect(atualizar.quantas()).toBe(1))
    expect(atualizar.ultima().corpo).toMatchObject({
      titulo: 'Prevenção de assédio no trabalho',
      comecaEm: '2026-11-16',
      obrigatorio: true,
      emiteCertificado: true,
      publico: 'TODOS',
      incluiAdmitidos: true,
    })
    atualizar.conforme()
  })

  it('Conteudo: sem "Aula presencial"; adicionar modulo usa o envio multipart', async () => {
    const { user, janela } = await abrirAssistente('/rh/treinamentos/6/editar?etapa=conteudo')
    await within(janela).findByRole('list', { name: 'Módulos do treinamento' })
    expect(within(janela).queryByRole('button', { name: /Aula presencial/ })).toBeNull()
    await user.click(within(janela).getByRole('button', { name: 'Link' }))
    const editor = within(janela).getByRole('region', { name: 'Novo módulo' })
    await user.type(
      within(editor).getByRole('textbox', { name: 'Título do módulo' }),
      'Política interna',
    )
    await user.type(within(editor).getByRole('textbox', { name: 'Endereço do link' }), 'ftp://x')
    await user.click(within(editor).getByRole('button', { name: 'Salvar módulo' }))
    expect(
      await within(editor).findByText('O endereço precisa começar com http:// ou https://.'),
    ).toBeInTheDocument()
    await user.clear(within(editor).getByRole('textbox', { name: 'Endereço do link' }))
    await user.type(
      within(editor).getByRole('textbox', { name: 'Endereço do link' }),
      'https://intranet.exemplo.com.br/politica',
    )
    await user.click(within(editor).getByRole('button', { name: 'Salvar módulo' }))
    await waitFor(() => expect(adicionarModulo).toHaveBeenCalledTimes(1))
    expect(adicionarModulo).toHaveBeenCalledWith(6, {
      titulo: 'Política interna',
      tipo: 'LINK',
      duracaoMinutos: null,
      urlExterna: 'https://intranet.exemplo.com.br/politica',
      conteudoTexto: null,
      arquivo: null,
    })
  })

  it('Conteudo: o PDF do modulo e conferido antes de enviar (ate 10 MB)', async () => {
    const { user, janela } = await abrirAssistente('/rh/treinamentos/6/editar?etapa=conteudo')
    await within(janela).findByRole('list', { name: 'Módulos do treinamento' })
    await user.click(within(janela).getByRole('button', { name: 'Arquivo PDF' }))
    const editor = within(janela).getByRole('region', { name: 'Novo módulo' })
    await user.upload(
      within(editor).getByLabelText('Escolher arquivo'),
      new File(['%'.repeat(10 * 1024 * 1024 + 1)], 'grande.pdf', { type: 'application/pdf' }),
    )
    expect(await within(editor).findByText('O arquivo passa de 10 MB.')).toBeInTheDocument()
    await user.type(within(editor).getByRole('textbox', { name: 'Título do módulo' }), 'Canais')
    await user.click(within(editor).getByRole('button', { name: 'Salvar módulo' }))
    expect(await within(editor).findByText('Escolha o PDF.')).toBeInTheDocument()
    expect(adicionarModulo).not.toHaveBeenCalled()
  })

  it('Conteudo: reordenar manda todos os ids na nova ordem; excluir chama excluirModuloDeTreinamento', async () => {
    const reordenar = capturar('reordenarModulosDeTreinamento')
    const excluir = capturar('excluirModuloDeTreinamento', { status: 204 })
    const { user, janela } = await abrirAssistente('/rh/treinamentos/6/editar?etapa=conteudo')
    await within(janela).findByRole('list', { name: 'Módulos do treinamento' })
    await user.click(within(janela).getByRole('button', { name: 'Descer o módulo 1' }))
    await waitFor(() => expect(reordenar.quantas()).toBe(1))
    expect(reordenar.ultima().corpo).toEqual({ moduloIds: [22, 21, 23] })
    reordenar.conforme()
    await user.click(within(janela).getByRole('button', { name: 'Editar o módulo 3' }))
    await user.click(within(janela).getByRole('button', { name: 'Remover módulo' }))
    await waitFor(() => expect(excluir.quantas()).toBe(1))
    expect(excluir.ultima().parametrosDoCaminho).toEqual({ treinamentoId: '6', moduloId: '23' })
    excluir.conforme()
  })

  it('publicado: campos travados abrem desligados e os modulos so se corrigem', async () => {
    capturar('obterTreinamento', {
      corpo: { ...TREINAMENTO, publicado: true, situacao: 'EM_ANDAMENTO' } satisfies Treinamento,
    })
    const { janela } = await abrirAssistente('/rh/treinamentos/6/editar')
    expect(await within(janela).findByRole('checkbox', { name: /Obrigatório/ })).toHaveAttribute(
      'aria-disabled',
      'true',
    )
    expect(within(janela).getByLabelText('Começa em')).toBeDisabled()
  })

  it('publicado: os modulos so se corrigem (sem acrescentar, mover nem remover)', async () => {
    capturar('obterTreinamento', {
      corpo: { ...TREINAMENTO, publicado: true, situacao: 'EM_ANDAMENTO' } satisfies Treinamento,
    })
    const { janela } = await abrirAssistente('/rh/treinamentos/6/editar?etapa=conteudo')
    expect(
      await within(janela).findByText(
        /Não dá mais para acrescentar, reordenar nem excluir módulos/,
      ),
    ).toBeInTheDocument()
    expect(within(janela).queryByRole('button', { name: /Descer o módulo/ })).toBeNull()
    expect(within(janela).queryByText('Adicionar módulo:')).toBeNull()
    expect(within(janela).getByRole('button', { name: 'Editar o módulo 1' })).toBeInTheDocument()
  })

  it('Publicacao: sem as caixas de e-mail; contagem com setorIds repetidos; publicar sem modulo mostra errors.modulos', async () => {
    const contagem = capturar('contarPublicoDeTreinamento')
    capturar('publicarTreinamento', problemaDoContrato('publicarTreinamento', 400))
    const { user, janela } = await abrirAssistente('/rh/treinamentos/6/editar?etapa=publicacao')
    await within(janela).findByRole('radio', { name: /Todos/ })
    expect(within(janela).queryByText(/Avisar por e-mail|Lembrar quem não começou/)).toBeNull()
    expect(
      within(janela).getByRole('checkbox', { name: /Incluir quem for admitido depois/ }),
    ).toBeInTheDocument()
    await user.click(within(janela).getByRole('radio', { name: /Setores/ }))
    await user.click(await within(janela).findByRole('checkbox', { name: 'Comercial' }))
    await user.click(within(janela).getByRole('checkbox', { name: 'Financeiro' }))
    await waitFor(() => expect(contagem.ultima().consulta.getAll('setorIds')).toHaveLength(2))
    expect(contagem.ultima().consulta.get('publico')).toBe('SETORES')
    contagem.conforme()
    await user.click(within(janela).getByRole('button', { name: 'Publicar treinamento' }))
    expect(await within(janela).findByText(/Inclua pelo menos um módulo\./)).toBeInTheDocument()
  })
})

describe('Ficha do RH sem a aba Treinamentos (decisao do dono, 2026-11-02)', () => {
  it('a ficha do RH nao tem a aba Treinamentos e nao chama listarTreinamentosDeFuncionario', async () => {
    const rota = capturar('listarTreinamentosDeFuncionario')
    renderizarTela('/rh/colaboradores/12')
    const abas = await screen.findByRole('tablist', { name: 'Partes da ficha' })
    expect(within(abas).queryByRole('tab', { name: 'Treinamentos' })).toBeNull()
    expect(rota.quantas()).toBe(0)
  })
})
