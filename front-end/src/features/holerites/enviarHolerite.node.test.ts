// @vitest-environment node
import { http } from 'msw'
import { describe, expect, it } from 'vitest'
import { criarClienteHttp } from '../../shared/api/clienteApi'
import { servidor } from '../../shared/mocks/servidor'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar } from '../../test/capturar'
import { esperarRequisicaoConforme } from '../../test/contrato'
import { enviarHolerite, enviarHoleritesEmLote, substituirHolerite } from './enviarHolerite'

const cliente = criarClienteHttp({ urlBase: 'https://api.exemplo.test' })
const pdf = (nome = 'holerite.pdf') => new File(['%PDF'], nome, { type: 'application/pdf' })

describe('enviarHolerite (multipart)', () => {
  it('"Publicar agora": mes, ano, tipo e arquivo, sem publicarEm nem noDiaDoPagamento, conforme', async () => {
    const envio = capturar('enviarHolerite')
    await enviarHolerite(12, { mes: 10, ano: 2026, tipo: 'MENSAL', arquivo: pdf() }, cliente)
    expect(envio.ultima().cabecalhos.get('content-type')).toMatch(
      /^multipart\/form-data; boundary=/,
    )
    expect(envio.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '12' })
    expect(envio.ultima().corpo).toEqual({
      mes: '10',
      ano: '2026',
      tipo: 'MENSAL',
      arquivo: 'holerite.pdf',
    })
    envio.conforme()
  })

  it('"Agendar para o dia do pagamento" manda noDiaDoPagamento: true', async () => {
    const envio = capturar('enviarHolerite')
    await enviarHolerite(
      12,
      { mes: 10, ano: 2026, tipo: 'MENSAL', arquivo: pdf(), noDiaDoPagamento: true },
      cliente,
    )
    expect(envio.ultima().corpo).toEqual({
      mes: '10',
      ano: '2026',
      tipo: 'MENSAL',
      arquivo: 'holerite.pdf',
      noDiaDoPagamento: 'true',
    })
    envio.conforme()
  })

  it('"Agendar para uma data" manda publicarEm', async () => {
    const envio = capturar('enviarHolerite')
    await enviarHolerite(
      12,
      { mes: 10, ano: 2026, tipo: 'FERIAS', arquivo: pdf(), publicarEm: '2026-11-06' },
      cliente,
    )
    expect(envio.ultima().corpo).toMatchObject({ tipo: 'FERIAS', publicarEm: '2026-11-06' })
    expect(envio.ultima().corpo).not.toHaveProperty('noDiaDoPagamento')
    envio.conforme()
  })

  it('substituirHolerite manda o arquivo na versao, conforme', async () => {
    const envio = capturar('substituirHolerite')
    const nova = await substituirHolerite(5121, { arquivo: pdf('corrigido.pdf') }, cliente)
    expect(envio.ultima().parametrosDoCaminho).toEqual({ holeriteId: '5121' })
    expect(envio.ultima().corpo).toEqual({ arquivo: 'corrigido.pdf' })
    expect(nova.versao).toBe(2)
    envio.conforme()
  })

  it('um campo a mais seria acusado pelo contrato', () => {
    expect(() =>
      esperarRequisicaoConforme('enviarHolerite', {
        corpo: { mes: 10, ano: 2026, tipo: 'MENSAL', arquivo: 'a.pdf', avisarPorEmail: true },
      }),
    ).toThrow(/avisarPorEmail/)
  })
})

describe('enviarHoleritesEmLote (multipart)', () => {
  it('campos comuns, itens em JSON (application/json) e um "arquivos" por PDF', async () => {
    const lote = capturar('enviarHoleritesEmLote')
    let partes: { itens: unknown; tipoDosItens: string; arquivos: string[] } | null = null
    servidor.use(
      http.post('*/api/rh/holerites/lote', async ({ request }) => {
        const formulario = await request.clone().formData()
        const itens = formulario.get('itens') as File
        partes = {
          itens: JSON.parse(await itens.text()),
          tipoDosItens: itens.type,
          arquivos: formulario.getAll('arquivos').map((a) => (a as File).name),
        }
        return undefined
      }),
    )
    const resultado = await enviarHoleritesEmLote(
      {
        mes: 10,
        ano: 2026,
        tipo: 'MENSAL',
        noDiaDoPagamento: true,
        itens: [
          { funcionarioId: 12, arquivo: pdf('bruno-lima.pdf') },
          { funcionarioId: 21, arquivo: pdf('diego-farias.pdf') },
        ],
      },
      cliente,
    )
    expect(partes).toEqual({
      itens: [
        { funcionarioId: 12, nomeDoArquivo: 'bruno-lima.pdf' },
        { funcionarioId: 21, nomeDoArquivo: 'diego-farias.pdf' },
      ],
      tipoDosItens: 'application/json',
      arquivos: ['bruno-lima.pdf', 'diego-farias.pdf'],
    })
    expect(lote.ultima().corpo).toMatchObject({ mes: '10', ano: '2026', noDiaDoPagamento: 'true' })
    expect(lote.ultima().corpo).not.toHaveProperty('publicarEm')
    expect(resultado).toEqual(exemploDoContrato('enviarHoleritesEmLote'))
    lote.conforme()
  })
})
