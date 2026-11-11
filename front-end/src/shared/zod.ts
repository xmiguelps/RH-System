import { z } from 'zod'

// jitless: sem isto o zod usa eval ao criar esquemas, e a política de segurança do site (sem unsafe-eval) barra.
// customError: mensagens em português do Brasil (o locales.pt do zod é de Portugal); a mensagem
// escrita no próprio esquema vence esta.
z.config({
  jitless: true,
  customError: (problema) => {
    if (problema.code === 'invalid_type') return 'Campo obrigatório.'
    if (problema.code === 'too_small' && problema.origin === 'string') {
      return Number(problema.minimum) <= 1
        ? 'Campo obrigatório.'
        : `Use no mínimo ${problema.minimum} caracteres.`
    }
    if (problema.code === 'too_big' && problema.origin === 'string') {
      return `Use no máximo ${problema.maximum} caracteres.`
    }
    if (problema.code === 'invalid_format' && problema.format === 'email') {
      return 'Informe um e-mail válido.'
    }
    return undefined
  },
})
