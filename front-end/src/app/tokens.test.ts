// @vitest-environment node
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const css = readFileSync('src/index.css', 'utf8')

describe('tokens de design', () => {
  it('index.css tem o @theme static com os tokens-ancora de 04-visual', () => {
    const ancoras: [string, string][] = [
      ['--color-pagina', '#fffffa'],
      ['--color-superficie', '#ffffff'],
      ['--color-superficie-suave', '#f2f8f7'],
      ['--color-superficie-destaque', '#e6f4f3'],
      ['--color-borda', '#d9e4e2'],
      ['--color-borda-campo', '#c9d8d6'],
      ['--color-texto', '#0e3236'],
      ['--color-texto-suave', '#4e6466'],
      ['--color-primaria', '#0d5c63'],
      ['--color-primaria-media', '#247b7b'],
      ['--color-primaria-clara', '#78cdd7'],
      ['--color-erro-texto', '#a8322a'],
      ['--spacing-menu', '232px'],
      ['--radius-cartao', '14px'],
      ['--text-t1', '36px'],
    ]
    for (const [nome, valor] of ancoras) {
      expect(css, nome).toMatch(new RegExp(`${nome}:\\s*${valor};`))
    }
  })

  it('todo --color-* e um hexadecimal de 6 digitos ou rgb(), e o arquivo usa @theme static', () => {
    expect(css).toMatch(/@theme static/)
    const linhas = css.split('\n').filter((linha) => /^\s*--color-/.test(linha))
    expect(linhas.length).toBeGreaterThan(20)
    for (const linha of linhas) {
      expect(linha).toMatch(/^\s*--color-[a-z-]+:\s*(#[0-9a-f]{6}|rgb\([^)]*\));\s*$/)
    }
  })
})
