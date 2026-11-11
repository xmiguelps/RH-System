import { mensagemDeErro } from '../api/mensagemDeErro'
import { Aviso } from './Aviso'
import { Botao } from './Botao'

export function AvisoDeErro({
  erro,
  aoTentarDeNovo,
  titulo,
}: {
  erro: unknown
  aoTentarDeNovo?: () => void
  titulo?: string
}) {
  return (
    <Aviso
      tom="erro"
      titulo={titulo}
      acao={
        aoTentarDeNovo && (
          <Botao variante="secundario" tamanho="pequeno" onClick={aoTentarDeNovo}>
            Tentar de novo
          </Botao>
        )
      }
    >
      {mensagemDeErro(erro)}
    </Aviso>
  )
}
