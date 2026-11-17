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

  it('a politica fecha tudo em self e libera so o que o app usa (API, armazenamento e o Turnstile), sem unsafe-inline nem unsafe-eval', () => {
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
    expect(Object.keys(diretivas).sort()).toEqual([
      'base-uri',
      'connect-src',
      'default-src',
      'form-action',
      'frame-ancestors',
      'frame-src',
      'media-src',
      'script-src',
    ])
    expect(diretivas['default-src']).toEqual(["'self'"])
    expect(diretivas['connect-src']).toHaveLength(2)
    expect(diretivas['connect-src'][0]).toBe("'self'")
    expect(diretivas['connect-src'][1]).toMatch(/^https:\/\/\S+$/)
    // o widget do Cloudflare Turnstile (candidatura publica) pede o script e o iframe, e so desse endereco
    expect(diretivas['script-src']).toEqual(["'self'", 'https://challenges.cloudflare.com'])
    expect(diretivas['frame-src']).toEqual(['https://challenges.cloudflare.com'])
    // o video por arquivo vem do armazenamento (endereco-marcador que o dono troca no deploy, como o da API)
    expect(diretivas['media-src']).toHaveLength(2)
    expect(diretivas['media-src'][0]).toBe("'self'")
    expect(diretivas['media-src'][1]).toMatch(/^https:\/\/\S+$/)
    // estas tres nao herdam do default-src
    expect(diretivas['base-uri']).toEqual(["'self'"])
    expect(diretivas['form-action']).toEqual(["'self'"])
    expect(diretivas['frame-ancestors']).toEqual(["'none'"])
    expect(politica).not.toMatch(/unsafe-inline|unsafe-eval|data:|blob:|\*/)
  })

  it('a politica nao deixa um arquivo de /assets que nao existe virar a pagina do app, e exige https', () => {
    const config = ler() as Configuracao & { navigationFallback: { exclude?: string[] } }
    expect(config.navigationFallback.exclude).toEqual(['/assets/*', '/*.{svg,js,css,woff2}'])
    expect(config.globalHeaders['Strict-Transport-Security']).toBe(
      'max-age=31536000; includeSubDomains',
    )
  })
})
