import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { CampoDeBusca } from './CampoDeBusca'

/** Faz o papel da página: guarda a busca já aparada, como `useEstadoDaLista` guarda na URL. */
function ComEstadoAparado({ aoMudar }: { aoMudar?: (texto: string) => void }) {
  const [busca, setBusca] = useState('')
  return (
    <>
      <CampoDeBusca
        valor={busca}
        esperaMs={10}
        aoMudar={(texto) => {
          aoMudar?.(texto)
          setBusca(texto.trim())
        }}
      />
      <button type="button" onClick={() => setBusca('')}>
        Limpar filtros
      </button>
    </>
  )
}

describe('CampoDeBusca', () => {
  it('o espaco digitado no fim nao some quando a pagina guarda a busca aparada (o usuario ainda esta escrevendo "ana maria")', async () => {
    const usuario = userEvent.setup()
    const aoMudar = vi.fn()
    render(<ComEstadoAparado aoMudar={aoMudar} />)
    const campo = screen.getByRole('searchbox', { name: 'Buscar' })
    await usuario.type(campo, 'ana ')
    await waitFor(() => expect(aoMudar).toHaveBeenCalledWith('ana '))
    expect(campo).toHaveValue('ana ')
    await usuario.type(campo, 'maria')
    expect(campo).toHaveValue('ana maria')
  })

  it('limpar o filtro por fora esvazia o campo', async () => {
    const usuario = userEvent.setup()
    render(<ComEstadoAparado />)
    const campo = screen.getByRole('searchbox', { name: 'Buscar' })
    await usuario.type(campo, 'ana')
    await waitFor(() => expect(campo).toHaveValue('ana'))
    await new Promise((resolver) => setTimeout(resolver, 30))
    await usuario.click(screen.getByRole('button', { name: 'Limpar filtros' }))
    expect(campo).toHaveValue('')
  })

  it('Enter busca na hora, sem esperar', async () => {
    const usuario = userEvent.setup()
    const aoMudar = vi.fn()
    render(<CampoDeBusca valor="" aoMudar={aoMudar} esperaMs={5000} />)
    await usuario.type(screen.getByRole('searchbox'), 'joao{Enter}')
    expect(aoMudar).toHaveBeenCalledTimes(1)
    expect(aoMudar).toHaveBeenCalledWith('joao')
  })
})
