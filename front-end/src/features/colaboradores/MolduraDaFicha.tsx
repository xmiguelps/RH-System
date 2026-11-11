import type { ReactNode } from 'react'
import { Abas } from '../../shared/ui/Abas'
import { Avatar } from '../../shared/ui/Avatar'
import { Janela } from '../../shared/ui/Janela'

/** O painel de 864 px da ficha (RH, tela 03; gestor, tela 52). */
export function MolduraDaFicha({
  trilha,
  nome,
  linhaDeApoio,
  etiqueta,
  acoes,
  faixas,
  abas,
  aba,
  aoMudarAba,
  aoFechar,
  children,
}: {
  trilha: { rotulo: string; aoClicar: () => void }
  nome: string | null
  linhaDeApoio: string
  etiqueta: ReactNode
  acoes?: ReactNode
  faixas?: ReactNode
  abas: readonly { valor: string; rotulo: string }[]
  aba: string
  aoMudarAba: (valor: string) => void
  aoFechar: () => void
  children: ReactNode
}) {
  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo="Ficha do colaborador"
      posicao="direita"
      largura="larga"
    >
      <div className="flex flex-col gap-6">
        <nav aria-label="Trilha" className="text-tabela text-texto-suave">
          <a
            href=".."
            onClick={(evento) => {
              evento.preventDefault()
              trilha.aoClicar()
            }}
            className="text-primaria-media underline-offset-2 hover:underline"
          >
            {trilha.rotulo}
          </a>
          {nome && <span> / {nome}</span>}
        </nav>
        <div className="flex items-start justify-between gap-6">
          <div className="flex items-center gap-4">
            {nome && <Avatar nome={nome} tamanho="ficha" />}
            <div className="flex flex-col gap-1">
              <p className="text-ficha font-bold text-texto">{nome ?? '—'}</p>
              {linhaDeApoio && <p className="text-corpo text-texto-suave">{linhaDeApoio}</p>}
              <div>{etiqueta}</div>
            </div>
          </div>
          {acoes && <div className="flex shrink-0 items-center gap-3">{acoes}</div>}
        </div>
        <Abas
          rotulo="Partes da ficha"
          variante="sublinhada"
          itens={abas}
          valor={aba}
          aoMudar={aoMudarAba}
        />
        {faixas}
        <div>{children}</div>
      </div>
    </Janela>
  )
}
