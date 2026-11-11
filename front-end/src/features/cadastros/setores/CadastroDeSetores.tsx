import { keepPreviousData } from '@tanstack/react-query'
import { useState } from 'react'
import { $api } from '../../../shared/api/api'
import type { Esquema } from '../../../shared/api/tipos'
import { useEstadoDaLista } from '../../../shared/lista/useEstadoDaLista'
import { Botao } from '../../../shared/ui/Botao'
import type { ColunaDaTabela } from '../../../shared/ui/Tabela'
import { ListaDeCadastro } from '../ListaDeCadastro'
import { ModalDeSetor } from './ModalDeSetor'

type Setor = Esquema<'SetorItem'>

export function CadastroDeSetores() {
  const lista = useEstadoDaLista({ filtros: [] as const, tamanhoPagina: 100 })
  const [modal, setModal] = useState<{ setor?: Setor } | null>(null)
  const consulta = $api.useQuery(
    'get',
    '/api/rh/setores',
    { params: { query: lista.consulta } },
    { placeholderData: keepPreviousData },
  )

  const colunas: ColunaDaTabela<Setor>[] = [
    {
      chave: 'nome',
      titulo: 'Setor',
      celula: (s) => <span className="font-semibold">{s.nome}</span>,
    },
    { chave: 'descricao', titulo: 'Descrição', celula: (s) => s.descricao ?? '—' },
    { chave: 'gestor', titulo: 'Gestor', celula: (s) => s.gestor?.nome ?? 'Sem gestor' },
    { chave: 'pessoas', titulo: 'Pessoas', celula: (s) => s.quantidadePessoas },
    {
      chave: 'editar',
      titulo: 'Ações',
      alinhamento: 'direita',
      celula: (s) => (
        <Botao variante="secundario" tamanho="pequeno" onClick={() => setModal({ setor: s })}>
          Editar
        </Botao>
      ),
    },
  ]

  return (
    <>
      <ListaDeCadastro
        titulo="Setores"
        apoio="Áreas e seus gestores"
        rotuloDaTabela="Setores"
        botaoNovo={{ rotulo: 'Novo setor', aoClicar: () => setModal({}) }}
        colunas={colunas}
        chaveDaLinha={(s) => s.id}
        dados={consulta.data}
        carregando={consulta.isFetching}
        erro={consulta.error}
        aoTentarDeNovo={() => void consulta.refetch()}
        busca={{ valor: lista.busca, aoMudar: lista.definirBusca }}
        aoMudarPagina={lista.definirPagina}
        vazio="Nenhum setor cadastrado."
        vazioComBusca="Nenhum setor encontrado."
      />
      <ModalDeSetor setor={modal?.setor} aberta={modal !== null} aoFechar={() => setModal(null)} />
    </>
  )
}
