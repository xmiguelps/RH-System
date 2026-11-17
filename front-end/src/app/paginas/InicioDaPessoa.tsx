import { useState } from 'react'
import { useAuth } from '../../features/autenticacao/useAuth'
import { CartaoProximoPagamento } from '../../features/holerites/CartaoProximoPagamento'
import {
  CartaoAniversariantes,
  CartaoAvisosRecentes,
  CartaoFeriasDisponiveis,
  CartaoSeusBeneficios,
  FaixaDeAjustesDoSetor,
} from '../../features/painel/CartoesDoInicio'
import { AvisoDoPedidoEmAnalise } from '../../features/ponto/AvisoDoPedidoEmAnalise'
import { CartaoDoPontoHoje } from '../../features/ponto/CartaoDoPontoHoje'
import { ModalEnviarAtestado } from '../../features/ponto/ModalEnviarAtestado'
import { ModalPedirAjuste } from '../../features/ponto/ModalPedirAjuste'
import { $api } from '../../shared/api/api'
import { dataPorExtenso } from '../../shared/formato/datas'
import { primeiroNome } from '../../shared/formato/texto'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { Esqueleto } from '../../shared/ui/Esqueleto'

/**
 * Início da pessoa (telas 32 e 46: a mesma para o funcionário e o gestor; o gestor ganha a faixa dos
 * ajustes de ponto do setor). Cada cartão carrega e falha sozinho: o contrato separa as rotas por assunto.
 */
export function InicioDaPessoa() {
  const { usuario } = useAuth()
  const [modal, setModal] = useState<'ajuste' | 'atestado' | null>(null)
  const painel = $api.useQuery('get', '/api/eu/painel')
  const p = painel.data
  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo={`Olá, ${primeiroNome(usuario?.nome ?? '')}`}
        subtitulo={dataPorExtenso()}
      />
      {usuario?.perfil === 'Gestor' && <FaixaDeAjustesDoSetor />}
      <CartaoDoPontoHoje
        variante="completo"
        aoPedirAjuste={() => setModal('ajuste')}
        aoEnviarAtestado={() => setModal('atestado')}
      />
      <AvisoDoPedidoEmAnalise />
      {painel.error ? (
        <AvisoDeErro erro={painel.error} aoTentarDeNovo={() => void painel.refetch()} />
      ) : (
        <div className="grid grid-cols-2 items-start gap-6">
          {!p ? (
            <Esqueleto className="h-32 w-full" />
          ) : (
            <CartaoFeriasDisponiveis
              dias={p.ferias.diasDisponiveis}
              prazo={p.ferias.prazoParaTirarSaldo}
            />
          )}
          {p && (
            <CartaoProximoPagamento
              data={p.proximoPagamento.pagamento?.data}
              mesReferencia={p.proximoPagamento.competencia?.mes}
              comLinkParaHolerites
            />
          )}
          {p && <CartaoSeusBeneficios beneficios={p.beneficios} />}
          <CartaoAvisosRecentes />
          <CartaoAniversariantes />
        </div>
      )}
      {modal === 'ajuste' && <ModalPedirAjuste aoFechar={() => setModal(null)} />}
      {modal === 'atestado' && <ModalEnviarAtestado aoFechar={() => setModal(null)} />}
    </div>
  )
}
