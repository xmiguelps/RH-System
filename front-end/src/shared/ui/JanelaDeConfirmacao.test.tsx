import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ErroDeApi } from '../api/erroDeApi'
import { JanelaDeConfirmacao } from './JanelaDeConfirmacao'

describe('JanelaDeConfirmacao', () => {
  it('simples: tem role alertdialog; Confirmar chama aoConfirmar e Cancelar e Escape chamam aoCancelar', async () => {
    const usuario = userEvent.setup()
    const aoConfirmar = vi.fn()
    const aoCancelar = vi.fn()
    render(
      <JanelaDeConfirmacao
        aberta
        titulo="Excluir cargo?"
        descricao="O cargo some da lista."
        aoConfirmar={aoConfirmar}
        aoCancelar={aoCancelar}
      />,
    )
    expect(screen.getByRole('alertdialog', { name: 'Excluir cargo?' })).toBeInTheDocument()
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }))
    expect(aoConfirmar).toHaveBeenCalledTimes(1)
    await usuario.click(screen.getByRole('button', { name: 'Cancelar' }))
    expect(aoCancelar).toHaveBeenCalledTimes(1)
    await usuario.keyboard('{Escape}')
    expect(aoCancelar).toHaveBeenCalledTimes(2)
  })

  it('digitar: o botao so habilita com "desativar" (sem diferenca de maiuscula ou espaco nas pontas) e Enter confirma', async () => {
    const usuario = userEvent.setup()
    const aoConfirmar = vi.fn()
    const props = {
      titulo: 'Inativar Bruno Lima?',
      descricao: 'Ele perde o acesso.',
      variante: 'digitar' as const,
      textoParaDigitar: 'desativar',
      aoConfirmar,
      aoCancelar: () => {},
    }
    const { rerender } = render(<JanelaDeConfirmacao aberta {...props} />)
    const campo = screen.getByLabelText('Digite "desativar" para confirmar')
    const botao = screen.getByRole('button', { name: 'Confirmar' })
    expect(botao).toBeDisabled()
    await usuario.type(campo, 'desativa')
    expect(botao).toBeDisabled()
    await usuario.clear(campo)
    await usuario.type(campo, 'Desativar ')
    expect(botao).toBeEnabled()
    expect(botao).toHaveAttribute('data-variante', 'perigo')
    await usuario.type(campo, '{Enter}')
    expect(aoConfirmar).toHaveBeenCalledTimes(1)

    rerender(<JanelaDeConfirmacao aberta={false} {...props} />)
    rerender(<JanelaDeConfirmacao aberta {...props} />)
    expect(screen.getByLabelText('Digite "desativar" para confirmar')).toHaveValue('')
  })

  it('pendentes mostra "38 pessoas ainda nao concluiram este treinamento." e "1 pessoa ainda nao concluiu"', () => {
    const { rerender } = render(
      <JanelaDeConfirmacao
        aberta
        titulo="Encerrar treinamento?"
        variante="pendentes"
        pendentes={38}
        assunto="este treinamento"
        consequencia="Elas não poderão mais concluir."
        aoConfirmar={() => {}}
        aoCancelar={() => {}}
      />,
    )
    expect(
      screen.getByText(
        '38 pessoas ainda não concluíram este treinamento. Elas não poderão mais concluir. Quer mesmo continuar?',
      ),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Continuar mesmo assim' })).toBeInTheDocument()
    rerender(
      <JanelaDeConfirmacao
        aberta
        titulo="Encerrar treinamento?"
        variante="pendentes"
        pendentes={1}
        assunto="este treinamento"
        aoConfirmar={() => {}}
        aoCancelar={() => {}}
      />,
    )
    expect(
      screen.getByText('1 pessoa ainda não concluiu este treinamento. Quer mesmo continuar?'),
    ).toBeInTheDocument()
  })

  it('pendentes com null nao diz quantas pessoas faltam', () => {
    render(
      <JanelaDeConfirmacao
        aberta
        titulo="Encerrar formulário?"
        variante="pendentes"
        pendentes={null}
        assunto="este formulário"
        aoConfirmar={() => {}}
        aoCancelar={() => {}}
      />,
    )
    expect(
      screen.getByText(
        'Ainda há pessoas que não concluíram este formulário. Quer mesmo continuar?',
      ),
    ).toBeInTheDocument()
  })

  it('confirmando desabilita os dois botoes e mantem os rotulos', () => {
    render(
      <JanelaDeConfirmacao
        aberta
        confirmando
        titulo="Excluir?"
        descricao="x"
        aoConfirmar={() => {}}
        aoCancelar={() => {}}
      />,
    )
    expect(screen.getByRole('button', { name: 'Confirmar' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Cancelar' })).toBeDisabled()
  })

  it('erro mostra a mensagem dentro da janela', () => {
    render(
      <JanelaDeConfirmacao
        aberta
        titulo="Excluir?"
        descricao="x"
        erro={new ErroDeApi(409, 'Conflito.', { detail: 'O cargo está em uso.' })}
        aoConfirmar={() => {}}
        aoCancelar={() => {}}
      />,
    )
    expect(screen.getByRole('alert')).toHaveTextContent('O cargo está em uso.')
  })
})
