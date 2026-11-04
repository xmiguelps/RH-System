// @vitest-environment node
import { describe, expect, it } from 'vitest'
import { criarClienteHttp } from '../../shared/api/clienteApi'
import { capturar } from '../../test/capturar'
import { esperarRequisicaoConforme } from '../../test/contrato'
import { enviarAtestado } from './enviarAtestado'

const cliente = criarClienteHttp({ urlBase: 'https://api.exemplo.test' })
const pdf = () => new File(['%PDF'], 'atestado.pdf', { type: 'application/pdf' })

describe('enviarAtestado', () => {
  it('dias inteiros: multipart com as datas e o arquivo, sem hora, conforme', async () => {
    const envio = capturar('enviarAtestado')
    await enviarAtestado(
      { dataInicio: '2026-10-07', dataFim: '2026-10-08', arquivo: pdf() },
      cliente,
    )
    expect(envio.ultima().cabecalhos.get('content-type')).toMatch(
      /^multipart\/form-data; boundary=/,
    )
    expect(envio.ultima().corpo).toEqual({
      dataInicio: '2026-10-07',
      dataFim: '2026-10-08',
      arquivo: 'atestado.pdf',
    })
    envio.conforme()
  })

  it('algumas horas: leva horaInicio e horaFim', async () => {
    const envio = capturar('enviarAtestado')
    await enviarAtestado(
      {
        dataInicio: '2026-09-25',
        dataFim: '2026-09-25',
        horaInicio: '13:00',
        horaFim: '17:48',
        arquivo: pdf(),
      },
      cliente,
    )
    expect(envio.ultima().corpo).toEqual({
      dataInicio: '2026-09-25',
      dataFim: '2026-09-25',
      horaInicio: '13:00',
      horaFim: '17:48',
      arquivo: 'atestado.pdf',
    })
    envio.conforme()
  })

  it('um campo de diagnostico seria acusado pelo contrato', () => {
    expect(() =>
      esperarRequisicaoConforme('enviarAtestado', {
        corpo: { dataInicio: '2026-10-07', dataFim: '2026-10-07', arquivo: 'a.pdf', cid: 'X' },
      }),
    ).toThrow(/cid/)
  })
})
