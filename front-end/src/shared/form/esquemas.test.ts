import { describe, expect, it } from 'vitest'
import '../zod'
import {
  campoCpf,
  campoData,
  campoEmail,
  campoHora,
  campoTelefone,
  campoTexto,
  opcional,
} from './esquemas'

describe('esquemas', () => {
  it('campoTexto apara e exige ao menos um caractere', () => {
    expect(campoTexto().parse('  Ana  ')).toBe('Ana')
    expect(campoTexto('Informe o nome.').safeParse('   ').error?.issues[0]?.message).toBe(
      'Informe o nome.',
    )
  })

  it('campoEmail recusa texto que nao e e-mail', () => {
    expect(campoEmail().parse('ana.souza@empresa.com.br')).toBe('ana.souza@empresa.com.br')
    expect(campoEmail().safeParse('ana').error?.issues[0]?.message).toBe(
      'Informe um e-mail válido.',
    )
  })

  it('campoCpf aceita com pontuacao, devolve so digitos e recusa digito errado', () => {
    expect(campoCpf().parse('846.392.710-69')).toBe('84639271069')
    expect(campoCpf().parse('84639271069')).toBe('84639271069')
    expect(campoCpf().safeParse('111.111.111-11').error?.issues[0]?.message).toBe('CPF inválido.')
  })

  it('campoTelefone, campoData, campoHora e opcional', () => {
    expect(campoTelefone().parse('(11) 98765-4410')).toBe('11987654410')
    expect(campoTelefone().safeParse('(11) 9876').error?.issues[0]?.message).toBe(
      'Telefone inválido. Use DDD e número.',
    )
    expect(campoData().parse('2026-10-07')).toBe('2026-10-07')
    expect(campoData().safeParse('2026-02-30').error?.issues[0]?.message).toBe('Data inválida.')
    expect(campoData().safeParse('07/10/2026').error?.issues[0]?.message).toBe('Data inválida.')
    expect(campoHora().parse('09:42')).toBe('09:42')
    expect(campoHora().safeParse('25:00').error?.issues[0]?.message).toBe('Hora inválida.')
    expect(opcional(campoCpf()).parse('')).toBeNull()
    expect(opcional(campoCpf()).parse('846.392.710-69')).toBe('84639271069')
    expect(opcional(campoCpf()).safeParse('123').success).toBe(false)
  })
})
