import { useState } from 'react'
import { useAuth } from '../../features/autenticacao/useAuth'
import { AvisoDoPedidoEmAnalise } from '../../features/ponto/AvisoDoPedidoEmAnalise'
import { CartaoDoPontoHoje } from '../../features/ponto/CartaoDoPontoHoje'
import { ModalEnviarAtestado } from '../../features/ponto/ModalEnviarAtestado'
import { ModalPedirAjuste } from '../../features/ponto/ModalPedirAjuste'
import { dataPorExtenso } from '../../shared/formato/datas'
import { primeiroNome } from '../../shared/formato/texto'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'

/** Início da pessoa (tela 32). Esta fatia monta o cartão do ponto e o aviso; o resto é da 7d. */
export function InicioDaPessoa() {
  const { usuario } = useAuth()
  const [modal, setModal] = useState<'ajuste' | 'atestado' | null>(null)
  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo={`Olá, ${primeiroNome(usuario?.nome ?? '')}`}
        subtitulo={dataPorExtenso()}
      />
      <CartaoDoPontoHoje
        variante="completo"
        aoPedirAjuste={() => setModal('ajuste')}
        aoEnviarAtestado={() => setModal('atestado')}
      />
      <AvisoDoPedidoEmAnalise />
      {modal === 'ajuste' && <ModalPedirAjuste aoFechar={() => setModal(null)} />}
      {modal === 'atestado' && <ModalEnviarAtestado aoFechar={() => setModal(null)} />}
    </div>
  )
}
