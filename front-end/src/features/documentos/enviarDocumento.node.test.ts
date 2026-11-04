// @vitest-environment node
import { describe, expect, it } from 'vitest'
import { criarClienteHttp } from '../../shared/api/clienteApi'
import { esperarRequisicaoConforme } from '../../test/contrato'
import { capturar } from '../../test/capturar'
import { adicionarDocumento, enviarMeuArquivo } from './enviarDocumento'

const cliente = criarClienteHttp({ urlBase: 'https://api.exemplo.test' })
const pdf = () => new File(['%PDF'], 'comprovante.pdf', { type: 'application/pdf' })

describe('enviarDocumento', () => {
  it('adicionarDocumento envia multipart com modo, tipo, descricao, validade e arquivo, e a requisicao e conforme', async () => {
    const envio = capturar('adicionarDocumentoDoFuncionario')
    const documento = await adicionarDocumento(
      12,
      {
        modo: 'ANEXAR_AGORA',
        tipo: 'COMPROVANTE_DE_RESIDENCIA',
        descricao: 'Comprovante de residência atualizado',
        dataValidade: '2027-10-01',
        arquivo: pdf(),
      },
      cliente,
    )
    expect(envio.ultima().cabecalhos.get('content-type')).toMatch(
      /^multipart\/form-data; boundary=/,
    )
    expect(envio.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '12' })
    expect(envio.ultima().corpo).toEqual({
      modo: 'ANEXAR_AGORA',
      tipo: 'COMPROVANTE_DE_RESIDENCIA',
      descricao: 'Comprovante de residência atualizado',
      dataValidade: '2027-10-01',
      arquivo: 'comprovante.pdf',
    })
    envio.conforme()
    expect(documento.id).toBe(70)
  })

  it('PEDIR_PARA_ENVIAR nao manda arquivo', async () => {
    const envio = capturar('adicionarDocumentoDoFuncionario')
    await adicionarDocumento(
      12,
      { modo: 'PEDIR_PARA_ENVIAR', tipo: 'CPF', arquivo: pdf() },
      cliente,
    )
    expect(envio.ultima().corpo).toEqual({ modo: 'PEDIR_PARA_ENVIAR', tipo: 'CPF' })
  })

  it('o multipart tem as mesmas chaves do contrato e uma chave a mais seria acusada', () => {
    expect(() =>
      esperarRequisicaoConforme('adicionarDocumentoDoFuncionario', {
        corpo: { modo: 'ANEXAR_AGORA', tipo: 'CPF', arquivo: 'a.pdf', lembrar: 'sim' },
      }),
    ).toThrow(/lembrar/)
    expect(() =>
      esperarRequisicaoConforme('adicionarDocumentoDoFuncionario', {
        corpo: { modo: 'ANEXAR_AGORA' },
      }),
    ).toThrow(/tipo/)
  })

  it('enviarMeuArquivo envia so o campo arquivo e a requisicao e conforme', async () => {
    const envio = capturar('enviarMeuDocumento')
    await enviarMeuArquivo(64, pdf(), cliente)
    expect(envio.ultima().parametrosDoCaminho).toEqual({ documentoId: '64' })
    expect(envio.ultima().corpo).toEqual({ arquivo: 'comprovante.pdf' })
    envio.conforme()
  })
})
