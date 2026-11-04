import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { criarClienteHttp } from '../api/clienteApi'
import { ErroDeApi } from '../api/erroDeApi'
import { exemplos } from '../mocks/gerado/handlers'
import { servidor } from '../mocks/servidor'
import {
  abrirLinkTemporario,
  baixarBlob,
  baixarExportacao,
  nomeDoArquivoDaResposta,
} from './baixar'

let cliques: HTMLAnchorElement[] = []

beforeEach(() => {
  cliques = []
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
    this: HTMLAnchorElement,
  ) {
    cliques.push(this)
  })
  Object.defineProperty(URL, 'createObjectURL', {
    value: vi.fn(() => 'blob:teste/1'),
    configurable: true,
  })
  Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true })
})

afterEach(() => {
  vi.restoreAllMocks()
})

describe('baixar', () => {
  it('abrirLinkTemporario clica num link https com noopener e nao troca a pagina', () => {
    const antes = window.location.href
    abrirLinkTemporario({ url: 'https://armazenamento.exemplo.test/doc.pdf?sig=1' })
    expect(cliques).toHaveLength(1)
    expect(cliques[0]?.href).toBe('https://armazenamento.exemplo.test/doc.pdf?sig=1')
    expect(cliques[0]?.rel).toBe('noopener noreferrer')
    expect(cliques[0]?.target).toBe('_blank')
    expect(document.body.contains(cliques[0]!)).toBe(false)
    expect(window.location.href).toBe(antes)
  })

  it('abrirLinkTemporario recusa javascript: e data:', () => {
    expect(() => abrirLinkTemporario({ url: 'javascript:alert(1)' })).toThrow(
      'Link de arquivo inválido.',
    )
    expect(() => abrirLinkTemporario({ url: 'data:text/html,x' })).toThrow(
      'Link de arquivo inválido.',
    )
    expect(cliques).toHaveLength(0)
  })

  it('baixarBlob cria o link de download com o nome e libera a url', () => {
    baixarBlob(new Blob(['a;b']), 'auditoria.csv')
    expect(cliques).toHaveLength(1)
    expect(cliques[0]?.download).toBe('auditoria.csv')
    expect(cliques[0]?.href).toBe('blob:teste/1')
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:teste/1')
  })

  it('nomeDoArquivoDaResposta le filename* e filename e cai no padrao', () => {
    const resposta = (cd?: string) =>
      new Response('', { headers: cd ? { 'content-disposition': cd } : {} })
    expect(
      nomeDoArquivoDaResposta(resposta('attachment; filename="auditoria-2026-10-07.csv"'), 'x.csv'),
    ).toBe('auditoria-2026-10-07.csv')
    expect(
      nomeDoArquivoDaResposta(resposta("attachment; filename*=UTF-8''relat%C3%B3rio.csv"), 'x.csv'),
    ).toBe('relatório.csv')
    expect(nomeDoArquivoDaResposta(resposta(), 'padrao.csv')).toBe('padrao.csv')
  })

  it('baixarExportacao baixa o csv do MSW com o nome do cabecalho e nao baixa se a API devolve erro', async () => {
    const caminho = exemplos.exportarAuditoria.caminho
    servidor.use(
      http.get(
        `*${caminho}`,
        () =>
          new HttpResponse(String(exemplos.exportarAuditoria.corpo ?? 'a;b'), {
            headers: {
              'content-type': 'text/csv',
              'content-disposition': 'attachment; filename="auditoria-2026-10-07.csv"',
            },
          }),
      ),
    )
    const cliente = criarClienteHttp({ urlBase: 'https://api.exemplo.test' })
    await baixarExportacao(
      cliente.GET('/api/rh/auditoria/exportacao', { parseAs: 'blob' }),
      'auditoria.csv',
    )
    expect(cliques).toHaveLength(1)
    expect(cliques[0]?.download).toBe('auditoria-2026-10-07.csv')

    servidor.use(
      http.get(`*${caminho}`, () =>
        HttpResponse.json(
          { title: 'Erro.', status: 500 },
          { status: 500, headers: { 'content-type': 'application/problem+json' } },
        ),
      ),
    )
    await expect(
      baixarExportacao(
        cliente.GET('/api/rh/auditoria/exportacao', { parseAs: 'blob' }),
        'auditoria.csv',
      ),
    ).rejects.toBeInstanceOf(ErroDeApi)
    expect(cliques).toHaveLength(1)
  })
})
