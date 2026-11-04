import { QueryClient } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { tokenDeAcesso } from '../features/autenticacao/armazenamentoDaSessao'
import { useAuth } from '../features/autenticacao/useAuth'
import { $api } from '../shared/api/api'
import { criarQueryClient } from '../shared/api/consultas'
import { exemplos } from '../shared/mocks/gerado/handlers'
import { entrarComo } from '../test/sessao'
import { Provedores } from './Provedores'

const NOME_EMPRESA = (exemplos.obterEmpresa.corpo as { nomeEmpresa: string }).nomeEmpresa

function Empresa() {
  const q = $api.useQuery('get', '/api/eu/empresa')
  return <p>{q.data?.nomeEmpresa}</p>
}

// Como no app: a parte logada (e as consultas dela) só existe com sessão.
function Tela() {
  const { usuario, sair } = useAuth()
  if (!usuario) return <p>sem sessão</p>
  return (
    <>
      <p>{usuario.nome}</p>
      <Empresa />
      <button type="button" onClick={() => void sair()}>
        Sair
      </button>
    </>
  )
}

beforeEach(() => {
  localStorage.clear()
  tokenDeAcesso.definir(null)
})

describe('Provedores', () => {
  it('ao sair, o cache de consultas e limpo (Review Focus 2)', async () => {
    const usuario = userEvent.setup()
    const queryClient: QueryClient = criarQueryClient()
    entrarComo('RH')
    const { unmount } = render(
      <Provedores queryClient={queryClient}>
        <Tela />
      </Provedores>,
    )
    expect(await screen.findByText('Ana Souza')).toBeInTheDocument()
    expect(await screen.findByText(NOME_EMPRESA)).toBeInTheDocument()
    expect(queryClient.getQueryCache().getAll()).toHaveLength(1)
    await usuario.click(screen.getByRole('button', { name: 'Sair' }))
    expect(await screen.findByText('sem sessão')).toBeInTheDocument()
    await waitFor(() => expect(queryClient.getQueryCache().getAll()).toHaveLength(0))
    unmount()

    entrarComo('Funcionario')
    render(
      <Provedores queryClient={queryClient}>
        <Tela />
      </Provedores>,
    )
    expect(await screen.findByText('Bruno Lima')).toBeInTheDocument()
    expect(screen.queryByText('Ana Souza')).not.toBeInTheDocument()
  })
})
