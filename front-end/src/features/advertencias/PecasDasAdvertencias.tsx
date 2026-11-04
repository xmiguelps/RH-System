import type { ReactNode } from 'react'
import type { Esquema } from '../../shared/api/tipos'
import { formatarData, formatarDataHora } from '../../shared/formato/datas'
import { Avatar } from '../../shared/ui/Avatar'
import { Botao } from '../../shared/ui/Botao'
import { Selo } from '../../shared/ui/Selo'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { ROTULO_DO_TIPO, SITUACAO, textoDeAplicadaPor } from './rotulosDasAdvertencias'

type Item = Esquema<'AdvertenciaItem'>

export function SeloDaSituacao({
  situacao,
  daPropriaPessoa = false,
}: {
  situacao: Esquema<'AdvertenciaSituacao'>
  daPropriaPessoa?: boolean
}) {
  const { texto, tom } = SITUACAO[situacao]
  return (
    <Selo tom={tom}>
      {daPropriaPessoa && situacao === 'PENDENTE' ? 'Aguardando sua ciência' : texto}
    </Selo>
  )
}

/** A tabela das telas 14, 11, 59 e da aba da ficha da equipe (as colunas mudam por tela). */
export function TabelaDeAdvertencias({
  rotulo,
  linhas,
  colaborador,
  comAplicadaPor = true,
  aoAbrir,
  carregando,
  erro,
  aoTentarDeNovo,
  vazio,
  rodape,
}: {
  rotulo: string
  linhas: readonly Item[] | undefined
  colaborador?: 'com-setor' | 'so-nome'
  comAplicadaPor?: boolean
  aoAbrir: (item: Item) => void
  carregando?: boolean
  erro?: unknown
  aoTentarDeNovo?: () => void
  vazio: ReactNode
  rodape?: ReactNode
}) {
  const colunas: ColunaDaTabela<Item>[] = [
    { chave: 'data', titulo: 'Data', celula: (a) => formatarData(a.data) },
  ]
  if (colaborador)
    colunas.push({
      chave: 'pessoa',
      titulo: colaborador === 'com-setor' ? 'Colaborador' : 'Pessoa',
      celula: (a) => (
        <span className="flex items-center gap-3">
          <Avatar nome={a.funcionario.nome} />
          <span className="flex flex-col">
            <span className="text-texto">{a.funcionario.nome}</span>
            {colaborador === 'com-setor' && a.funcionario.setor && (
              <span className="text-pequeno text-texto-suave">{a.funcionario.setor}</span>
            )}
          </span>
        </span>
      ),
    })
  colunas.push(
    { chave: 'tipo', titulo: 'Tipo', celula: (a) => ROTULO_DO_TIPO[a.tipo] },
    { chave: 'motivo', titulo: 'Motivo', celula: (a) => a.motivo },
  )
  if (comAplicadaPor)
    colunas.push({
      chave: 'aplicada',
      titulo: 'Aplicada por',
      celula: (a) => textoDeAplicadaPor(a.aplicadaPor),
    })
  colunas.push(
    {
      chave: 'situacao',
      titulo: 'Situação',
      celula: (a) => <SeloDaSituacao situacao={a.situacao} />,
    },
    {
      chave: 'abrir',
      titulo: 'Ações',
      alinhamento: 'direita',
      celula: (a) => (
        <Botao
          variante="secundario"
          tamanho="pequeno"
          aria-label={`Abrir a advertência de ${a.funcionario.nome} de ${formatarData(a.data)}`}
          onClick={() => aoAbrir(a)}
        >
          Abrir
        </Botao>
      ),
    },
  )
  return (
    <Tabela
      rotulo={rotulo}
      colunas={colunas}
      linhas={linhas}
      chaveDaLinha={(a) => a.id}
      carregando={carregando}
      erro={erro}
      aoTentarDeNovo={aoTentarDeNovo}
      vazio={vazio}
      rodape={rodape}
    />
  )
}

type Detalhe = Esquema<'AdvertenciaDetalheGestor'>

/** Os campos do detalhe (RH e gestor). O gestor nunca recebe a contestação (outro esquema). */
export function CamposDaAdvertencia({ advertencia }: { advertencia: Detalhe }) {
  const a = advertencia
  return (
    <div className="flex flex-col gap-5">
      <div className="flex items-center gap-3">
        <Avatar nome={a.funcionario.nome} />
        <span className="flex flex-col">
          <span className="font-bold text-texto">{a.funcionario.nome}</span>
          {a.funcionario.setor && (
            <span className="text-pequeno text-texto-suave">{a.funcionario.setor}</span>
          )}
        </span>
      </div>
      <dl className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1">
          <dt className="text-tabela text-texto-suave">Data do ocorrido</dt>
          <dd className="text-corpo text-texto">{formatarData(a.data)}</dd>
        </div>
        <div className="flex flex-col gap-1">
          <dt className="text-tabela text-texto-suave">Tipo</dt>
          <dd className="text-corpo text-texto">{ROTULO_DO_TIPO[a.tipo]}</dd>
        </div>
        <div className="flex flex-col gap-1">
          <dt className="text-tabela text-texto-suave">Aplicada por</dt>
          <dd className="text-corpo text-texto">{textoDeAplicadaPor(a.aplicadaPor)}</dd>
        </div>
        <div className="flex flex-col gap-1">
          <dt className="text-tabela text-texto-suave">Situação</dt>
          <dd>
            <SeloDaSituacao situacao={a.situacao} />
          </dd>
        </div>
        <div className="col-span-2 flex flex-col gap-1">
          <dt className="text-tabela text-texto-suave">Motivo</dt>
          <dd className="text-corpo text-texto">{a.motivo}</dd>
        </div>
        <div className="col-span-2 flex flex-col gap-1">
          <dt className="text-tabela text-texto-suave">Descrição</dt>
          <dd className="whitespace-pre-line text-corpo text-texto">{a.descricao}</dd>
        </div>
      </dl>
      <p className="text-tabela text-texto-suave">
        {a.dataCienciaUtc
          ? `Ciência em ${formatarDataHora(a.dataCienciaUtc).replace(' ', ', ')}`
          : 'Ainda sem ciência'}
      </p>
    </div>
  )
}
