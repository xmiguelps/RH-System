import { describe, expect, it } from 'vitest'
import { validarArquivo } from './validarArquivo'

const REGRAS = { tiposAceitos: ['application/pdf', 'image/jpeg', 'image/png'], tamanhoMaximoMb: 10 }

function arquivo(bytes: number, tipo: string, nome = 'a') {
  return new File([new Uint8Array(bytes)], nome, { type: tipo })
}

describe('validarArquivo', () => {
  it.each([
    [arquivo(1024 * 1024, 'application/pdf', 'a.pdf'), null],
    [arquivo(10, 'application/x-msdownload', 'a.exe'), 'Esse tipo de arquivo não é aceito.'],
    [arquivo(11 * 1024 * 1024, 'application/pdf', 'b.pdf'), 'O arquivo passa de 10 MB.'],
    [arquivo(0, 'application/pdf', 'c.pdf'), 'O arquivo está vazio.'],
  ])('validarArquivo(%#)', (entrada, saida) => {
    expect(validarArquivo(entrada, REGRAS)).toBe(saida)
  })
})
