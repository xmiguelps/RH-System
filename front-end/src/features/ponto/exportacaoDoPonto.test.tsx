import { screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { capturar, problema } from '../../test/capturar'
import { renderizarTela } from '../../test/telas'

const ROTA = '/rh/ponto-e-feriados?ano=2026&mes=9'
beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
  Object.defineProperty(URL, 'createObjectURL', {
    value: vi.fn(() => 'blob:x'),
    configurable: true,
  })
  Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true })
})
afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
})

async function abrirModal() {
  const tela = renderizarTela(ROTA)
  await screen.findByRole('heading', { level: 1, name: 'Ponto e feriados', hidden: true })
  await tela.user.click(screen.getByRole('button', { name: 'Exportar para a folha' }))
  const janela = await screen.findByRole('dialog', { name: 'Exportar para a folha' })
  return { ...tela, janela }
}
const csv = { status: 200, corpo: 'a;b', cabecalhos: { 'content-type': 'text/csv' } }

describe('Exportar o ponto para a folha (janela, contrato 0.3.0)', { timeout: 20000 }, () => {
  it('abre com o periodo, as 12 colunas de sempre marcadas e na ordem de sempre; baixar sem mexer em nada nao manda os parametros novos', async () => {
    const folha = capturar('exportarPontoParaFolha', csv)
    const { user, janela } = await abrirModal()
    expect(within(janela).getByText('Setembro de 2026')).toBeInTheDocument()
    const caixas = within(janela).getAllByRole('checkbox')
    expect(caixas).toHaveLength(12)
    for (const caixa of caixas) expect(caixa).toBeChecked()
    expect(within(janela).getByRole('radio', { name: /Minutos/ })).toBeChecked()
    expect(within(janela).getByRole('radio', { name: /Ponto e vírgula/ })).toBeChecked()
    await user.click(within(janela).getByRole('button', { name: 'Baixar arquivo' }))
    await waitFor(() => expect(folha.quantas()).toBe(1))
    expect(folha.ultima().consulta.get('ano')).toBe('2026')
    expect(folha.ultima().consulta.get('mes')).toBe('9')
    for (const nome of ['colunas', 'formatoHoras', 'separador'])
      expect(folha.ultima().consulta.has(nome)).toBe(false)
    folha.conforme()
    await waitFor(() =>
      expect(screen.queryByRole('dialog', { name: 'Exportar para a folha' })).toBeNull(),
    )
  })

  it('desmarcar colunas e mover uma de lugar manda so as marcadas, na ordem escolhida, pelos codigos do contrato', async () => {
    const folha = capturar('exportarPontoParaFolha', csv)
    const { user, janela } = await abrirModal()
    for (const rotulo of [
      'Matrícula',
      'Setor',
      'Cargo',
      'Dias trabalhados',
      'Horas previstas',
      'Atrasos',
      'Faltas',
      'Faltas compensadas',
      'Débito do banco de horas no mês',
    ])
      await user.click(within(janela).getByRole('checkbox', { name: rotulo }))
    // sobram: Nome, Horas trabalhadas, Crédito do banco de horas no mês
    // a lista tem as 12 colunas (marcadas ou não): são 10 passos até o topo
    for (let i = 0; i < 10; i++)
      await user.click(
        within(janela).getByRole('button', {
          name: 'Subir a coluna Crédito do banco de horas no mês',
        }),
      )
    expect(
      within(janela).getByRole('button', {
        name: 'Subir a coluna Crédito do banco de horas no mês',
      }),
    ).toBeDisabled()
    await user.click(within(janela).getByRole('button', { name: 'Baixar arquivo' }))
    await waitFor(() => expect(folha.quantas()).toBe(1))
    expect(folha.ultima().consulta.get('colunas')).toBe(
      'banco_credito_do_mes,nome,horas_trabalhadas',
    )
  })

  it('formato das horas e separador vao como o contrato pede', async () => {
    const folha = capturar('exportarPontoParaFolha', csv)
    const { user, janela } = await abrirModal()
    await user.click(within(janela).getByRole('radio', { name: /Horas decimais/ }))
    await user.click(within(janela).getByRole('radio', { name: /Vírgula/ }))
    await user.click(within(janela).getByRole('button', { name: 'Baixar arquivo' }))
    await waitFor(() => expect(folha.quantas()).toBe(1))
    expect(folha.ultima().consulta.get('formatoHoras')).toBe('HORAS_DECIMAIS')
    expect(folha.ultima().consulta.get('separador')).toBe('VIRGULA')
    expect(folha.ultima().consulta.has('colunas')).toBe(false)
  })

  it('"Horas (hh:mm)" manda HH_MM', async () => {
    const folha = capturar('exportarPontoParaFolha', csv)
    const { user, janela } = await abrirModal()
    await user.click(within(janela).getByRole('radio', { name: /Horas e minutos/ }))
    await user.click(within(janela).getByRole('button', { name: 'Baixar arquivo' }))
    await waitFor(() => expect(folha.quantas()).toBe(1))
    expect(folha.ultima().consulta.get('formatoHoras')).toBe('HH_MM')
  })

  it('sem nenhuma coluna marcada o botao fica desligado e a janela diz o motivo', async () => {
    const { user, janela } = await abrirModal()
    for (const caixa of within(janela).getAllByRole('checkbox')) await user.click(caixa)
    expect(within(janela).getByText('Escolha ao menos uma coluna.')).toBeInTheDocument()
    expect(within(janela).getByRole('button', { name: 'Baixar arquivo' })).toBeDisabled()
  })

  it('400 em colunas aparece no bloco das colunas e a janela continua aberta', async () => {
    capturar(
      'exportarPontoParaFolha',
      problema(400, {
        title: 'Confira os dados informados.',
        status: 400,
        errors: { colunas: ['Coluna desconhecida: xpto.'] },
      }),
    )
    const { user, janela } = await abrirModal()
    await user.click(within(janela).getByRole('button', { name: 'Baixar arquivo' }))
    expect(await within(janela).findByText('Coluna desconhecida: xpto.')).toBeInTheDocument()
    expect(screen.getByRole('dialog', { name: 'Exportar para a folha' })).toBeInTheDocument()
  })

  it('erro do servidor aparece dentro da janela (nao so numa notificacao que some)', async () => {
    capturar('exportarPontoParaFolha', problema(500, { title: 'Erro interno.', status: 500 }))
    const { user, janela } = await abrirModal()
    await user.click(within(janela).getByRole('button', { name: 'Baixar arquivo' }))
    expect(
      await within(janela).findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
  })
})
