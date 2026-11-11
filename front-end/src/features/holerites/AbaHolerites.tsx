import { Plus } from 'lucide-react'
import { useState } from 'react'
import { $api, clienteHttp } from '../../shared/api/api'
import type { Esquema } from '../../shared/api/tipos'
import { useBaixarPorLink } from '../../shared/arquivos/useBaixarPorLink'
import { formatarDataDoInstante } from '../../shared/formato/datas'
import { Botao } from '../../shared/ui/Botao'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { cn } from '../../shared/ui/estilo'
import { Paginacao } from '../../shared/ui/Paginacao'
import { Selo } from '../../shared/ui/Selo'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import type { PropsDaAba } from '../colaboradores/abasDaFicha'
import { ModalEnviarHolerite, ModalSubstituirHolerite } from './ModaisDoHolerite'
import { referenciaDoHolerite, ROTULO_DO_TIPO } from './rotulosDosHolerites'

type HoleriteRh = Esquema<'HoleriteItemRh'>
type Modal = { tipo: 'enviar' } | { tipo: 'substituir'; holerite: HoleriteRh } | null

/** Tela 07: a aba Holerites da ficha do RH (também para pessoa inativa). */
export function AbaHolerites({ ficha }: PropsDaAba) {
  const [todasAsVersoes, setTodasAsVersoes] = useState(false)
  const [pagina, setPagina] = useState(1)
  const [modal, setModal] = useState<Modal>(null)
  const { baixar } = useBaixarPorLink()
  const consulta = $api.useQuery('get', '/api/rh/funcionarios/{funcionarioId}/holerites', {
    params: {
      path: { funcionarioId: ficha.id },
      query: {
        ...(todasAsVersoes ? { todasAsVersoes: true } : {}),
        ...(pagina > 1 ? { pagina } : {}),
      },
    },
  })
  const dados = consulta.data
  const nome = ficha.nome ?? 'a pessoa'

  const colunas: readonly ColunaDaTabela<HoleriteRh>[] = [
    {
      chave: 'referencia',
      titulo: 'Referência',
      celula: (h) => (
        <span className={cn('flex flex-col', !h.versaoAtual && 'opacity-60')}>
          <span>{referenciaDoHolerite(h)}</span>
          {h.versao > 1 && <span className="text-pequeno text-texto-suave">Versão {h.versao}</span>}
          {!h.versaoAtual && (
            <span className="text-pequeno text-texto-suave">Versão substituída</span>
          )}
        </span>
      ),
    },
    { chave: 'tipo', titulo: 'Tipo', celula: (h) => ROTULO_DO_TIPO[h.tipo] },
    {
      chave: 'publicado',
      titulo: 'Publicado em',
      celula: (h) =>
        h.situacao === 'AGENDADO' ? (
          <span className="flex flex-wrap items-center gap-2">
            <Selo tom="alerta">Agendado</Selo>
            <span>Agendado para {formatarDataDoInstante(h.publicacaoEmUtc)}</span>
          </span>
        ) : (
          formatarDataDoInstante(h.publicacaoEmUtc)
        ),
    },
    {
      chave: 'acoes',
      titulo: 'Ações',
      alinhamento: 'direita',
      celula: (h) => (
        <span className="flex justify-end gap-2">
          <Botao
            variante="secundario"
            tamanho="pequeno"
            aria-label={`Baixar PDF de ${referenciaDoHolerite(h)}${h.versao > 1 ? `, versão ${h.versao}` : ''}`}
            onClick={() =>
              void baixar(() =>
                clienteHttp.GET('/api/rh/holerites/{holeriteId}/download', {
                  params: { path: { holeriteId: h.id } },
                }),
              )
            }
          >
            Baixar PDF
          </Botao>
          {h.versaoAtual && (
            <Botao
              variante="texto"
              tamanho="pequeno"
              aria-label={`Substituir ${referenciaDoHolerite(h)}`}
              onClick={() => setModal({ tipo: 'substituir', holerite: h })}
            >
              Substituir
            </Botao>
          )}
        </span>
      ),
    },
  ]

  return (
    <div className="flex flex-col gap-5">
      <div className="flex items-center justify-between gap-4">
        <p className="text-corpo text-texto-suave">
          O colaborador recebe um e-mail sempre que um holerite novo é publicado.
        </p>
        <Botao onClick={() => setModal({ tipo: 'enviar' })}>
          <Plus aria-hidden="true" className="h-4 w-4" />
          Enviar holerite
        </Botao>
      </div>
      <Tabela
        rotulo="Holerites"
        colunas={colunas}
        linhas={dados?.itens}
        chaveDaLinha={(h) => h.id}
        carregando={consulta.isLoading}
        erro={consulta.error ?? undefined}
        aoTentarDeNovo={() => void consulta.refetch()}
        vazio={<EstadoVazio titulo="Nenhum holerite enviado ainda." />}
        rodape={
          dados && dados.totalPaginas > 1 ? (
            <Paginacao dados={dados} aoMudar={setPagina} />
          ) : undefined
        }
      />
      <div>
        <Botao
          variante="texto"
          tamanho="pequeno"
          onClick={() => {
            setPagina(1)
            setTodasAsVersoes((v) => !v)
          }}
        >
          {todasAsVersoes ? 'Mostrar só as versões atuais' : 'Ver versões anteriores'}
        </Botao>
      </div>
      {modal?.tipo === 'enviar' && (
        <ModalEnviarHolerite
          funcionario={{ id: ficha.id, nome }}
          ultimoPublicado={dados?.ultimoPublicado}
          holerites={dados?.itens ?? []}
          aoFechar={() => setModal(null)}
          aoSubstituir={(holerite) => setModal({ tipo: 'substituir', holerite })}
        />
      )}
      {modal?.tipo === 'substituir' && (
        <ModalSubstituirHolerite
          holerite={modal.holerite}
          nome={nome}
          aoFechar={() => setModal(null)}
        />
      )}
    </div>
  )
}
