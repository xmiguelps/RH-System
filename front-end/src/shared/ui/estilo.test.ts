import { describe, expect, it } from 'vitest'
import { cn } from './estilo'

describe('cn', () => {
  it('cn junta classes e descarta as falsas', () => {
    const falso = false as boolean
    expect(cn('a', falso && 'b', undefined, 'c')).toBe('a c')
  })

  it('cn mantem tamanho de letra e cor do texto, que nao conflitam', () => {
    expect(cn('text-corpo', 'text-texto')).toBe('text-corpo text-texto')
    expect(cn('text-corpo', 'text-t2')).toBe('text-t2')
  })

  it('cn resolve conflito de raio e de fundo dos tokens', () => {
    expect(cn('rounded-botao', 'rounded-campo')).toBe('rounded-campo')
    expect(cn('bg-superficie', 'bg-primaria')).toBe('bg-primaria')
  })
})
