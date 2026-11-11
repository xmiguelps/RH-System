// @vitest-environment node
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'

// Cada padrão é algo que a política do site barra ou que injeta HTML cru.
const PADROES: [string, RegExp][] = [
  ['<style', /<style[\s>]/i],
  ['style="', /\bstyle\s*=\s*["']/],
  ['dangerouslySetInnerHTML', /dangerouslySetInnerHTML/],
  ['innerHTML', /\binnerHTML\b/],
  ['insertAdjacentHTML', /insertAdjacentHTML/],
]

function achar(texto: string): string[] {
  return PADROES.filter(([, padrao]) => padrao.test(texto)).map(([nome]) => nome)
}

function arquivos(pasta: string): string[] {
  const saida: string[] = []
  for (const nome of readdirSync(pasta)) {
    const caminho = join(pasta, nome)
    if (statSync(caminho).isDirectory()) {
      if (nome === 'gerado') continue
      saida.push(...arquivos(caminho))
    } else if (/\.(ts|tsx|css|html)$/.test(nome) && !/\.test\.tsx?$/.test(nome)) {
      saida.push(caminho)
    }
  }
  return saida
}

describe('sem HTML cru', () => {
  it('nenhum arquivo tem <style, style=", dangerouslySetInnerHTML, innerHTML nem insertAdjacentHTML', () => {
    const achados = [...arquivos('src'), 'index.html']
      .map((caminho) => ({ caminho, achados: achar(readFileSync(caminho, 'utf8')) }))
      .filter((item) => item.achados.length > 0)
    expect(achados).toEqual([])
  })

  it('o detector acha cada um dos cinco num texto de exemplo (controle: prova que a busca funciona)', () => {
    expect(achar('<style>a{}</style>')).toEqual(['<style'])
    expect(achar('<p style="color:red">')).toEqual(['style="'])
    expect(achar('<div dangerouslySetInnerHTML={x} />')).toEqual(['dangerouslySetInnerHTML'])
    expect(achar('el.innerHTML = x')).toEqual(['innerHTML'])
    expect(achar('el.insertAdjacentHTML("x", y)')).toEqual(['insertAdjacentHTML'])
    expect(achar('<p className="a">')).toEqual([])
  })
})
