import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { invalidarContadoresDoMenu } from '../../app/useContadoresDoMenu'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { formatarData } from '../../shared/formato/datas'
import { Aviso } from '../../shared/ui/Aviso'
import { Botao } from '../../shared/ui/Botao'
import { Selo } from '../../shared/ui/Selo'
import { useNotificacao } from '../../shared/ui/useNotificacao'

type Ajuste = Esquema<'PainelAjusteDePontoPendente'>

/** O ajuste de ponto que espera o gestor, no Painel da equipe (decisão da fatia 3; observação opcional, aqui vazia). */
export function CartaoDeAjusteDoGestor({ ajuste }: { ajuste: Ajuste }) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const aprovar = $api.useMutation('post', '/api/gestor/ponto/pedidos-de-ajuste/{pedidoId}/aprovar')
  const recusar = $api.useMutation('post', '/api/gestor/ponto/pedidos-de-ajuste/{pedidoId}/recusar')
  const [erro, setErro] = useState<string | null>(null)
  const decidindo = aprovar.isPending || recusar.isPending

  async function decidir(qual: 'aprovar' | 'recusar') {
    if (decidindo) return
    setErro(null)
    try {
      await (qual === 'aprovar' ? aprovar : recusar).mutateAsync({
        params: { path: { pedidoId: ajuste.pedidoId } },
        body: { observacao: null },
      })
      notificacao.sucesso(
        qual === 'aprovar' ? 'Pedido aprovado. Agora o RH confirma.' : 'Pedido recusado.',
      )
    } catch (e) {
      // 409 e 404: outra pessoa decidiu antes (ou o pedido saiu da equipe); mostra o texto e recarrega
      if (e instanceof ErroDeApi && (e.status === 409 || e.status === 404))
        notificacao.erro(mensagemDeErro(e))
      else {
        setErro(mensagemDeErro(e))
        return
      }
    }
    await Promise.all([
      invalidarRota(queryClient, '/api/gestor/'),
      invalidarContadoresDoMenu(queryClient),
    ])
  }

  return (
    <li
      aria-label={`Ajuste de ponto de ${ajuste.funcionario.nome}`}
      className="flex flex-col gap-3 rounded-cartao border border-borda bg-superficie p-5"
    >
      <Selo tom="alerta">Ajuste de ponto</Selo>
      <p className="text-corpo text-texto">
        {ajuste.funcionario.nome} pediu: {ajuste.resumoDaCorrecao}
      </p>
      <p className="text-pequeno text-texto-suave">Dia {formatarData(ajuste.data)}</p>
      <blockquote className="whitespace-pre-line text-tabela text-texto">
        “{ajuste.justificativa}”
      </blockquote>
      {erro && <Aviso tom="erro">{erro}</Aviso>}
      <div className="flex gap-3">
        <Botao
          tamanho="pequeno"
          onClick={() => void decidir('aprovar')}
          carregando={aprovar.isPending}
          disabled={recusar.isPending}
        >
          Aprovar
        </Botao>
        <Botao
          variante="secundario"
          tamanho="pequeno"
          onClick={() => void decidir('recusar')}
          carregando={recusar.isPending}
          disabled={aprovar.isPending}
        >
          Recusar
        </Botao>
      </div>
    </li>
  )
}
