import { Aviso } from '../../shared/ui/Aviso'
import { Botao } from '../../shared/ui/Botao'
import { BotaoLink } from '../../shared/ui/BotaoLink'

/**
 * A tela que aparece no lugar de uma tela que quebrou (`errorElement` das rotas). Não mostra o erro
 * em si: o texto técnico não ajuda quem usa o sistema e pode revelar como ele foi feito.
 */
export function PaginaDeErro() {
  return (
    <div className="mx-auto flex max-w-[520px] flex-col gap-5 p-8">
      <Aviso tom="erro" titulo="Algo deu errado.">
        Tente recarregar a página. Se continuar assim, avise o RH.
      </Aviso>
      <div className="flex gap-3">
        <Botao onClick={() => window.location.reload()}>Recarregar a página</Botao>
        <BotaoLink to="/" variante="secundario">
          Ir para o início
        </BotaoLink>
      </div>
    </div>
  )
}
