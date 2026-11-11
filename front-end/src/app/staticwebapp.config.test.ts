// @vitest-environment node
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

type Configuracao = {
  navigationFallback: { rewrite: string }
  globalHeaders: Record<string, string>
}

function ler(): Configuracao {
  return JSON.parse(readFileSync('public/staticwebapp.config.json', 'utf8')) as Configuracao
}

describe('staticwebapp.config.json', () => {
  it('staticwebapp.config.json e JSON valido, tem navigationFallback e os cabecalhos de seguranca', () => {
    const config = ler()
    expect(config.navigationFallback.rewrite).toBe('/index.html')
    expect(config.globalHeaders).toMatchObject({
      'X-Content-Type-Options': 'nosniff',
      'X-Frame-Options': 'DENY',
      'Referrer-Policy': 'no-referrer',
      'Permissions-Policy': 'geolocation=(), microphone=(), camera=()',
    })
  })

  it('a politica fecha tudo em self e libera so a API no connect-src, sem unsafe-inline nem unsafe-eval', () => {
    const politica = ler().globalHeaders['Content-Security-Policy']
    expect(politica).toBeDefined()
    const diretivas = Object.fromEntries(
      politica
        .split(';')
        .map((parte) => parte.trim())
        .filter(Boolean)
        .map((parte) => {
          const [nome, ...valores] = parte.split(/\s+/)
          return [nome, valores]
        }),
    )
    expect(Object.keys(diretivas).sort()).toEqual(['connect-src', 'default-src'])
    expect(diretivas['default-src']).toEqual(["'self'"])
    expect(diretivas['connect-src']).toHaveLength(2)
    expect(diretivas['connect-src'][0]).toBe("'self'")
    expect(diretivas['connect-src'][1]).toMatch(/^https:\/\/\S+$/)
    expect(politica).not.toMatch(/unsafe-inline|unsafe-eval|data:|blob:|\*/)
  })
})
