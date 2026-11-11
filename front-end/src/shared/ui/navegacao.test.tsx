import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { Etapas } from './Etapas'
import { MenuDeAcoes } from './MenuDeAcoes'
import { Paginacao } from './Paginacao'

describe('Paginacao', () => {
  it('Paginacao escreve Mostrando 1 a 8 de 101 e desabilita Anterior na primeira pagina', () => {
    render(
      <Paginacao
        dados={{ pagina: 1, tamanhoPagina: 8, total: 101, totalPaginas: 13 }}
        aoMudar={() => {}}
      />,
    )
    expect(screen.getByText('Mostrando 1 a 8 de 101')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeEnabled()
  })

  it('Paginacao na ultima pagina escreve 97 a 101 e desabilita Proxima', () => {
    render(
      <Paginacao
        dados={{ pagina: 13, tamanhoPagina: 8, total: 101, totalPaginas: 13 }}
        aoMudar={() => {}}
      />,
    )
    expect(screen.getByText('Mostrando 97 a 101 de 101')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled()
  })

  it('Paginacao chama aoMudar com pagina anterior e proxima', async () => {
    const usuario = userEvent.setup()
    const aoMudar = vi.fn()
    render(
      <Paginacao
        dados={{ pagina: 3, tamanhoPagina: 8, total: 101, totalPaginas: 13 }}
        aoMudar={aoMudar}
      />,
    )
    await usuario.click(screen.getByRole('button', { name: 'Anterior' }))
    expect(aoMudar).toHaveBeenLastCalledWith(2)
    await usuario.click(screen.getByRole('button', { name: 'Próxima' }))
    expect(aoMudar).toHaveBeenLastCalledWith(4)
  })

  it('Paginacao com total 0 escreve Nenhum registro e desabilita os dois', () => {
    render(
      <Paginacao
        dados={{ pagina: 1, tamanhoPagina: 20, total: 0, totalPaginas: 0 }}
        aoMudar={() => {}}
      />,
    )
    expect(screen.getByText('Nenhum registro')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled()
  })

  it('Paginacao escreve 1.284 com ponto de milhar', () => {
    render(
      <Paginacao
        dados={{ pagina: 1, tamanhoPagina: 20, total: 1284, totalPaginas: 65 }}
        aoMudar={() => {}}
      />,
    )
    expect(screen.getByText('Mostrando 1 a 20 de 1.284')).toBeInTheDocument()
  })
})

describe('Etapas', () => {
  it('Etapas marca a atual com aria-current=step e as anteriores como concluidas', () => {
    render(
      <Etapas
        etapas={[
          { chave: 'detalhes', rotulo: 'Detalhes' },
          { chave: 'perguntas', rotulo: 'Perguntas' },
          { chave: 'verificacao', rotulo: 'Verificação' },
        ]}
        atual="perguntas"
      />,
    )
    const lista = screen.getByRole('list', { name: 'Etapas' })
    const itens = within(lista).getAllByRole('listitem')
    expect(itens[1]).toHaveAttribute('aria-current', 'step')
    expect(itens[0]).not.toHaveAttribute('aria-current')
    expect(itens[0]).toHaveTextContent('concluída')
    expect(itens[2]).not.toHaveTextContent('concluída')
  })
})

describe('MenuDeAcoes', () => {
  it('MenuDeAcoes abre, chama a acao escolhida e fecha', async () => {
    const usuario = userEvent.setup()
    const editar = vi.fn()
    render(
      <MenuDeAcoes
        rotulo="Mais ações"
        acoes={[
          { chave: 'editar', rotulo: 'Editar', aoEscolher: editar },
          { chave: 'inativar', rotulo: 'Inativar', aoEscolher: () => {}, perigo: true },
        ]}
      />,
    )
    await usuario.click(screen.getByRole('button', { name: 'Mais ações' }))
    await usuario.click(await screen.findByRole('menuitem', { name: 'Editar' }))
    expect(editar).toHaveBeenCalledTimes(1)
    expect(screen.queryByRole('menuitem', { name: 'Editar' })).not.toBeInTheDocument()
  })

  it('MenuDeAcoes nao chama acao desabilitada', async () => {
    const usuario = userEvent.setup()
    const editar = vi.fn()
    render(
      <MenuDeAcoes
        rotulo="Mais ações"
        acoes={[{ chave: 'editar', rotulo: 'Editar', aoEscolher: editar, desabilitada: true }]}
      />,
    )
    await usuario.click(screen.getByRole('button', { name: 'Mais ações' }))
    await usuario.click(await screen.findByRole('menuitem', { name: 'Editar' }))
    expect(editar).not.toHaveBeenCalled()
  })
})
