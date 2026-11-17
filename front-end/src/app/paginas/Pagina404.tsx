import { BotaoLink } from '../../shared/ui/BotaoLink'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'

export function Pagina404() {
  return (
    <EstadoVazio
      titulo="Página não encontrada."
      descricao="O endereço não existe ou foi trocado."
      acao={<BotaoLink to="/">Ir para o início</BotaoLink>}
    />
  )
}
