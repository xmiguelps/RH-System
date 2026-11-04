import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, renderHook } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { useBaixarPorLink } from '../../shared/arquivos/useBaixarPorLink'
import { formatarDataDoInstante } from '../../shared/formato/datas'
import { NotificacoesProvider } from '../../shared/ui/NotificacoesProvider'
import { etiquetaDoDocumento, rotuloDoTipoDeDocumento } from './rotulos'

afterEach(() => vi.restoreAllMocks())

describe('formatarDataDoInstante', () => {
  it('formatarDataDoInstante converte para Sao Paulo e nao troca o dia (Review Focus)', () => {
    expect(formatarDataDoInstante('2022-03-14T13:20:00Z')).toBe('14/03/2022')
    expect(formatarDataDoInstante('2026-10-08T02:30:00Z')).toBe('07/10/2026')
    expect(formatarDataDoInstante(null)).toBe('—')
  })
})

describe('rotulos de documento', () => {
  it.each([
    ['IDENTIDADE', 'Documento de identidade (RG ou CNH)'],
    ['CPF', 'CPF'],
    ['CARTEIRA_DE_TRABALHO', 'Carteira de trabalho digital'],
    ['COMPROVANTE_DE_RESIDENCIA', 'Comprovante de residência'],
    ['CONTRATO_ASSINADO', 'Contrato assinado'],
    ['EXAME_ADMISSIONAL', 'Exame admissional (ASO)'],
    ['OUTRO', 'Outro'],
    ['NOVO_TIPO', 'NOVO_TIPO'],
  ])('rotuloDoTipoDeDocumento(%s)', (tipo, rotulo) => {
    expect(rotuloDoTipoDeDocumento(tipo)).toBe(rotulo)
  })

  it('etiquetas Enviado e Pendente', () => {
    expect(etiquetaDoDocumento('ENVIADO')).toEqual({ texto: 'Enviado', tom: 'sucesso' })
    expect(etiquetaDoDocumento('PENDENTE')).toEqual({ texto: 'Pendente', tom: 'alerta' })
  })
})

describe('useBaixarPorLink', () => {
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={new QueryClient()}>
      <NotificacoesProvider>{children}</NotificacoesProvider>
    </QueryClientProvider>
  )

  it('abre o link temporario como anexo e baixando fica true enquanto espera', async () => {
    const cliques: string[] = []
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      cliques.push(this.href)
    })
    let liberar: (v: { data: { url: string; expiraEmUtc: string } }) => void = () => {}
    const { result } = renderHook(() => useBaixarPorLink(), { wrapper })
    let promessa!: Promise<void>
    act(() => {
      promessa = result.current.baixar(() => new Promise((r) => (liberar = r)))
    })
    expect(result.current.baixando).toBe(true)
    await act(async () => {
      liberar({
        data: { url: 'https://arquivos.exemplo.com.br/a.pdf', expiraEmUtc: '2026-10-07T12:50:00Z' },
      })
      await promessa
    })
    expect(cliques).toEqual(['https://arquivos.exemplo.com.br/a.pdf'])
    expect(result.current.baixando).toBe(false)
  })

  it('falha da API vira notificacao de erro e nao abre nada', async () => {
    const clique = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    const { result } = renderHook(() => useBaixarPorLink(), { wrapper })
    await act(() =>
      result.current.baixar(() =>
        Promise.reject(
          new ErroDeApi(422, 'x', {
            codigo: 'DOCUMENTO_SEM_ARQUIVO',
            detail: 'Este documento ainda não foi enviado.',
          }),
        ),
      ),
    )
    expect(clique).not.toHaveBeenCalled()
    expect(document.body).toHaveTextContent('Este documento ainda não foi enviado.')
  })
})
