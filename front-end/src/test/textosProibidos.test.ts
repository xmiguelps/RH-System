import { afterEach, describe, expect, it } from 'vitest'
import { acharTextoProibido, rotulosDaTela } from './textosProibidos'

afterEach(() => {
  document.body.replaceChildren()
})

function el(tag: string, ...filhos: (Node | string)[]): HTMLElement {
  const elemento = document.createElement(tag)
  elemento.append(...filhos)
  return elemento
}

function montar(...filhos: Node[]): HTMLElement {
  const conteiner = el('div', ...filhos)
  document.body.append(conteiner)
  return conteiner
}

describe('textosProibidos', () => {
  it('rotulosDaTela ignora celulas e a busca acha so a palavra inteira', () => {
    const conteiner = montar(
      el('h1', 'Anotações'),
      el('table', el('tbody', el('tr', el('td', 'nota')))),
      el('button', 'Salvar'),
    )
    const rotulos = rotulosDaTela(conteiner)
    expect(rotulos).toContain('Anotações')
    expect(rotulos).toContain('Salvar')
    expect(acharTextoProibido(rotulos, 'nota')).toBe(false)

    const comBotao = montar(el('button', 'Nota'))
    expect(acharTextoProibido(rotulosDaTela(comBotao), 'nota')).toBe(true)
  })

  it('acha palavra com acento no comeco e procura *** como texto', () => {
    expect(acharTextoProibido('Ótimo trabalho', 'Ótimo')).toBe(true)
    expect(acharTextoProibido('Ótimos', 'Ótimo')).toBe(false)
    expect(acharTextoProibido('CPF ***.123', '***')).toBe(true)
    expect(acharTextoProibido('CPF 123', '***')).toBe(false)
  })

  it('le os paragrafos dos dialogos fora do conteiner, mas nao os da tela', () => {
    const conteiner = montar(el('p', 'Ela recebe um aviso'))
    expect(acharTextoProibido(rotulosDaTela(conteiner), 'Ela recebe um aviso')).toBe(false)
    const portal = el('div', el('p', 'Ela recebe um aviso'))
    portal.setAttribute('role', 'dialog')
    document.body.append(portal)
    expect(acharTextoProibido(rotulosDaTela(conteiner), 'Ela recebe um aviso')).toBe(true)
  })
})
