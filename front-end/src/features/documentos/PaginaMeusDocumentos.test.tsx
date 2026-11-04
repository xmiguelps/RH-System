import { screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { exemplos } from '../../shared/mocks/gerado/handlers'
import { capturar, problema } from '../../test/capturar'
import { renderizarTela } from '../../test/telas'
import { enviarMeuArquivo } from './enviarDocumento'

vi.mock('./enviarDocumento', () => ({ adicionarDocumento: vi.fn(), enviarMeuArquivo: vi.fn() }))

declare global {
  interface Window {
    __x?: number
  }
}

type Lista = { itens: Record<string, unknown>[] }
const LISTA = exemplos.listarMeusDocumentos.corpo as Lista
const pdf = () => new File(['%PDF'], 'comprovante.pdf', { type: 'application/pdf' })

afterEach(() => {
  vi.clearAllMocks()
  vi.restoreAllMocks()
})

describe('Meus documentos', () => {
  it.each(['Funcionario', 'Gestor', 'RH'] as const)(
    'abre para %s no menu dele, sem 403',
    async (perfil) => {
      renderizarTela('/eu/meus-documentos', { perfil })
      expect(
        await screen.findByRole('heading', { level: 1, name: 'Meus documentos' }),
      ).toBeInTheDocument()
      const menu = screen.getByRole('navigation', { name: 'Menu principal' })
      expect(within(menu).getByRole('link', { name: /^Meus documentos/ })).toHaveAttribute(
        'aria-current',
        'page',
      )
    },
  )

  it('titulo, apoio, colunas e a bolinha do menu; a requisicao e conforme', async () => {
    const lista = capturar('listarMeusDocumentos')
    renderizarTela('/eu/meus-documentos', { perfil: 'Funcionario' })
    expect(
      await screen.findByText(
        'Os documentos que o RH guarda na sua ficha e os que ele pediu para você enviar.',
      ),
    ).toBeInTheDocument()
    const tabela = await screen.findByRole('table', { name: 'Meus documentos' })
    expect(
      within(tabela)
        .getAllByRole('columnheader')
        .map((c) => c.textContent),
    ).toEqual(['Documento', 'Validade', 'Situação', 'Data', 'Ação'])
    const item = within(screen.getByRole('navigation', { name: 'Menu principal' })).getByRole(
      'link',
      {
        name: /^Meus documentos/,
      },
    )
    await waitFor(() => expect(within(item).getByText('1')).toBeInTheDocument())
    lista.conforme()
  })

  it('pendente com Enviar arquivo; enviado com Ver; nao ha botao Adicionar', async () => {
    renderizarTela('/eu/meus-documentos', { perfil: 'Funcionario' })
    const tabela = await screen.findByRole('table', { name: 'Meus documentos' })
    const pendente = (await within(tabela).findByText('Comprovante de residência')).closest('tr')!
    expect(within(pendente).getByText('Pendente')).toBeInTheDocument()
    expect(within(pendente).getByText('Pedido em 01/10')).toBeInTheDocument()
    expect(within(pendente).getByText('01/10/2027')).toBeInTheDocument()
    expect(within(pendente).getByRole('button', { name: 'Enviar arquivo' })).toBeInTheDocument()
    const enviado = within(tabela).getByText('Documento de identidade (RG ou CNH)').closest('tr')!
    expect(within(enviado).getByText('Enviado em 14/03/2022')).toBeInTheDocument()
    expect(within(enviado).getByRole('button', { name: 'Ver' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Adicionar/ })).toBeNull()
  })

  it('Ver chama baixarMeuDocumento e abre o link temporario', async () => {
    const cliques: string[] = []
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      cliques.push(this.href)
    })
    const baixar = capturar('baixarMeuDocumento')
    const { user } = renderizarTela('/eu/meus-documentos', { perfil: 'Funcionario' })
    await user.click(await screen.findByRole('button', { name: 'Ver' }))
    await waitFor(() => expect(cliques).toHaveLength(1))
    expect(baixar.ultima().parametrosDoCaminho).toEqual({ documentoId: '61' })
    baixar.conforme()
  })

  it('enviar chama enviarMeuArquivo, fecha, refaz a lista e as bolinhas', async () => {
    vi.mocked(enviarMeuArquivo).mockResolvedValue(exemplos.enviarMeuDocumento.corpo as never)
    const lista = capturar('listarMeusDocumentos')
    const contadores = capturar('obterContadoresDoMenuDoFuncionario')
    const { user } = renderizarTela('/eu/meus-documentos', { perfil: 'Funcionario' })
    await user.click(await screen.findByRole('button', { name: 'Enviar arquivo' }))
    const modal = await screen.findByRole('dialog', { name: 'Enviar Comprovante de residência' })
    expect(within(modal).getByText('Arraste o arquivo aqui')).toBeInTheDocument()
    await waitFor(() => expect(contadores.quantas()).toBeGreaterThan(0))
    const [l0, c0] = [lista.quantas(), contadores.quantas()]
    const arquivo = pdf()
    await user.upload(within(modal).getByLabelText('Escolher arquivo'), arquivo)
    await user.click(screen.getByRole('button', { name: 'Enviar documento' }))
    await waitFor(() => expect(enviarMeuArquivo).toHaveBeenCalledWith(64, arquivo))
    await waitFor(() => expect(screen.queryByRole('dialog', { name: /^Enviar / })).toBeNull())
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(l0))
    await waitFor(() => expect(contadores.quantas()).toBeGreaterThan(c0))
  })

  it('400 em arquivo aparece junto da area; 422 DOCUMENTO_NAO_PEDIDO recarrega a lista e avisa', async () => {
    vi.mocked(enviarMeuArquivo).mockRejectedValueOnce(
      new ErroDeApi(400, 'x', { errors: { arquivo: ['O arquivo não é um PDF de verdade.'] } }),
    )
    const lista = capturar('listarMeusDocumentos')
    const { user } = renderizarTela('/eu/meus-documentos', { perfil: 'Funcionario' })
    await user.click(await screen.findByRole('button', { name: 'Enviar arquivo' }))
    const modal = await screen.findByRole('dialog', { name: 'Enviar Comprovante de residência' })
    await user.upload(within(modal).getByLabelText('Escolher arquivo'), pdf())
    await user.click(screen.getByRole('button', { name: 'Enviar documento' }))
    expect(await within(modal).findByText('O arquivo não é um PDF de verdade.')).toBeInTheDocument()
    vi.mocked(enviarMeuArquivo).mockRejectedValueOnce(
      new ErroDeApi(422, 'x', {
        codigo: 'DOCUMENTO_NAO_PEDIDO',
        detail: 'Este documento não está aguardando envio.',
      }),
    )
    const antes = lista.quantas()
    await user.click(screen.getByRole('button', { name: 'Enviar documento' }))
    expect(await screen.findByText('Este documento não está aguardando envio.')).toBeInTheDocument()
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(antes))
  })

  it('vazio, erro com Tentar de novo e descricao como texto', async () => {
    capturar('listarMeusDocumentos', { corpo: { itens: [] } })
    const a = renderizarTela('/eu/meus-documentos', { perfil: 'Funcionario' })
    expect(await screen.findByText('Você ainda não tem documentos na ficha.')).toBeInTheDocument()
    a.unmount()
    capturar('listarMeusDocumentos', problema(500))
    const b = renderizarTela('/eu/meus-documentos', { perfil: 'Funcionario' })
    expect(await screen.findByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
    b.unmount()
    capturar('listarMeusDocumentos', {
      corpo: { itens: [{ ...LISTA.itens[0], descricao: '<img src=x onerror="window.__x=1">' }] },
    })
    renderizarTela('/eu/meus-documentos', { perfil: 'Funcionario' })
    expect(await screen.findByText('<img src=x onerror="window.__x=1">')).toBeInTheDocument()
    expect(window.__x).toBeUndefined()
  })
})
