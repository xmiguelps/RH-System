import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { CaixaDeSelecao } from './CaixaDeSelecao'
import { Interruptor } from './Interruptor'
import { OpcoesEmCartao } from './OpcoesEmCartao'

describe('CaixaDeSelecao', () => {
  it('CaixaDeSelecao marca ao clicar no rotulo e chama aoMudar(true)', async () => {
    const usuario = userEvent.setup()
    const aoMudar = vi.fn()
    render(
      <CaixaDeSelecao
        marcada={false}
        aoMudar={aoMudar}
        rotulo="Vale-refeição"
        descricao="R$ 35,00 por dia"
      />,
    )
    await usuario.click(screen.getByText('Vale-refeição'))
    expect(aoMudar).toHaveBeenCalledWith(true)
    expect(screen.getByRole('checkbox', { name: /Vale-refeição/ })).toHaveAttribute(
      'aria-checked',
      'false',
    )
    expect(screen.getByText('R$ 35,00 por dia')).toBeInTheDocument()
  })
})

describe('Interruptor', () => {
  it('Interruptor liga e desliga (role switch)', async () => {
    const usuario = userEvent.setup()
    function Teste() {
      const [ligado, setLigado] = useState(false)
      return <Interruptor ligado={ligado} aoMudar={setLigado} rotulo="Gestor ausente" />
    }
    render(<Teste />)
    const chave = screen.getByRole('switch', { name: 'Gestor ausente' })
    expect(chave).toHaveAttribute('aria-checked', 'false')
    await usuario.click(chave)
    expect(chave).toHaveAttribute('aria-checked', 'true')
    await usuario.click(chave)
    expect(chave).toHaveAttribute('aria-checked', 'false')
  })
})

describe('OpcoesEmCartao', () => {
  const OPCOES = [
    { valor: 'contrato', titulo: 'Contrato', descricao: 'Documento assinado.' },
    { valor: 'atestado', titulo: 'Atestado', descricao: 'Atestado médico.' },
    { valor: 'outro', titulo: 'Outro' },
  ] as const

  it('OpcoesEmCartao e um radiogroup nomeado, escolhe por clique e por seta do teclado', async () => {
    const usuario = userEvent.setup()
    const aoMudar = vi.fn()
    function Teste() {
      const [valor, setValor] = useState<string | null>('contrato')
      return (
        <OpcoesEmCartao
          rotulo="Tipo de documento"
          opcoes={OPCOES}
          valor={valor}
          aoMudar={(v) => {
            aoMudar(v)
            setValor(v)
          }}
        />
      )
    }
    render(<Teste />)
    expect(screen.getByRole('radiogroup', { name: 'Tipo de documento' })).toBeInTheDocument()
    const atestado = screen.getByRole('radio', { name: /Atestado/ })
    await usuario.click(atestado)
    expect(aoMudar).toHaveBeenLastCalledWith('atestado')
    expect(atestado).toHaveAttribute('aria-checked', 'true')
    await usuario.keyboard('{ArrowDown}')
    expect(aoMudar).toHaveBeenLastCalledWith('outro')
    expect(screen.getByRole('radio', { name: /Outro/ })).toHaveAttribute('aria-checked', 'true')
  })

  it('OpcoesEmCartao mostra titulo e descricao de cada cartao', () => {
    render(<OpcoesEmCartao rotulo="Tipo" opcoes={OPCOES} valor={null} aoMudar={() => {}} />)
    expect(screen.getByText('Contrato')).toBeInTheDocument()
    expect(screen.getByText('Documento assinado.')).toBeInTheDocument()
    expect(screen.getByText('Atestado médico.')).toBeInTheDocument()
  })
})
