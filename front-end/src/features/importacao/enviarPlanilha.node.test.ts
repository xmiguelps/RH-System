// @vitest-environment node
import { describe, expect, it } from 'vitest'
import { criarClienteHttp } from '../../shared/api/clienteApi'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { esperarRequisicaoConforme } from '../../test/contrato'
import { validarPlanilha } from './enviarPlanilha'
import { TIPO_XLSX } from './rotulosDaImportacao'

const cliente = criarClienteHttp({ urlBase: 'https://api.exemplo.test' })
const planilha = () => new File(['PK'], 'novos-colaboradores.xlsx', { type: TIPO_XLSX })

describe('validarPlanilha', () => {
  it('envia uma requisicao multipart so com o campo arquivo, conforme, e devolve a previa', async () => {
    const envio = capturar('validarPlanilhaDeImportacao', {
      status: 201,
      corpo: exemploDoContrato('validarPlanilhaDeImportacao', 201, 'pronta'),
    })
    const previa = await validarPlanilha(planilha(), cliente)
    expect(envio.quantas()).toBe(1)
    expect(envio.ultima().cabecalhos.get('content-type')).toMatch(
      /^multipart\/form-data; boundary=/,
    )
    expect(envio.ultima().corpo).toEqual({ arquivo: 'novos-colaboradores.xlsx' })
    envio.conforme()
    expect(previa.importacaoId).toBe(16)
    expect(previa.podeConfirmar).toBe(true)
  })

  it('o 422 do arquivo chega como ErroDeApi com o codigo e o detail do contrato', async () => {
    const corpo = exemploDoContrato<{ detail: string } & Record<string, unknown>>(
      'validarPlanilhaDeImportacao',
      422,
      'naoEExcel',
    )
    capturar('validarPlanilhaDeImportacao', problema(422, corpo))
    const erro = await validarPlanilha(planilha(), cliente).catch((e: unknown) => e)
    expect(erro).toBeInstanceOf(ErroDeApi)
    expect((erro as ErroDeApi).codigo).toBe('ARQUIVO_INVALIDO')
    expect((erro as ErroDeApi).detail).toBe(corpo.detail)
  })

  it('uma chave a mais no multipart seria acusada pelo contrato', () => {
    expect(() =>
      esperarRequisicaoConforme('validarPlanilhaDeImportacao', {
        corpo: { arquivo: 'a.xlsx', sobrescrever: 'sim' },
      }),
    ).toThrow(/sobrescrever/)
  })
})
