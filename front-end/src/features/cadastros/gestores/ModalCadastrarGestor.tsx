import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { $api } from '../../../shared/api/api'
import { mensagemDeErro } from '../../../shared/api/mensagemDeErro'
import { Botao } from '../../../shared/ui/Botao'
import { Campo } from '../../../shared/ui/Campo'
import { ComboDeBusca } from '../../../shared/ui/ComboDeBusca'
import { Janela } from '../../../shared/ui/Janela'
import { SEM_GESTOR, useBuscaDePessoas } from '../../colaboradores/useBuscaDePessoas'
import { invalidarCadastros } from '../invalidarCadastros'

function Conteudo({ aoFechar }: { aoFechar: () => void }) {
  const queryClient = useQueryClient()
  const cadastrar = $api.useMutation('post', '/api/rh/gestores')
  const busca = useBuscaDePessoas()
  const [pessoa, setPessoa] = useState<string | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const opcoes = busca.opcoes.filter((o) => o.valor !== SEM_GESTOR)

  const salvar = async () => {
    if (!pessoa) {
      setErro('Escolha a pessoa.')
      return
    }
    setErro(null)
    try {
      await cadastrar.mutateAsync({ body: { funcionarioId: Number(pessoa) } })
    } catch (falha) {
      setErro(mensagemDeErro(falha))
      return
    }
    await invalidarCadastros(queryClient, '/api/rh/gestores')
    aoFechar()
  }

  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo="Cadastrar gestor"
      tamanho="media"
      rodape={
        <>
          <Botao variante="secundario" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao carregando={cadastrar.isPending} onClick={() => void salvar()}>
            Cadastrar gestor
          </Botao>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <p className="text-corpo text-texto-suave">
          Só quem é do RH é cadastrado aqui, para decidir os pedidos de gestores. Para pôr alguém
          como gestor de um setor, escolha a pessoa em Cadastros, Setores.
        </p>
        <Campo rotulo="Pessoa" erro={erro}>
          <ComboDeBusca
            rotulo="Pessoa"
            placeholder="Busque pelo nome"
            opcoes={opcoes}
            valor={pessoa}
            aoMudar={setPessoa}
            aoDigitar={busca.aoDigitar}
            carregando={busca.carregando}
          />
        </Campo>
      </div>
    </Janela>
  )
}

export function ModalCadastrarGestor({
  aberta,
  aoFechar,
}: {
  aberta: boolean
  aoFechar: () => void
}) {
  if (!aberta) return null
  return <Conteudo aoFechar={aoFechar} />
}
