import type { ErroDeApi } from '../api/erroDeApi'
import { mensagemDeErro } from '../api/mensagemDeErro'

function comInicialMinuscula(nome: string): string {
  return nome.charAt(0).toLowerCase() + nome.slice(1)
}

export function aplicarErrosDoServidor(
  erro: ErroDeApi,
  opcoes: {
    definirErro: (campo: string, mensagem: string) => void
    camposDoFormulario: readonly string[]
  },
): { mensagemGeral: string | null } {
  const sobras: string[] = []
  let casados = 0
  for (const [chave, mensagens] of Object.entries(erro.errors)) {
    const campo = comInicialMinuscula(chave)
    const texto = mensagens.join(' ')
    if (opcoes.camposDoFormulario.includes(campo)) {
      opcoes.definirErro(campo, texto)
      casados += 1
    } else {
      sobras.push(texto)
    }
  }
  if (casados === 0 && sobras.length === 0) {
    return {
      mensagemGeral:
        erro.status === 0 ? mensagemDeErro(erro) : (erro.detail ?? mensagemDeErro(erro)),
    }
  }
  return { mensagemGeral: sobras.length > 0 ? sobras.join(' ') : null }
}
