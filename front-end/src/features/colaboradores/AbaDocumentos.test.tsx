import { screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { exemplos } from '../../shared/mocks/gerado/handlers'
import { capturar, problema } from '../../test/capturar'
import { renderizarTela } from '../../test/telas'

declare global {
  interface Window {
    __x?: number
  }
}

type Lista = { itens: Record<string, unknown>[] }
const LISTA = exemplos.listarDocumentosDoFuncionario.corpo as Lista

async function abrir() {
  const tela = renderizarTela('/rh/colaboradores/12?aba=documentos')
  await screen.findByRole('dialog', { name: 'Ficha do colaborador' })
  return tela
}

afterEach(() => vi.restoreAllMocks())

describe('Aba Documentos (tela 09)', () => {
  it('mostra o texto de abertura, o botao Adicionar documento e as colunas, e a requisicao e conforme', async () => {
    const lista = capturar('listarDocumentosDoFuncionario')
    await abrir()
    expect(
      await screen.findByText(
        'Documentos guardados na ficha. Só o RH e o próprio colaborador conseguem ver.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Adicionar documento' })).toBeInTheDocument()
    const tabela = await screen.findByRole('table', { name: 'Documentos' })
    expect(
      within(tabela)
        .getAllByRole('columnheader')
        .map((c) => c.textContent),
    ).toEqual(['Documento', 'Enviado em', 'Situação', 'Ação'])
    expect(screen.getByRole('tab', { name: 'Documentos' })).toHaveAttribute('aria-selected', 'true')
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(0))
    expect(lista.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '12' })
    lista.conforme()
  })

  it('linha enviada com Ver; linha pendente com pedido, validade e SEM botao Lembrar', async () => {
    await abrir()
    const tabela = await screen.findByRole('table', { name: 'Documentos' })
    const enviada = (
      await within(tabela).findByText('Documento de identidade (RG ou CNH)')
    ).closest('tr')!
    expect(within(enviada).getByText('14/03/2022')).toBeInTheDocument()
    expect(within(enviada).getByText('Enviado')).toBeInTheDocument()
    expect(within(enviada).getByRole('button', { name: 'Ver' })).toBeInTheDocument()
    const pendente = within(tabela).getByText('Comprovante de residência').closest('tr')!
    expect(within(pendente).getByText('Pendente')).toBeInTheDocument()
    expect(within(pendente).getByText('Atualização pedida em 01/10')).toBeInTheDocument()
    expect(within(pendente).getByText('Comprovante atualizado')).toBeInTheDocument()
    expect(within(pendente).getByText('Válido até 01/10/2027')).toBeInTheDocument()
    expect(within(pendente).queryByRole('button')).toBeNull()
    expect(screen.queryByRole('button', { name: 'Lembrar' })).toBeNull()
  })

  it('Ver chama baixarDocumentoDoFuncionario e abre o link temporario; 422 mostra o detail', async () => {
    const cliques: string[] = []
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      cliques.push(this.href)
    })
    const baixar = capturar('baixarDocumentoDoFuncionario')
    const { user, unmount } = await abrir()
    await user.click(await screen.findByRole('button', { name: 'Ver' }))
    await waitFor(() => expect(cliques).toHaveLength(1))
    expect(baixar.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '12', documentoId: '61' })
    baixar.conforme()
    unmount()
    capturar(
      'baixarDocumentoDoFuncionario',
      problema(422, {
        codigo: 'DOCUMENTO_SEM_ARQUIVO',
        detail: 'Este documento ainda não foi enviado.',
      }),
    )
    const outra = await abrir()
    await outra.user.click(await screen.findByRole('button', { name: 'Ver' }))
    expect(await screen.findByText('Este documento ainda não foi enviado.')).toBeInTheDocument()
  })

  it('vazio e erro com Tentar de novo', async () => {
    capturar('listarDocumentosDoFuncionario', { corpo: { itens: [] } })
    const { unmount } = await abrir()
    expect(await screen.findByText('Nenhum documento na ficha ainda.')).toBeInTheDocument()
    unmount()
    const lista = capturar('listarDocumentosDoFuncionario', problema(500))
    const outra = await abrir()
    await outra.user.click(await screen.findByRole('button', { name: 'Tentar de novo' }))
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(1))
  })

  it('o texto da descricao e mostrado como texto, nunca como HTML', async () => {
    capturar('listarDocumentosDoFuncionario', {
      corpo: { itens: [{ ...LISTA.itens[1], descricao: '<img src=x onerror="window.__x=1">' }] },
    })
    await abrir()
    expect(await screen.findByText('<img src=x onerror="window.__x=1">')).toBeInTheDocument()
    expect(window.__x).toBeUndefined()
  })
})
