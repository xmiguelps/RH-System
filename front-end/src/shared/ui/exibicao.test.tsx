import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ErroDeApi } from '../api/erroDeApi'
import { Avatar } from './Avatar'
import { Aviso } from './Aviso'
import { AvisoDeErro } from './AvisoDeErro'
import { Bolinha } from './Bolinha'
import { CabecalhoDePagina } from './CabecalhoDePagina'
import { Carregando } from './Carregando'
import { Cartao } from './Cartao'
import { EstadoVazio } from './EstadoVazio'
import { Esqueleto } from './Esqueleto'
import { LogoR } from './LogoR'
import { Progresso } from './Progresso'
import { Secao } from './Secao'
import { Selo } from './Selo'

describe('componentes de exibicao', () => {
  it('Selo e Cartao expoem data-tom', () => {
    render(
      <>
        <Selo tom="sucesso">Ativo</Selo>
        <Cartao tom="destaque">conteúdo</Cartao>
      </>,
    )
    expect(screen.getByText('Ativo')).toHaveAttribute('data-tom', 'sucesso')
    expect(screen.getByText('conteúdo').closest('section')).toHaveAttribute('data-tom', 'destaque')
  })

  it('Selo e Cartao tem tom padrao neutro e branco', () => {
    render(
      <>
        <Selo>Alterou</Selo>
        <Cartao>miolo</Cartao>
      </>,
    )
    expect(screen.getByText('Alterou')).toHaveAttribute('data-tom', 'neutro')
    expect(screen.getByText('miolo').closest('section')).toHaveAttribute('data-tom', 'branco')
  })

  it('Avatar mostra as iniciais e some para leitor de tela', () => {
    render(<Avatar nome="Ana Souza" />)
    const avatar = screen.getByText('AS')
    expect(avatar).toHaveAttribute('aria-hidden', 'true')
    expect(avatar).toHaveAttribute('data-tamanho', 'lista')
  })

  it('LogoR mostra o R e some para leitor de tela', () => {
    render(<LogoR tom="escuro" />)
    expect(screen.getByText('R')).toHaveAttribute('aria-hidden', 'true')
    expect(screen.getByText('R')).toHaveAttribute('data-tom', 'escuro')
  })

  it('Bolinha some com 0, null e undefined, mostra 4 e escreve 99+ acima de 99', () => {
    const { container, rerender } = render(<Bolinha valor={0} />)
    expect(container).toBeEmptyDOMElement()
    rerender(<Bolinha valor={null} />)
    expect(container).toBeEmptyDOMElement()
    rerender(<Bolinha valor={undefined} />)
    expect(container).toBeEmptyDOMElement()
    rerender(<Bolinha valor={-1} />)
    expect(container).toBeEmptyDOMElement()
    rerender(<Bolinha valor={4} />)
    expect(screen.getByText('4')).toBeInTheDocument()
    rerender(<Bolinha valor={120} />)
    expect(screen.getByText('99+')).toBeInTheDocument()
  })

  it('Aviso de erro usa role alert e os outros role status', () => {
    render(
      <>
        <Aviso tom="erro">Falhou.</Aviso>
        <Aviso tom="alerta" titulo="Atenção">
          Cuidado.
        </Aviso>
      </>,
    )
    expect(screen.getByRole('alert')).toHaveTextContent('Falhou.')
    expect(screen.getByRole('alert')).toHaveAttribute('data-tom', 'erro')
    expect(screen.getByRole('status')).toHaveTextContent('Atenção')
    expect(screen.getByRole('status')).toHaveTextContent('Cuidado.')
  })

  it('AvisoDeErro mostra a mensagem do erro e so tem Tentar de novo com a funcao', async () => {
    const usuario = userEvent.setup()
    const { rerender } = render(<AvisoDeErro erro={new ErroDeApi(500, 'x')} />)
    expect(screen.getByRole('alert')).toHaveTextContent(
      'Erro no servidor. Tente novamente em instantes.',
    )
    expect(screen.queryByRole('button', { name: 'Tentar de novo' })).not.toBeInTheDocument()
    const aoTentar = vi.fn()
    rerender(<AvisoDeErro erro={new ErroDeApi(500, 'x')} aoTentarDeNovo={aoTentar} />)
    await usuario.click(screen.getByRole('button', { name: 'Tentar de novo' }))
    expect(aoTentar).toHaveBeenCalledTimes(1)
  })

  it('EstadoVazio mostra titulo, descricao e acao', () => {
    render(
      <EstadoVazio
        titulo="Nenhum colaborador."
        descricao="Cadastre o primeiro."
        acao={<button type="button">Adicionar</button>}
      />,
    )
    expect(screen.getByText('Nenhum colaborador.')).toBeInTheDocument()
    expect(screen.getByText('Cadastre o primeiro.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Adicionar' })).toBeInTheDocument()
  })

  it('Carregando tem role status', () => {
    render(<Carregando />)
    expect(screen.getByRole('status')).toHaveTextContent('Carregando…')
  })

  it('Esqueleto some para leitor de tela', () => {
    const { container } = render(<Esqueleto className="h-4" />)
    expect(container.firstElementChild).toHaveAttribute('aria-hidden', 'true')
  })

  it('CabecalhoDePagina escreve h1, subtitulo e acoes', () => {
    render(
      <CabecalhoDePagina
        titulo="Colaboradores"
        subtitulo="Todos os colaboradores da empresa."
        acoes={<button type="button">Adicionar colaborador</button>}
      />,
    )
    expect(screen.getByRole('heading', { level: 1, name: 'Colaboradores' })).toBeInTheDocument()
    expect(screen.getByText('Todos os colaboradores da empresa.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Adicionar colaborador' })).toBeInTheDocument()
  })

  it('Secao e uma regiao nomeada pelo h2', () => {
    render(
      <Secao titulo="Precisa da sua atenção">
        <p>itens</p>
      </Secao>,
    )
    expect(screen.getByRole('region', { name: 'Precisa da sua atenção' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('Precisa da sua atenção')
  })

  it('Progresso expoe role progressbar com aria-valuenow e o nome do rotulo', () => {
    render(<Progresso valor={18} maximo={24} rotulo="Treinamentos concluídos" />)
    const barra = screen.getByRole('progressbar', { name: 'Treinamentos concluídos' })
    expect(barra).toHaveAttribute('aria-valuenow', '18')
    expect(barra).toHaveAttribute('aria-valuemax', '24')
  })
})
