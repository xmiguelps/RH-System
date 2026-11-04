// @vitest-environment node
import { describe, expect, it } from 'vitest'
import { criarClienteHttp } from '../../shared/api/clienteApi'
import { capturar } from '../../test/capturar'
import { esperarRequisicaoConforme } from '../../test/contrato'
import { adicionarModulo, atualizarModulo } from './enviarModulo'

const cliente = criarClienteHttp({ urlBase: 'https://api.exemplo.test' })
const pdf = () => new File(['%PDF'], 'canais-de-denuncia.pdf', { type: 'application/pdf' })

describe('modulos do treinamento (multipart)', () => {
  it('PDF: titulo, tipo, duracao e o arquivo, sem urlExterna nem conteudoTexto, conforme', async () => {
    const envio = capturar('adicionarModuloDeTreinamento')
    await adicionarModulo(
      6,
      {
        titulo: 'Canais de denúncia',
        tipo: 'PDF',
        duracaoMinutos: 5,
        urlExterna: 'https://x',
        conteudoTexto: 'x',
        arquivo: pdf(),
      },
      cliente,
    )
    expect(envio.ultima().cabecalhos.get('content-type')).toMatch(
      /^multipart\/form-data; boundary=/,
    )
    expect(envio.ultima().parametrosDoCaminho).toEqual({ treinamentoId: '6' })
    expect(envio.ultima().corpo).toEqual({
      titulo: 'Canais de denúncia',
      tipo: 'PDF',
      duracaoMinutos: '5',
      arquivo: 'canais-de-denuncia.pdf',
    })
    envio.conforme()
  })

  it('video: so o endereco; editar um PDF sem arquivo novo nao manda arquivo', async () => {
    const novo = capturar('adicionarModuloDeTreinamento')
    await adicionarModulo(
      6,
      { titulo: 'Introdução', tipo: 'VIDEO', urlExterna: 'https://videos.exemplo.com.br/a' },
      cliente,
    )
    expect(novo.ultima().corpo).toEqual({
      titulo: 'Introdução',
      tipo: 'VIDEO',
      urlExterna: 'https://videos.exemplo.com.br/a',
    })
    novo.conforme()
    const edicao = capturar('atualizarModuloDeTreinamento')
    await atualizarModulo(
      6,
      23,
      { titulo: 'Canais', tipo: 'PDF', duracaoMinutos: null, arquivo: null },
      cliente,
    )
    expect(edicao.ultima().parametrosDoCaminho).toEqual({ treinamentoId: '6', moduloId: '23' })
    expect(edicao.ultima().corpo).toEqual({ titulo: 'Canais', tipo: 'PDF' })
    edicao.conforme()
  })

  it('um campo de quiz ou de aula presencial seria acusado pelo contrato', () => {
    expect(() =>
      esperarRequisicaoConforme('adicionarModuloDeTreinamento', {
        corpo: { titulo: 'x', tipo: 'TEXTO', conteudoTexto: 'x', notaMinima: 7 },
      }),
    ).toThrow(/notaMinima/)
  })
})
