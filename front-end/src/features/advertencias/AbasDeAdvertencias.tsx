import { useState } from 'react'
import { $api } from '../../shared/api/api'
import { Botao } from '../../shared/ui/Botao'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Paginacao } from '../../shared/ui/Paginacao'
import type { PropsDaAba } from '../colaboradores/abasDaFicha'
import type { PropsDaAbaDaEquipe } from '../equipe/abasDaFichaDaEquipe'
import {
  PainelDetalheDaAdvertenciaDaEquipe,
  PainelDetalheDaAdvertenciaRh,
} from './DetalhesDaAdvertencia'
import { TabelaDeAdvertencias } from './PecasDasAdvertencias'
import { PainelRegistrarAdvertencia } from './PainelRegistrarAdvertencia'

type Modal = { tipo: 'registrar' } | { tipo: 'abrir'; id: number } | null

/** Tela 11: a aba Advertências da ficha do RH (todos os anos, inclusive canceladas). */
export function AbaAdvertencias({ ficha }: PropsDaAba) {
  const [pagina, setPagina] = useState(1)
  const [modal, setModal] = useState<Modal>(null)
  const consulta = $api.useQuery('get', '/api/rh/funcionarios/{funcionarioId}/advertencias', {
    params: { path: { funcionarioId: ficha.id }, query: pagina > 1 ? { pagina } : {} },
  })
  const dados = consulta.data
  return (
    <div className="flex flex-col gap-5">
      <TabelaDeAdvertencias
        rotulo="Advertências"
        linhas={dados?.itens}
        aoAbrir={(a) => setModal({ tipo: 'abrir', id: a.id })}
        carregando={consulta.isLoading}
        erro={consulta.error ?? undefined}
        aoTentarDeNovo={() => void consulta.refetch()}
        vazio={<EstadoVazio titulo="Nenhuma advertência registrada." />}
        rodape={
          dados && dados.totalPaginas > 1 ? (
            <Paginacao dados={dados} aoMudar={setPagina} />
          ) : undefined
        }
      />
      {ficha.ativo !== false && (
        <div>
          <Botao variante="secundario" onClick={() => setModal({ tipo: 'registrar' })}>
            Registrar advertência
          </Botao>
        </div>
      )}
      {modal?.tipo === 'registrar' && (
        <PainelRegistrarAdvertencia
          modo="rh"
          pessoaFixa={{ id: ficha.id, nome: ficha.nome ?? 'Colaborador' }}
          aoFechar={() => setModal(null)}
        />
      )}
      {modal?.tipo === 'abrir' && (
        <PainelDetalheDaAdvertenciaRh advertenciaId={modal.id} aoFechar={() => setModal(null)} />
      )}
    </div>
  )
}

/** A aba Advertências da ficha da equipe (sem print): a mesma tabela, só leitura. */
export function AbaAdvertenciasDaEquipe({ ficha }: PropsDaAbaDaEquipe) {
  const [pagina, setPagina] = useState(1)
  const [aberta, setAberta] = useState<number | null>(null)
  const consulta = $api.useQuery('get', '/api/gestor/equipe/{funcionarioId}/advertencias', {
    params: { path: { funcionarioId: ficha.id }, query: pagina > 1 ? { pagina } : {} },
  })
  const dados = consulta.data
  return (
    <div className="flex flex-col gap-5">
      <TabelaDeAdvertencias
        rotulo="Advertências"
        linhas={dados?.itens}
        aoAbrir={(a) => setAberta(a.id)}
        carregando={consulta.isLoading}
        erro={consulta.error ?? undefined}
        aoTentarDeNovo={() => void consulta.refetch()}
        vazio={<EstadoVazio titulo="Nenhuma advertência registrada." />}
        rodape={
          dados && dados.totalPaginas > 1 ? (
            <Paginacao dados={dados} aoMudar={setPagina} />
          ) : undefined
        }
      />
      {aberta !== null && (
        <PainelDetalheDaAdvertenciaDaEquipe
          advertenciaId={aberta}
          aoFechar={() => setAberta(null)}
        />
      )}
    </div>
  )
}
