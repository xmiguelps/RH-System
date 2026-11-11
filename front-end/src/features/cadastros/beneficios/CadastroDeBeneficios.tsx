import { keepPreviousData, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { $api } from '../../../shared/api/api'
import type { Esquema } from '../../../shared/api/tipos'
import { useEstadoDaLista } from '../../../shared/lista/useEstadoDaLista'
import { Botao } from '../../../shared/ui/Botao'
import { JanelaDeConfirmacao } from '../../../shared/ui/JanelaDeConfirmacao'
import type { ColunaDaTabela } from '../../../shared/ui/Tabela'
import { invalidarCadastros } from '../invalidarCadastros'
import { ListaDeCadastro } from '../ListaDeCadastro'
import { ModalDeBeneficio } from './ModalDeBeneficio'

type Beneficio = Esquema<'BeneficioCatalogoItem'>

const simOuNao = (valor: boolean) => (valor ? 'Sim' : 'Não')

export function CadastroDeBeneficios() {
  const queryClient = useQueryClient()
  const lista = useEstadoDaLista({ filtros: [] as const })
  const [modal, setModal] = useState<{ beneficio?: Beneficio } | null>(null)
  const [excluindo, setExcluindo] = useState<Beneficio | null>(null)
  const [erroDaExclusao, setErroDaExclusao] = useState<unknown>(null)
  const excluir = $api.useMutation('delete', '/api/rh/beneficios/{beneficioId}')
  const consulta = $api.useQuery(
    'get',
    '/api/rh/beneficios',
    { params: { query: lista.consulta } },
    { placeholderData: keepPreviousData },
  )

  const colunas: ColunaDaTabela<Beneficio>[] = [
    {
      chave: 'nome',
      titulo: 'Benefício',
      celula: (b) => (
        <div className="flex flex-col">
          <span className="font-semibold text-texto">{b.nome}</span>
          {b.descricao && <span className="text-pequeno text-texto-suave">{b.descricao}</span>}
        </div>
      ),
    },
    {
      chave: 'quem',
      titulo: 'Quem pode pedir',
      celula: (b) => (b.opcional ? 'A própria pessoa pode pedir' : 'Só o RH concede'),
    },
    { chave: 'dependentes', titulo: 'Dependentes', celula: (b) => simOuNao(b.aceitaDependentes) },
    { chave: 'trajeto', titulo: 'Trajeto', celula: (b) => simOuNao(b.aceitaTrajeto) },
    { chave: 'pessoas', titulo: 'Pessoas', celula: (b) => b.quantidadePessoas },
    {
      chave: 'acoes',
      titulo: 'Ações',
      alinhamento: 'direita',
      celula: (b) => (
        <div className="flex justify-end gap-2">
          <Botao variante="secundario" tamanho="pequeno" onClick={() => setModal({ beneficio: b })}>
            Editar
          </Botao>
          <Botao
            variante="perigo"
            tamanho="pequeno"
            onClick={() => {
              setErroDaExclusao(null)
              setExcluindo(b)
            }}
          >
            Excluir
          </Botao>
        </div>
      ),
    },
  ]

  return (
    <>
      <ListaDeCadastro
        titulo="Benefícios"
        apoio="O que a empresa oferece"
        rotuloDaTabela="Benefícios"
        botaoNovo={{ rotulo: 'Novo benefício', aoClicar: () => setModal({}) }}
        colunas={colunas}
        chaveDaLinha={(b) => b.id}
        dados={consulta.data}
        carregando={consulta.isFetching}
        erro={consulta.error}
        aoTentarDeNovo={() => void consulta.refetch()}
        busca={{ valor: lista.busca, aoMudar: lista.definirBusca }}
        aoMudarPagina={lista.definirPagina}
        vazio="Nenhum benefício cadastrado."
        vazioComBusca="Nenhum benefício encontrado."
      />
      <ModalDeBeneficio
        beneficio={modal?.beneficio}
        aberta={modal !== null}
        aoFechar={() => setModal(null)}
      />
      <JanelaDeConfirmacao
        aberta={excluindo !== null}
        titulo={`Excluir o benefício ${excluindo?.nome ?? ''}?`}
        descricao="Só é possível se ele nunca foi dado a ninguém."
        rotuloDeConfirmar="Excluir"
        perigo
        confirmando={excluir.isPending}
        erro={erroDaExclusao ?? undefined}
        aoCancelar={() => setExcluindo(null)}
        aoConfirmar={async () => {
          if (!excluindo) return
          try {
            await excluir.mutateAsync({ params: { path: { beneficioId: excluindo.id } } })
          } catch (erro) {
            setErroDaExclusao(erro)
            return
          }
          setExcluindo(null)
          await invalidarCadastros(queryClient, '/api/rh/beneficios')
        }}
      />
    </>
  )
}
