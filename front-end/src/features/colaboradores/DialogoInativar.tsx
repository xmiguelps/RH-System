import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import type { Esquema } from '../../shared/api/tipos'
import { formatarData, hojeEmSaoPaulo } from '../../shared/formato/datas'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { JanelaDeConfirmacao } from '../../shared/ui/JanelaDeConfirmacao'
import { useNotificacao } from '../../shared/ui/useNotificacao'

type Ficha = Esquema<'FuncionarioFichaRh'>

export function DialogoInativar({
  ficha,
  aberto,
  aoFechar,
  aoNaoEncontrar,
}: {
  ficha: Ficha
  aberto: boolean
  aoFechar: () => void
  aoNaoEncontrar: (erro: unknown) => void
}) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const inativar = $api.useMutation('post', '/api/rh/funcionarios/{funcionarioId}/desligamento')
  const [ultimoDia, setUltimoDia] = useState(() => hojeEmSaoPaulo())
  const [erroDaData, setErroDaData] = useState<string | null>(null)
  const [erro, setErro] = useState<unknown>(null)

  const fechar = () => {
    setErro(null)
    setErroDaData(null)
    setUltimoDia(hojeEmSaoPaulo())
    aoFechar()
  }

  const confirmar = async () => {
    setErro(null)
    setErroDaData(null)
    if (!ultimoDia) {
      setErroDaData('Informe o último dia de trabalho.')
      return
    }
    if (ultimoDia < ficha.dataAdmissao) {
      setErroDaData(
        `O último dia não pode ser anterior à admissão (${formatarData(ficha.dataAdmissao)}).`,
      )
      return
    }
    try {
      await inativar.mutateAsync({
        params: { path: { funcionarioId: ficha.id } },
        body: { ultimoDiaDeTrabalho: ultimoDia },
      })
      await invalidarRota(queryClient, '/api/rh/funcionarios')
      notificacao.sucesso(`Desligamento de ${ficha.nome} registrado.`)
      fechar()
    } catch (falha) {
      if (falha instanceof ErroDeApi && falha.status === 404) {
        fechar()
        aoNaoEncontrar(falha)
        return
      }
      const doCampo = falha instanceof ErroDeApi ? falha.errors.ultimoDiaDeTrabalho : undefined
      if (doCampo) {
        setErroDaData(doCampo.join(' '))
        return
      }
      if (falha instanceof ErroDeApi && falha.status === 409) {
        await invalidarRota(queryClient, '/api/rh/funcionarios')
      }
      setErro(falha)
    }
  }

  return (
    <JanelaDeConfirmacao
      aberta={aberto}
      titulo={`Inativar ${ficha.nome}`}
      variante="digitar"
      textoParaDigitar="desativar"
      rotuloDeConfirmar="Inativar"
      confirmando={inativar.isPending}
      erro={erro ?? undefined}
      aoCancelar={fechar}
      aoConfirmar={confirmar}
      descricao={
        <span className="flex flex-col gap-4">
          <Campo rotulo="Último dia de trabalho" erro={erroDaData}>
            <Entrada
              type="date"
              lang="pt-BR"
              value={ultimoDia}
              onChange={(evento) => setUltimoDia(evento.target.value)}
            />
          </Campo>
          <span>
            A pessoa perde o acesso ao portal, os benefícios dela são desativados e as férias
            futuras são canceladas, sem aviso a ela. Nada é apagado: o histórico continua na ficha.
          </span>
          {ficha.perfil === 'Gestor' && (
            <span>
              Se esta pessoa gerencia um setor, os pedidos que esperam por ela passam para o RH e o
              setor fica sem gestor ativo até você escolher outro em Cadastros, Setores.
            </span>
          )}
        </span>
      }
    />
  )
}
