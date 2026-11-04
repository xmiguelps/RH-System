import { z } from 'zod'

/** De 8 a 128 caracteres e sem trim: espaço nas pontas faz parte da senha. */
export function campoSenhaNova() {
  return z.string().min(8, 'Use no mínimo 8 caracteres.').max(128, 'Use no máximo 128 caracteres.')
}

export const esquemaDeNovaSenha = z
  .object({
    novaSenha: campoSenhaNova(),
    confirmacao: z.string().min(1, 'Repita a nova senha.'),
  })
  .refine((v) => v.confirmacao === v.novaSenha, {
    message: 'As senhas não conferem.',
    path: ['confirmacao'],
  })

export const esquemaDeTrocaDeSenha = z
  .object({
    senhaAtual: z.string().min(1, 'Informe a senha atual.').max(128),
    novaSenha: campoSenhaNova(),
    confirmacao: z.string().min(1, 'Repita a nova senha.'),
  })
  .refine((v) => v.confirmacao === v.novaSenha, {
    message: 'As senhas não conferem.',
    path: ['confirmacao'],
  })
  .refine((v) => v.novaSenha !== v.senhaAtual, {
    message: 'A nova senha precisa ser diferente da atual.',
    path: ['novaSenha'],
  })
