import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { formatarData, formatarDataDoInstante, formatarDataHora } from '../../shared/formato/datas'
import { AreaDeTexto } from '../../shared/ui/AreaDeTexto'
import { Aviso } from '../../shared/ui/Aviso'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { Campo } from '../../shared/ui/Campo'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Janela } from '../../shared/ui/Janela'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { SeloDaSituacao } from './PecasDasAdvertencias'
import {
  LIMITE_DA_RESPOSTA,
  ROTULO_DO_TIPO,
  textoDeAplicadaPor,
  tituloDaAdvertencia,
} from './rotulosDasAdvertencias'

type Minha = Esquema<'AdvertenciaEu'>
type Modal = { tipo: 'contestar' | 'detalhes'; advertencia: Minha } | null

const MINIMO_DA_CONTESTACAO = 10

function useAtualizarMinhas() {
  const queryClient = useQueryClient()
  return () => invalidarRota(queryClient, '/api/eu/advertencias')
}

function CartaoPendente({
  advertencia,
  aoContestar,
}: {
  advertencia: Minha
  aoContestar: () => void
}) {
  const notificacao = useNotificacao()
  const atualizar = useAtualizarMinhas()
  const ciencia = $api.useMutation('post', '/api/eu/advertencias/{advertenciaId}/ciencia')
  const titulo = tituloDaAdvertencia(advertencia)

  async function confirmar() {
    if (ciencia.isPending) return
    try {
      await ciencia.mutateAsync({ params: { path: { advertenciaId: advertencia.id } } })
      notificacao.sucesso('Ciência registrada.')
    } catch (erro) {
      notificacao.erro(mensagemDeErro(erro))
    }
    await atualizar()
  }

  return (
    <section
      aria-label={titulo}
      className="flex flex-col gap-4 rounded-cartao border-2 border-alerta-ponto bg-superficie p-6"
    >
      <div>
        <SeloDaSituacao situacao="PENDENTE" daPropriaPessoa />
      </div>
      <h2 className="text-t3 font-bold text-texto">{titulo}</h2>
      <dl className="grid grid-cols-3 gap-4">
        <div className="flex flex-col gap-1">
          <dt className="text-tabela text-texto-suave">Tipo</dt>
          <dd className="text-corpo text-texto">{ROTULO_DO_TIPO[advertencia.tipo]}</dd>
        </div>
        <div className="flex flex-col gap-1">
          <dt className="text-tabela text-texto-suave">Data</dt>
          <dd className="text-corpo text-texto">{formatarData(advertencia.data)}</dd>
        </div>
        <div className="flex flex-col gap-1">
          <dt className="text-tabela text-texto-suave">Aplicada por</dt>
          <dd className="text-corpo text-texto">
            {textoDeAplicadaPor(advertencia.aplicadaPor, true)}
          </dd>
        </div>
        <div className="col-span-3 flex flex-col gap-1">
          <dt className="text-tabela text-texto-suave">Motivo</dt>
          <dd className="text-corpo text-texto">{advertencia.motivo}</dd>
        </div>
        <div className="col-span-3 flex flex-col gap-1">
          <dt className="text-tabela text-texto-suave">Descrição</dt>
          <dd className="whitespace-pre-line text-corpo text-texto">{advertencia.descricao}</dd>
        </div>
      </dl>
      <Aviso tom="info">
        Confirmar a ciência não quer dizer que você concorda. Se discordar, escolha Contestar e
        explique o que aconteceu. O RH analisa a contestação e responde por e-mail.
      </Aviso>
      <div className="flex gap-3">
        <Botao onClick={() => void confirmar()} carregando={ciencia.isPending}>
          Confirmar ciência
        </Botao>
        <Botao variante="secundario" onClick={aoContestar} disabled={ciencia.isPending}>
          Contestar
        </Botao>
      </div>
    </section>
  )
}

function ModalContestar({ advertencia, aoFechar }: { advertencia: Minha; aoFechar: () => void }) {
  const notificacao = useNotificacao()
  const atualizar = useAtualizarMinhas()
  const contestar = $api.useMutation('post', '/api/eu/advertencias/{advertenciaId}/contestacao')
  const [texto, setTexto] = useState('')
  const [erroDoCampo, setErroDoCampo] = useState<string | null>(null)
  const [erro, setErro] = useState<unknown>(null)

  async function enviar() {
    if (contestar.isPending) return
    const limpo = texto.trim()
    if (limpo.length < MINIMO_DA_CONTESTACAO) {
      setErroDoCampo('Escreva pelo menos 10 caracteres.')
      return
    }
    setErroDoCampo(null)
    setErro(null)
    try {
      await contestar.mutateAsync({
        params: { path: { advertenciaId: advertencia.id } },
        body: { texto: limpo },
      })
      notificacao.sucesso('Contestação enviada. O RH vai analisar e responder por e-mail.')
      await atualizar()
      aoFechar()
    } catch (e) {
      if (e instanceof ErroDeApi && e.status === 400 && e.errors.texto) {
        setErroDoCampo(e.errors.texto.join(' '))
        return
      }
      setErro(e)
      if (e instanceof ErroDeApi && e.status === 422) await atualizar()
    }
  }

  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo="Contestar advertência"
      descricao="Explique o que aconteceu. O RH analisa e responde por e-mail."
      rodape={
        <>
          <Botao variante="secundario" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao onClick={() => void enviar()} carregando={contestar.isPending}>
            Enviar contestação
          </Botao>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <Campo rotulo="O que você quer explicar" erro={erroDoCampo}>
          <AreaDeTexto
            rows={6}
            limite={LIMITE_DA_RESPOSTA}
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
          />
        </Campo>
        <p className="text-pequeno text-texto-suave">
          Depois de enviada, a contestação não pode ser trocada.
        </p>
        {!!erro && <AvisoDeErro erro={erro} />}
      </div>
    </Janela>
  )
}

