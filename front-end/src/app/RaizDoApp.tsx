import { Outlet } from 'react-router'
import { useAuth } from '../features/autenticacao/useAuth'
import { Aviso } from '../shared/ui/Aviso'
import { Botao } from '../shared/ui/Botao'
import { Carregando } from '../shared/ui/Carregando'

export function RaizDoApp() {
  const { carregando, usuario, falhaDeConexao, tentarDeNovo } = useAuth()
  if (carregando) {
    return (
      <div className="p-8">
        <Carregando />
      </div>
    )
  }
  // A sessão guardada não foi recusada: só não deu para falar com o servidor. Mandar para a entrada
  // faria a pessoa digitar a senha à toa e ainda esconderia que o problema é a conexão.
  if (falhaDeConexao && !usuario) {
    return (
      <div className="mx-auto flex max-w-[520px] flex-col gap-5 p-8">
        <Aviso tom="erro" titulo="Não foi possível conectar.">
          Confira sua internet e tente de novo. Sua sessão continua guardada.
        </Aviso>
        <div>
          <Botao onClick={() => void tentarDeNovo()}>Tentar de novo</Botao>
        </div>
      </div>
    )
  }
  return <Outlet />
}
