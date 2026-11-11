import { keepPreviousData } from '@tanstack/react-query'
import { useState } from 'react'
import { $api } from '../../../shared/api/api'
import type { Esquema } from '../../../shared/api/tipos'
import { useEstadoDaLista } from '../../../shared/lista/useEstadoDaLista'
import { Botao } from '../../../shared/ui/Botao'
import type { ColunaDaTabela } from '../../../shared/ui/Tabela'
import { ListaDeCadastro } from '../ListaDeCadastro'
import { ModalDeCargo } from './ModalDeCargo'
import { formatarCargaSemanal } from './rotulos'

type Cargo = Esquema<'CargoItem'>

export function CadastroDeCargos() {
  const lista = useEstadoDaLista({ filtros: [] as const })
  const [modal, setModal] = useState<{ cargo?: Cargo } | null>(null)
  const consulta = $api.useQuery(
    'get',
    '/api/rh/cargos',
    { params: { query: lista.consulta } },
    { placeholderData: keepPreviousData },
  )

  const colunas: ColunaDaTabela<Cargo>[] = [
    {
      chave: 'nome',
      titulo: 'Cargo',
      celula: (c) => <span className="font-semibold">{c.nome}</span>,
    },
    { chave: 'descricao', titulo: 'Descrição', celula: (c) => c.descricao ?? '—' },
    {
      chave: 'carga',
      titulo: 'Carga semanal',
      celula: (c) => formatarCargaSemanal(c.cargaSemanalHoras),
    },
    { chave: 'pessoas', titulo: 'Pessoas', celula: (c) => c.quantidadePessoas },
    {
      chave: 'editar',
      titulo: 'Ações',
      alinhamento: 'direita',
      celula: (c) => (
        <Botao variante="secundario" tamanho="pequeno" onClick={() => setModal({ cargo: c })}>
          Editar
        </Botao>
      ),
    },
  ]

  return (
    <>
      <ListaDeCadastro
        titulo="Cargos"
        apoio="Cada colaborador tem um cargo. A carga horária do cargo é usada para calcular o saldo do ponto e o banco de horas."
        rotuloDaTabela="Cargos"
        botaoNovo={{ rotulo: 'Novo cargo', aoClicar: () => setModal({}) }}
        colunas={colunas}
        chaveDaLinha={(c) => c.id}
        dados={consulta.data}
        carregando={consulta.isFetching}
        erro={consulta.error}
        aoTentarDeNovo={() => void consulta.refetch()}
        busca={{ valor: lista.busca, aoMudar: lista.definirBusca }}
        aoMudarPagina={lista.definirPagina}
        vazio="Nenhum cargo cadastrado."
        vazioComBusca="Nenhum cargo encontrado."
      />
      <ModalDeCargo cargo={modal?.cargo} aberta={modal !== null} aoFechar={() => setModal(null)} />
    </>
  )
}
