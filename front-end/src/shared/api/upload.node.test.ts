// @vitest-environment node
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { exemplos } from '../mocks/gerado/handlers'
import { servidor } from '../mocks/servidor'
import { criarClienteHttp } from './clienteApi'

describe('upload multipart', () => {
  it('validarPlanilhaDeImportacao envia o arquivo como multipart/form-data e recebe a previa do example', async () => {
    let tipo = ''
    let nomeDoArquivo = ''
    servidor.use(
      http.post('*/api/rh/importacoes', async ({ request }) => {
        tipo = request.headers.get('content-type') ?? ''
        const formulario = await request.formData()
        const arquivo = formulario.get('arquivo')
        nomeDoArquivo = arquivo instanceof File ? arquivo.name : ''
        return HttpResponse.json(
          exemplos.validarPlanilhaDeImportacao.corpo as Record<string, unknown>,
          { status: 201 },
        )
      }),
    )
    const cliente = criarClienteHttp({ urlBase: 'https://api.exemplo.test' })
    const arquivo = new File(['x'], 'planilha.xlsx')
    const { data, response } = await cliente.POST('/api/rh/importacoes', {
      body: { arquivo: arquivo as unknown as string },
      bodySerializer: (b) => {
        const formulario = new FormData()
        formulario.append('arquivo', b.arquivo as unknown as Blob)
        return formulario
      },
    })
    expect(tipo).toMatch(/^multipart\/form-data; boundary=/)
    expect(nomeDoArquivo).toBe('planilha.xlsx')
    expect(response.status).toBe(201)
    expect(data?.nomeArquivo).toBe('novos-colaboradores-outubro.xlsx')
  })
})
