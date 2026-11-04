import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ErroDeApi } from '../api/erroDeApi'
import { Tabela, type ColunaDaTabela } from './Tabela'

type Linha = { id: number; nome: string; setor: string }

const COLUNAS: ColunaDaTabela<Linha>[] = [
  { chave: 'nome', titulo: 'Nome', celula: (l) => l.nome, ordenavel: true },
  { chave: 'setor', titulo: 'Setor', celula: (l) => l.setor },
  { chave: 'acoes', titulo: 'Ações', celula: () => 'Abrir', alinhamento: 'direita' },
]

const LINHAS: Linha[] = [
  { id: 12, nome: 'Bruno Lima', setor: 'Financeiro' },
  { id: 14, nome: 'Carla Rocha', setor: 'Comercial' },
]

describe('Tabela', () => {
  it('Tabela escreve colunas e linhas e se chama pelo rotulo', () => {
    render(
      <Tabela
        rotulo="Colaboradores"
        colunas={COLUNAS}
        linhas={LINHAS}
        chaveDaLinha={(l) => l.id}
      />,
    )
    const tabela = screen.getByRole('table', { name: 'Colaboradores' })
    expect(within(tabela).getAllByRole('columnheader')).toHaveLength(3)
    expect(within(tabela).getAllByRole('row')).toHaveLength(3)
    expect(within(tabela).getByText('Bruno Lima')).toBeInTheDocument()
  })

  it('Tabela com coluna ordenavel tem botao com aria-sort e chama aoOrdenar com a chave', async () => {
    const usuario = userEvent.setup()
    const aoOrdenar = vi.fn()
    render(
      <Tabela
        rotulo="Colaboradores"
        colunas={COLUNAS}
        linhas={LINHAS}
        chaveDaLinha={(l) => l.id}
        ordenacao={{ campo: 'nome', direcao: 'ASC' }}
        aoOrdenar={aoOrdenar}
      />,
    )
    const cabecalhos = screen.getAllByRole('columnheader')
    expect(cabecalhos[0]).toHaveAttribute('aria-sort', 'ascending')
    await usuario.click(screen.getByRole('button', { name: /Nome/ }))
    expect(aoOrdenar).toHaveBeenCalledWith('nome')
    expect(within(cabecalhos[1]!).queryByRole('button')).not.toBeInTheDocument()
  })

  it('Tabela marca aria-sort descending e none', () => {
    const { rerender } = render(
      <Tabela
        rotulo="T"
        colunas={COLUNAS}
        linhas={LINHAS}
        chaveDaLinha={(l) => l.id}
        ordenacao={{ campo: 'nome', direcao: 'DESC' }}
        aoOrdenar={() => {}}
      />,
    )
    expect(screen.getAllByRole('columnheader')[0]).toHaveAttribute('aria-sort', 'descending')
    rerender(
      <Tabela
        rotulo="T"
        colunas={COLUNAS}
        linhas={LINHAS}
        chaveDaLinha={(l) => l.id}
        ordenacao={null}
        aoOrdenar={() => {}}
      />,
    )
    expect(screen.getAllByRole('columnheader')[0]).toHaveAttribute('aria-sort', 'none')
  })

  it('Tabela carregando sem linhas mostra o esqueleto, e com linhas as mantem com aria-busy', () => {
    const { rerender } = render(
      <Tabela
        rotulo="T"
        colunas={COLUNAS}
        linhas={undefined}
        chaveDaLinha={(l) => l.id}
        carregando
      />,
    )
    const tabela = screen.getByRole('table', { name: 'T' })
    expect(within(tabela).getAllByRole('row')).toHaveLength(6)
    expect(screen.getByRole('status')).toHaveTextContent('Carregando')
    rerender(
      <Tabela rotulo="T" colunas={COLUNAS} linhas={LINHAS} chaveDaLinha={(l) => l.id} carregando />,
    )
    expect(screen.getByRole('table', { name: 'T' })).toHaveAttribute('aria-busy', 'true')
    expect(screen.getByText('Carla Rocha')).toBeInTheDocument()
  })

  it('Tabela com erro mostra a mensagem e Tentar de novo no lugar das linhas', async () => {
    const usuario = userEvent.setup()
    const aoTentar = vi.fn()
    render(
      <Tabela
        rotulo="T"
        colunas={COLUNAS}
        linhas={LINHAS}
        chaveDaLinha={(l) => l.id}
        erro={new ErroDeApi(500, 'x')}
        aoTentarDeNovo={aoTentar}
      />,
    )
    expect(screen.getByRole('alert')).toHaveTextContent(
      'Erro no servidor. Tente novamente em instantes.',
    )
    expect(screen.queryByText('Bruno Lima')).not.toBeInTheDocument()
    await usuario.click(screen.getByRole('button', { name: 'Tentar de novo' }))
    expect(aoTentar).toHaveBeenCalledTimes(1)
  })

  it('Tabela com linhas vazias mostra o vazio', () => {
    render(
      <Tabela
        rotulo="T"
        colunas={COLUNAS}
        linhas={[]}
        chaveDaLinha={(l) => l.id}
        vazio={<p>Nenhum colaborador.</p>}
      />,
    )
    expect(screen.getByText('Nenhum colaborador.')).toBeInTheDocument()
  })

  it('Tabela mostra o detalhe sob a linha e o rodape dentro do cartao', () => {
    render(
      <Tabela
        rotulo="T"
        colunas={COLUNAS}
        linhas={LINHAS}
        chaveDaLinha={(l) => l.id}
        detalhe={(l) => (l.id === 12 ? <p>detalhe de {l.nome}</p> : null)}
        rodape={<p>Mostrando 1 a 2 de 2</p>}
      />,
    )
    expect(screen.getByText('detalhe de Bruno Lima')).toBeInTheDocument()
    expect(screen.queryByText('detalhe de Carla Rocha')).not.toBeInTheDocument()
    expect(screen.getByText('Mostrando 1 a 2 de 2')).toBeInTheDocument()
  })
})
