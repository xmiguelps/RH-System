import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { exemplos } from '../../shared/mocks/gerado/handlers'
import { capturar } from '../../test/capturar'
import { escolherOpcao, preencherData } from '../../test/formulario'
import { renderizarTela } from '../../test/telas'
import { adicionarDocumento } from './enviarDocumento'

vi.mock('./enviarDocumento', () => ({ adicionarDocumento: vi.fn(), enviarMeuArquivo: vi.fn() }))

afterEach(() => vi.clearAllMocks())

const pdf = () => new File(['%PDF'], 'comprovante.pdf', { type: 'application/pdf' })

async function abrirModal() {
  const tela = renderizarTela('/rh/colaboradores/12?aba=documentos')
  await tela.user.click(await screen.findByRole('button', { name: 'Adicionar documento' }))
  const modal = await screen.findByRole('dialog', { name: 'Adicionar documento de Bruno' })
  return { ...tela, modal }
}

describe('Modal Adicionar documento (tela 10)', { timeout: 15000 }, () => {
  it('mostra os dois cartoes e a nota com o primeiro nome, sem prometer aviso', async () => {
    const { modal } = await abrirModal()
    expect(within(modal).getByRole('radio', { name: /Anexar agora/ })).toBeInTheDocument()
    expect(
      within(modal).getByRole('radio', { name: /Pedir para a pessoa enviar/ }),
    ).toBeInTheDocument()
    expect(within(modal).getByText('Ela envia pelo portal')).toBeInTheDocument()
    expect(
      within(modal).getByText('Só o RH e Bruno conseguem ver os documentos da ficha.'),
    ).toBeInTheDocument()
    expect(within(modal).queryByText(/Ela recebe um aviso/)).toBeNull()
  })

  it('ANEXAR_AGORA e o padrao e mostra a area de envio; PEDIR_PARA_ENVIAR a esconde', async () => {
    const { user, modal } = await abrirModal()
    expect(within(modal).getByRole('radio', { name: /Anexar agora/ })).toHaveAttribute(
      'aria-checked',
      'true',
    )
    expect(within(modal).getByText('Arraste o arquivo aqui')).toBeInTheDocument()
    expect(within(modal).getByText('PDF, JPG ou PNG, até 10 MB')).toBeInTheDocument()
    await user.click(within(modal).getByText('Pedir para a pessoa enviar'))
    expect(within(modal).queryByText('Arraste o arquivo aqui')).toBeNull()
  })

  it('com tipo Outro a dica diz Diga qual e o documento', async () => {
    const { user, modal } = await abrirModal()
    await escolherOpcao(user, 'Tipo de documento', 'Outro')
    expect(within(modal).getByText('Diga qual é o documento')).toBeInTheDocument()
  })

  it('arquivo invalido nao e aceito; anexar sem arquivo mostra o erro do campo', async () => {
    const { modal } = await abrirModal()
    const semAccept = userEvent.setup({ applyAccept: false })
    await semAccept.upload(
      within(modal).getByLabelText('Escolher arquivo'),
      new File(['x'], 'a.exe', { type: 'application/x-msdownload' }),
    )
    expect(within(modal).getByText('Esse tipo de arquivo não é aceito.')).toBeInTheDocument()
    await escolherOpcao(semAccept, 'Tipo de documento', 'CPF')
    await semAccept.click(screen.getByRole('button', { name: 'Salvar documento' }))
    expect(await within(modal).findByText('Escolha o arquivo.')).toBeInTheDocument()
    expect(adicionarDocumento).not.toHaveBeenCalled()
  })

  it('salvar chama adicionarDocumento com os campos certos, fecha o modal e refaz a lista', async () => {
    vi.mocked(adicionarDocumento).mockResolvedValue(
      exemplos.adicionarDocumentoDoFuncionario.corpo as never,
    )
    const lista = capturar('listarDocumentosDoFuncionario')
    const { user, modal } = await abrirModal()
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(0))
    const antes = lista.quantas()
    await escolherOpcao(user, 'Tipo de documento', 'Comprovante de residência')
    preencherData('Validade (opcional)', '2027-10-01')
    await user.type(within(modal).getByLabelText('Descrição (opcional)'), 'Comprovante atualizado')
    const arquivo = pdf()
    await user.upload(within(modal).getByLabelText('Escolher arquivo'), arquivo)
    await user.click(screen.getByRole('button', { name: 'Salvar documento' }))
    await waitFor(() => expect(adicionarDocumento).toHaveBeenCalledTimes(1))
    expect(vi.mocked(adicionarDocumento).mock.calls[0]?.slice(0, 2)).toEqual([
      12,
      {
        modo: 'ANEXAR_AGORA',
        tipo: 'COMPROVANTE_DE_RESIDENCIA',
        descricao: 'Comprovante atualizado',
        dataValidade: '2027-10-01',
        arquivo,
      },
    ])
    await waitFor(() =>
      expect(screen.queryByRole('dialog', { name: 'Adicionar documento de Bruno' })).toBeNull(),
    )
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(antes))
  })

  it('400 em arquivo aparece junto da area de envio; enviando mostra Enviando… e clique duplo envia uma vez', async () => {
    let falhar: (e: unknown) => void = () => {}
    vi.mocked(adicionarDocumento).mockImplementation(
      () => new Promise((_, rejeitar) => (falhar = rejeitar)),
    )
    const { user, modal } = await abrirModal()
    await escolherOpcao(user, 'Tipo de documento', 'CPF')
    await user.upload(within(modal).getByLabelText('Escolher arquivo'), pdf())
    await user.dblClick(screen.getByRole('button', { name: 'Salvar documento' }))
    expect(await within(modal).findByText('Enviando…')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Salvar documento' })).toBeDisabled()
    expect(adicionarDocumento).toHaveBeenCalledTimes(1)
    falhar(new ErroDeApi(400, 'x', { errors: { arquivo: ['O arquivo não é um PDF de verdade.'] } }))
    expect(await within(modal).findByText('O arquivo não é um PDF de verdade.')).toBeInTheDocument()
  })
})
