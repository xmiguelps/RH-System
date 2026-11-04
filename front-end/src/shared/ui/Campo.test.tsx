import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { AreaDeTexto } from './AreaDeTexto'
import { Campo } from './Campo'
import { ComboDeBusca } from './ComboDeBusca'
import { Entrada } from './Entrada'
import { Selecao } from './Selecao'

describe('Campo', () => {
  it('Campo liga o rotulo ao Entrada, a dica ao campo e o erro como descricao', () => {
    render(
      <Campo rotulo="Nome completo" dica="Como no documento." erro="Obrigatório.">
        <Entrada />
      </Campo>,
    )
    const campo = screen.getByLabelText('Nome completo')
    expect(campo).toHaveAccessibleDescription(/Obrigatório/)
    expect(campo).toHaveAccessibleDescription(/Como no documento/)
    expect(campo).toBeInvalid()
  })

  it('Campo sem erro nao esta invalido e nao mostra mensagem', () => {
    render(
      <Campo rotulo="Nome completo">
        <Entrada />
      </Campo>,
    )
    expect(screen.getByLabelText('Nome completo')).toBeValid()
    expect(screen.queryByText('Obrigatório.')).not.toBeInTheDocument()
  })

  it('Campo nomeia pelo rotulo o Selecao, o ComboDeBusca e a AreaDeTexto', () => {
    render(
      <>
        <Campo rotulo="Cargo">
          <Selecao
            rotulo="Cargo"
            opcoes={[{ valor: 'analista', rotulo: 'Analista' }]}
            valor={null}
            aoMudar={() => {}}
          />
        </Campo>
        <Campo rotulo="Setor">
          <ComboDeBusca
            rotulo="Setor"
            opcoes={[{ valor: '1', rotulo: 'Logística' }]}
            valor={null}
            aoMudar={() => {}}
          />
        </Campo>
        <Campo rotulo="Descrição">
          <AreaDeTexto />
        </Campo>
      </>,
    )
    expect(screen.getByRole('combobox', { name: 'Cargo' })).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: 'Setor' })).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: 'Descrição' })).toBeInTheDocument()
  })
})
