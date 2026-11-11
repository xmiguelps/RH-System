import { describe, expect, it } from 'vitest'
import { z } from 'zod'
import './zod'

function primeiraMensagem(resultado: { success: boolean; error?: z.ZodError }): string | undefined {
  return resultado.error?.issues[0]?.message
}

describe('configuracao do zod', () => {
  it('z.config jitless fica ligado (sem isso o zod gera eval e a politica do site barra)', () => {
    expect(z.config().jitless).toBe(true)
    const esquema = z.object({ nome: z.string().min(1) })
    expect(esquema.safeParse({ nome: 'Ana Souza' }).success).toBe(true)
    expect(esquema.safeParse({ nome: '' }).success).toBe(false)
  })

  it('mensagens globais em portugues e a do esquema vence', () => {
    expect(primeiraMensagem(z.string().min(1).safeParse(''))).toBe('Campo obrigatório.')
    expect(primeiraMensagem(z.string().min(3).safeParse('a'))).toBe('Use no mínimo 3 caracteres.')
    expect(primeiraMensagem(z.string().max(2).safeParse('abc'))).toBe('Use no máximo 2 caracteres.')
    expect(primeiraMensagem(z.string().safeParse(undefined))).toBe('Campo obrigatório.')
    expect(primeiraMensagem(z.email().safeParse('x'))).toBe('Informe um e-mail válido.')
    expect(primeiraMensagem(z.string().min(1, 'Informe o nome.').safeParse(''))).toBe(
      'Informe o nome.',
    )
    expect(z.config().jitless).toBe(true)
  })
})
