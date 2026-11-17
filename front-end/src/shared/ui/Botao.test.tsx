import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Plus } from 'lucide-react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { Botao } from './Botao'
import { Cartao } from './Cartao'
import { BotaoLink } from './BotaoLink'

describe('Botao', () => {
  it('Botao tem type=button por padrao e chama onClick', async () => {
    const usuario = userEvent.setup()
    const aoClicar = vi.fn()
    render(<Botao onClick={aoClicar}>Salvar</Botao>)
    const botao = screen.getByRole('button', { name: 'Salvar' })
    expect(botao).toHaveAttribute('type', 'button')
    await usuario.click(botao)
    expect(aoClicar).toHaveBeenCalledTimes(1)
  })

  it('Botao carregando fica desabilitado, aria-busy e mantem o nome acessivel (o texto nao muda)', async () => {
    const usuario = userEvent.setup()
    const aoClicar = vi.fn()
    render(
      <Botao carregando onClick={aoClicar}>
        Salvar
      </Botao>,
    )
    const botao = screen.getByRole('button', { name: 'Salvar' })
    expect(botao).toBeDisabled()
    expect(botao).toHaveAttribute('aria-busy', 'true')
    await usuario.click(botao)
    expect(aoClicar).not.toHaveBeenCalled()
  })

  it('o foco de um controle sobre o cartao escuro usa o anel claro (o anel padrao some em cima do fundo escuro) e o botao claro tambem', () => {
    render(
      <Cartao tom="escuro">
        <a href="/x">Pedir ajuste</a>
        <Botao variante="claro">Registrar entrada</Botao>
      </Cartao>,
    )
    expect(screen.getByRole('link').closest('section')!.className).toContain(
      '**:focus-visible:outline-primaria-clara',
    )
    expect(screen.getByRole('button', { name: 'Registrar entrada' }).className).toContain(
      'focus-visible:outline-primaria-clara',
    )
  })

  it('Botao expoe data-variante e data-tamanho', () => {
    render(
      <Botao variante="perigo" tamanho="pequeno">
        Inativar
      </Botao>,
    )
    const botao = screen.getByRole('button', { name: 'Inativar' })
    expect(botao).toHaveAttribute('data-variante', 'perigo')
    expect(botao).toHaveAttribute('data-tamanho', 'pequeno')
  })

  it('Botao com icone mostra um svg aria-hidden', () => {
    render(<Botao icone={Plus}>Adicionar colaborador</Botao>)
    const svg = screen.getByRole('button', { name: 'Adicionar colaborador' }).querySelector('svg')
    expect(svg).not.toBeNull()
    expect(svg).toHaveAttribute('aria-hidden', 'true')
  })

  it('BotaoLink e um link com href e as classes de botao', () => {
    render(
      <MemoryRouter>
        <BotaoLink to="/rh" variante="secundario">
          Ver
        </BotaoLink>
      </MemoryRouter>,
    )
    const link = screen.getByRole('link', { name: 'Ver' })
    expect(link).toHaveAttribute('href', '/rh')
    expect(link).toHaveAttribute('data-variante', 'secundario')
  })
})
