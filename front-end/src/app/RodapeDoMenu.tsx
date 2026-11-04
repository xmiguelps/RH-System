import { useState } from 'react'
import { JanelaTrocarSenha } from '../features/autenticacao/JanelaTrocarSenha'
import { useAuth } from '../features/autenticacao/useAuth'
import { Avatar } from '../shared/ui/Avatar'
import { linhaDeApoio } from './linhaDeApoio'

const BOTAO =
  'text-pequeno font-semibold text-menu-texto underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-primaria-clara'

export function RodapeDoMenu() {
  const { usuario, sair } = useAuth()
  const [trocando, setTrocando] = useState(false)
  if (!usuario) return null
  const apoio = linhaDeApoio(usuario)
  return (
    <div
      data-testid="rodape-do-menu"
      className="flex items-start gap-3 border-t border-menu-linha px-6 py-5"
    >
      <Avatar nome={usuario.nome} tamanho="menu" />
      <div className="flex min-w-0 flex-col gap-0.5">
        <p className="truncate text-corpo font-semibold text-menu-texto">{usuario.nome}</p>
        {apoio && <p className="text-mini text-menu-apoio">{apoio}</p>}
        <div className="mt-2 flex gap-4">
          <button type="button" onClick={() => setTrocando(true)} className={BOTAO}>
            Trocar senha
          </button>
          <button type="button" onClick={() => void sair()} className={BOTAO}>
            Sair
          </button>
        </div>
      </div>
      <JanelaTrocarSenha aberta={trocando} aoFechar={() => setTrocando(false)} />
    </div>
  )
}
