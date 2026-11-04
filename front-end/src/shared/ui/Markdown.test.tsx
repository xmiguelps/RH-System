import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { Markdown } from './Markdown'

declare global {
  interface Window {
    __xss?: number
  }
}

afterEach(() => {
  delete window.__xss
})

describe('Markdown', () => {
  it('Markdown escreve negrito, italico, lista, citacao e codigo', () => {
    const { container } = render(
      <Markdown texto={'**forte** e *leve*\n\n- um\n- dois\n\n> citado\n\n`codigo`'} />,
    )
    expect(container.querySelector('strong')).toHaveTextContent('forte')
    expect(container.querySelector('em')).toHaveTextContent('leve')
    expect(container.querySelectorAll('li')).toHaveLength(2)
    expect(container.querySelector('blockquote')).toHaveTextContent('citado')
    expect(container.querySelector('code')).toHaveTextContent('codigo')
  })

  it('Markdown mostra HTML cru como TEXTO e nao executa nada (Review Focus 3)', () => {
    const { container } = render(
      <Markdown
        texto={
          '<script>window.__xss = 1</script> <img src=x onerror="window.__xss = 2"> <b onclick="1">n</b>'
        }
      />,
    )
    expect(container.querySelector('script')).toBeNull()
    expect(container.querySelector('img')).toBeNull()
    expect(container.querySelector('b')).toBeNull()
    expect(window.__xss).toBeUndefined()
    expect(container).toHaveTextContent('<script>')
  })

  it('Markdown neutraliza javascript: nos links', () => {
    render(<Markdown texto={'[x](javascript:alert(1)) e [ok](https://exemplo.com)'} />)
    expect(screen.getByText('x').closest('a')).toHaveAttribute('href', '')
    expect(screen.getByRole('link', { name: 'ok' })).toHaveAttribute('href', 'https://exemplo.com')
  })

  it('Markdown nao cria img e nao pede imagem de terceiro', () => {
    const { container } = render(<Markdown texto={'![a](https://exemplo.com/x.png)'} />)
    expect(container.querySelector('img')).toBeNull()
  })

  it('Markdown nao deixa style nem <style> chegar ao DOM', () => {
    const { container } = render(
      <Markdown texto={'<div style="color:red">x</div> <style>body{color:red}</style>'} />,
    )
    expect(container.querySelectorAll('[style]')).toHaveLength(0)
    expect(container.querySelector('style')).toBeNull()
  })

  it('Markdown abre link em nova aba com noopener noreferrer', () => {
    render(<Markdown texto={'[site](https://exemplo.com)'} />)
    const link = screen.getByRole('link', { name: 'site' })
    expect(link).toHaveAttribute('target', '_blank')
    expect(link).toHaveAttribute('rel', 'noopener noreferrer')
  })

  it('Markdown rebaixa # para h3 e ### para h4', () => {
    const { container } = render(<Markdown texto={'# Um\n\n## Dois\n\n### Tres\n\n#### Quatro'} />)
    expect(container.querySelector('h1')).toBeNull()
    expect(container.querySelector('h2')).toBeNull()
    expect(container.querySelectorAll('h3')).toHaveLength(2)
    expect(container.querySelectorAll('h4')).toHaveLength(2)
  })

  it('Markdown de texto vazio nao escreve nada', () => {
    const { container } = render(<Markdown texto="" />)
    expect(container.textContent).toBe('')
  })
})
