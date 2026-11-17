import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { Paginacao } from './Paginacao'

const dados = (pagina: number, total: number, totalPaginas: number) => ({
  pagina,
  tamanhoPagina: 10,
  total,
  totalPaginas,
})

describe('Paginacao', () => {
  it('mostra o intervalo da pagina e muda para a anterior e a proxima', async () => {
    const usuario = userEvent.setup()
    const aoMudar = vi.fn()
    render(<Paginacao dados={dados(2, 25, 3)} aoMudar={aoMudar} />)
    expect(screen.getByText('Mostrando 11 a 20 de 25')).toBeInTheDocument()
    await usuario.click(screen.getByRole('button', { name: 'Anterior' }))
    await usuario.click(screen.getByRole('button', { name: 'Próxima' }))
    expect(aoMudar.mock.calls).toEqual([[1], [3]])
  })

  it('pagina alem da ultima (apagaram os itens dela) nao mostra intervalo invertido e "Anterior" volta para a ultima que existe', async () => {
    const usuario = userEvent.setup()
    const aoMudar = vi.fn()
    render(<Paginacao dados={dados(4, 25, 3)} aoMudar={aoMudar} />)
    expect(screen.getByText('Mostrando 21 a 25 de 25')).toBeInTheDocument()
    await usuario.click(screen.getByRole('button', { name: 'Anterior' }))
    expect(aoMudar).toHaveBeenCalledWith(3)
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled()
  })

  it('sem registros diz "Nenhum registro" e trava os dois botoes', () => {
    render(<Paginacao dados={dados(1, 0, 0)} aoMudar={() => {}} />)
    expect(screen.getByText('Nenhum registro')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled()
  })

  it('sobrou uma pagina so e a pessoa estava na 2: "Anterior" continua livre para sair da pagina vazia', async () => {
    const usuario = userEvent.setup()
    const aoMudar = vi.fn()
    render(<Paginacao dados={dados(2, 4, 1)} aoMudar={aoMudar} />)
    await usuario.click(screen.getByRole('button', { name: 'Anterior' }))
    expect(aoMudar).toHaveBeenCalledWith(1)
  })
})
