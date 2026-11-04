// @vitest-environment node
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { servidor } from '../mocks/servidor'
import { multipart } from './multipart'

describe('multipart', () => {
  it('multipart envia multipart/form-data com o arquivo e os outros campos e omite null', async () => {
    let tipo = ''
    let campos: string[] = []
    let nome = ''
    let tipoDoc: FormDataEntryValue | null = null
    servidor.use(
      http.post('https://api.exemplo.test/envio', async ({ request }) => {
        tipo = request.headers.get('content-type') ?? ''
        const form = await request.formData()
        campos = [...form.keys()]
        const arquivo = form.get('arquivo')
        nome = arquivo instanceof File ? arquivo.name : ''
        tipoDoc = form.get('tipo')
        return HttpResponse.json({ ok: true })
      }),
    )
    const { body, bodySerializer } = multipart<Record<string, unknown>>({
      arquivo: new File(['x'], 'planilha.xlsx'),
      tipo: 'ASO',
      observacao: null,
      outro: undefined,
      ativo: true,
    })
    await fetch('https://api.exemplo.test/envio', { method: 'POST', body: bodySerializer(body) })
    expect(tipo).toMatch(/^multipart\/form-data; boundary=/)
    expect(nome).toBe('planilha.xlsx')
    expect(tipoDoc).toBe('ASO')
    expect(campos).toEqual(['arquivo', 'tipo', 'ativo'])
  })
})