function textoDoDesfecho(desfecho: Minha['desfechoDaContestacao']): string | null {
  if (desfecho === 'ACATADA') return 'O RH acatou a sua contestação: a advertência foi cancelada.'
  if (desfecho === 'MANTIDA') return 'O RH analisou a sua contestação e manteve a advertência.'
  return null
}

function ModalDetalhes({ advertencia: a, aoFechar }: { advertencia: Minha; aoFechar: () => void }) {
  const desfecho = textoDoDesfecho(a.desfechoDaContestacao)
  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo={`Advertência de ${formatarData(a.data)}`}
      rodape={<Botao onClick={aoFechar}>Fechar</Botao>}
    >
      <div className="flex flex-col gap-4">
        <dl className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-1">
            <dt className="text-tabela text-texto-suave">Tipo</dt>
            <dd className="text-corpo text-texto">{ROTULO_DO_TIPO[a.tipo]}</dd>
          </div>
          <div className="flex flex-col gap-1">
            <dt className="text-tabela text-texto-suave">Data</dt>
            <dd className="text-corpo text-texto">{formatarData(a.data)}</dd>
          </div>
          <div className="flex flex-col gap-1">
            <dt className="text-tabela text-texto-suave">Aplicada por</dt>
            <dd className="text-corpo text-texto">{textoDeAplicadaPor(a.aplicadaPor, true)}</dd>
          </div>
          <div className="flex flex-col gap-1">
            <dt className="text-tabela text-texto-suave">Situação</dt>
            <dd>
              <SeloDaSituacao situacao={a.situacao} daPropriaPessoa />
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
        {a.dataCienciaUtc && (
          <p className="text-tabela text-texto-suave">
            Ciência em {formatarDataHora(a.dataCienciaUtc).replace(' ', ', ')}
          </p>
        )}
        {a.textoContestacao && (
          <section
            aria-label="Sua contestação"
            className="flex flex-col gap-2 rounded-cartao bg-superficie-suave p-4"
          >
            <p className="text-tabela font-semibold text-texto">
              Você contestou em {formatarDataDoInstante(a.dataContestacaoUtc)}
            </p>
            <p className="whitespace-pre-line text-corpo text-texto">{a.textoContestacao}</p>
          </section>
        )}
        {desfecho && <p className="text-corpo text-texto">{desfecho}</p>}
      </div>
    </Janela>
  )
}

/** Tela 43 (a mesma para os três perfis): as pendentes em destaque e o histórico. */
export function PaginaMinhasAdvertencias() {
  const consulta = $api.useQuery('get', '/api/eu/advertencias')
  const [modal, setModal] = useState<Modal>(null)
  const itens = consulta.data?.itens
  const pendentes = itens?.filter((a) => a.situacao === 'PENDENTE') ?? []
  const historico = itens?.filter((a) => a.situacao !== 'PENDENTE')

  const colunas: readonly ColunaDaTabela<Minha>[] = [
    { chave: 'data', titulo: 'Data', celula: (a) => formatarData(a.data) },
    { chave: 'tipo', titulo: 'Tipo', celula: (a) => ROTULO_DO_TIPO[a.tipo] },
    { chave: 'motivo', titulo: 'Motivo', celula: (a) => a.motivo },
    {
      chave: 'aplicada',
      titulo: 'Aplicada por',
      celula: (a) => textoDeAplicadaPor(a.aplicadaPor),
    },
    {
      chave: 'situacao',
      titulo: 'Situação',
      celula: (a) => <SeloDaSituacao situacao={a.situacao} daPropriaPessoa />,
    },
    {
      chave: 'acoes',
      titulo: 'Ações',
      alinhamento: 'direita',
      celula: (a) => (
        <Botao
          variante="secundario"
          tamanho="pequeno"
          aria-label={`Ver detalhes da advertência de ${formatarData(a.data)}`}
          onClick={() => setModal({ tipo: 'detalhes', advertencia: a })}
        >
          Ver detalhes
        </Botao>
      ),
    },
  ]

  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo="Minhas advertências"
        subtitulo="Registros disciplinares feitos pelo seu gestor ou pelo RH"
      />
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      ) : !itens ? (
        <div role="status" aria-label="Carregando as advertências" className="flex flex-col gap-3">
          <Esqueleto className="h-40 w-full" />
          <Esqueleto className="h-32 w-full" />
        </div>
      ) : itens.length === 0 ? (
        <EstadoVazio titulo="Você não tem advertências." />
      ) : (
        <>
          {pendentes.map((a) => (
            <CartaoPendente
              key={a.id}
              advertencia={a}
              aoContestar={() => setModal({ tipo: 'contestar', advertencia: a })}
            />
          ))}
          {historico && historico.length > 0 && (
            <section aria-labelledby="titulo-historico" className="flex flex-col gap-3">
              <h2 id="titulo-historico" className="text-t3 font-bold text-texto">
                Histórico
              </h2>
              <Tabela
                rotulo="Histórico"
                colunas={colunas}
                linhas={historico}
                chaveDaLinha={(a) => a.id}
              />
            </section>
          )}
        </>
      )}
      <p className="text-tabela text-texto-suave">Dúvidas sobre uma advertência? Fale com o RH</p>
      {modal?.tipo === 'contestar' && (
        <ModalContestar advertencia={modal.advertencia} aoFechar={() => setModal(null)} />
      )}
      {modal?.tipo === 'detalhes' && (
        <ModalDetalhes advertencia={modal.advertencia} aoFechar={() => setModal(null)} />
      )}
    </div>
  )
}
