import { useAuth } from '../../features/autenticacao/useAuth'
import { dataPorExtenso, saudacao } from '../../shared/formato/datas'
import { primeiroNome } from '../../shared/formato/texto'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'

export function InicioDoRh() {
  const { usuario } = useAuth()
  const agora = new Date()
  return (
    <CabecalhoDePagina
      titulo={`${saudacao(agora)}, ${primeiroNome(usuario?.nome ?? '')}`}
      subtitulo={dataPorExtenso(agora)}
    />
  )
}
