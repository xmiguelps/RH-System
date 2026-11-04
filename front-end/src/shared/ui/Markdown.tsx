import ReactMarkdown, { type Components } from 'react-markdown'
import { cn } from './estilo'

// Só estes elementos; nada de img (imagem de outro domínio é pedido a terceiro e a política barra).
const PERMITIDOS = [
  'p',
  'br',
  'strong',
  'em',
  'del',
  'ul',
  'ol',
  'li',
  'a',
  'blockquote',
  'code',
  'pre',
  'hr',
  'h1',
  'h2',
  'h3',
  'h4',
]

// O <h1> é o da página e o <h2> o do bloco: os títulos do texto descem de nível.
const COMPONENTES: Components = {
  a: ({ href, children }) => (
    <a
      href={href}
      target="_blank"
      rel="noopener noreferrer"
      className="text-primaria-media underline underline-offset-2"
    >
      {children}
    </a>
  ),
  h1: ({ children }) => <h3 className="mt-4 text-t3 font-bold">{children}</h3>,
  h2: ({ children }) => <h3 className="mt-4 text-t3 font-bold">{children}</h3>,
  h3: ({ children }) => <h4 className="mt-3 text-corpo font-bold">{children}</h4>,
  h4: ({ children }) => <h4 className="mt-3 text-corpo font-bold">{children}</h4>,
  ul: ({ children }) => <ul className="list-disc pl-6">{children}</ul>,
  ol: ({ children }) => <ol className="list-decimal pl-6">{children}</ol>,
  blockquote: ({ children }) => (
    <blockquote className="border-l-4 border-borda pl-4 text-texto-suave">{children}</blockquote>
  ),
  code: ({ children }) => (
    <code className="rounded bg-superficie-suave px-1 font-mono text-pequeno">{children}</code>
  ),
}

export function Markdown({ texto, className }: { texto: string; className?: string }) {
  if (!texto) return null
  return (
    <div className={cn('flex flex-col gap-3 text-corpo text-texto', className)}>
      <ReactMarkdown allowedElements={PERMITIDOS} unwrapDisallowed components={COMPONENTES}>
        {texto}
      </ReactMarkdown>
    </div>
  )
}
