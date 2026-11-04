import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { SeletorDeArquivo } from './SeletorDeArquivo'

const TIPOS = ['application/pdf']

describe('SeletorDeArquivo', () => {
  it('SeletorDeArquivo entrega um arquivo valido e mostra o nome', async () => {
    const usuario = userEvent.setup()
    const aoMudar = vi.fn()
    function Teste() {
      const [arquivo, setArquivo] = useState<File | null>(null)
      return (
        <SeletorDeArquivo
          arquivo={arquivo}
          aoMudar={(a) => {
            aoMudar(a)
            setArquivo(a)
          }}
          tiposAceitos={TIPOS}
          tamanhoMaximoMb={10}
          textoDeAjuda="PDF até 10 MB"
        />
      )
    }
    render(<Teste />)
    expect(screen.getByText('Arraste o arquivo aqui')).toBeInTheDocument()
    expect(screen.getByText('PDF até 10 MB')).toBeInTheDocument()
    const pdf = new File(['%PDF'], 'contrato.pdf', { type: 'application/pdf' })
    await usuario.upload(screen.getByLabelText('Escolher arquivo'), pdf)
    expect(aoMudar).toHaveBeenCalledWith(pdf)
    expect(screen.getByText('contrato.pdf')).toBeInTheDocument()
  })

  it('SeletorDeArquivo recusa tipo invalido sem chamar aoMudar e mostra a mensagem', async () => {
    const usuario = userEvent.setup({ applyAccept: false })
    const aoMudar = vi.fn()
    render(
      <SeletorDeArquivo
        arquivo={null}
        aoMudar={aoMudar}
        tiposAceitos={TIPOS}
        tamanhoMaximoMb={10}
      />,
    )
    await usuario.upload(
      screen.getByLabelText('Escolher arquivo'),
      new File(['x'], 'a.exe', { type: 'application/x-msdownload' }),
    )
    expect(aoMudar).not.toHaveBeenCalled()
    expect(screen.getByRole('alert')).toHaveTextContent('Esse tipo de arquivo não é aceito.')
  })

  it('SeletorDeArquivo aceita soltar o arquivo na area', () => {
    const aoMudar = vi.fn()
    render(
      <SeletorDeArquivo
        arquivo={null}
        aoMudar={aoMudar}
        tiposAceitos={TIPOS}
        tamanhoMaximoMb={10}
      />,
    )
    const pdf = new File(['%PDF'], 'solto.pdf', { type: 'application/pdf' })
    fireEvent.drop(screen.getByText('Arraste o arquivo aqui'), { dataTransfer: { files: [pdf] } })
    expect(aoMudar).toHaveBeenCalledWith(pdf)
  })

  it('SeletorDeArquivo remove o arquivo escolhido (aoMudar(null))', async () => {
    const usuario = userEvent.setup()
    const aoMudar = vi.fn()
    const pdf = new File(['%PDF'], 'contrato.pdf', { type: 'application/pdf' })
    render(
      <SeletorDeArquivo
        arquivo={pdf}
        aoMudar={aoMudar}
        tiposAceitos={TIPOS}
        tamanhoMaximoMb={10}
      />,
    )
    await usuario.click(screen.getByRole('button', { name: 'Remover arquivo' }))
    expect(aoMudar).toHaveBeenCalledWith(null)
  })

  it('SeletorDeArquivo mostra o erro vindo de fora', () => {
    render(
      <SeletorDeArquivo
        arquivo={null}
        aoMudar={() => {}}
        tiposAceitos={TIPOS}
        tamanhoMaximoMb={10}
        erro="Escolha um arquivo."
      />,
    )
    expect(screen.getByRole('alert')).toHaveTextContent('Escolha um arquivo.')
  })
})
