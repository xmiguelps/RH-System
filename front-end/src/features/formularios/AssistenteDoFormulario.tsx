import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useLocation, useNavigate, useParams, useSearchParams } from 'react-router'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { formatarDiaMesDaData } from '../../shared/formato/datas'
import { inteiroDaUrl, valorDeEnum } from '../../shared/lista/valoresDeLista'
import { Aviso } from '../../shared/ui/Aviso'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Etapas } from '../../shared/ui/Etapas'
import { Janela } from '../../shared/ui/Janela'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { EtapaDetalhes, type Detalhes, type ErrosDosDetalhes } from './EtapaDetalhes'
import { EtapaPerguntas } from './EtapaPerguntas'
import { EtapaPublicacao, type ErrosDaPublicacao } from './EtapaPublicacao'
import { EtapaVerificacao } from './EtapaVerificacao'
import { travasDosDetalhes, useVerificacao } from './regrasDoAssistente'
import { PreviaDoFormulario } from './PreviaDoFormulario'
import { comandoDaPublicacao, linkPublico, publicacaoInicial, type Publicacao } from './publicacao'
import { cartaoDoTipo, tipoDoCartao } from './rotulosDosFormularios'

type Formulario = Esquema<'FormularioDetalheRh'>
type Etapa = 'detalhes' | 'perguntas' | 'verificacao' | 'publicacao'

const ETAPAS = [
  { chave: 'detalhes', rotulo: 'Detalhes' },
  { chave: 'perguntas', rotulo: 'Perguntas' },
  { chave: 'verificacao', rotulo: 'Verificação' },
  { chave: 'publicacao', rotulo: 'Publicação' },
] as const
const CHAVES = ETAPAS.map((e) => e.chave)

function detalhesDe(f: Formulario | undefined): Detalhes {
  return f
    ? {
        titulo: f.titulo,
        descricao: f.descricao ?? '',
        cartao: cartaoDoTipo(f.tipo, f.sentidoAvaliacao),
        anonimo: f.anonimo,
      }
    : { titulo: '', descricao: '', cartao: 'ENTREVISTA', anonimo: false }
}

function errosDeCampo<K extends string>(
  erro: unknown,
  campos: readonly K[],
): Partial<Record<K, string>> {
  const saida: Partial<Record<K, string>> = {}
  if (!(erro instanceof ErroDeApi)) return saida
  for (const [chave, mensagens] of Object.entries(erro.errors)) {
    const campo = (chave.charAt(0).toLowerCase() + chave.slice(1)) as K
    if (campos.includes(campo)) saida[campo] = mensagens.join(' ')
  }
  return saida
}

/** O corpo (detalhes, perguntas, verificação, publicação) depois de o formulário carregar. */
function CorpoDoAssistente({
  formulario,
  etapa,
  irPara,
  fechar,
}: {
  formulario: Formulario | undefined
  etapa: Etapa
  irPara: (etapa: Etapa, id?: number) => void
  fechar: () => void
}) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const [detalhes, setDetalhes] = useState<Detalhes>(() => detalhesDe(formulario))
  const [publicacao, setPublicacao] = useState<Publicacao | null>(() =>
    formulario ? publicacaoInicial(formulario) : null,
  )
  const [errosDetalhes, setErrosDetalhes] = useState<ErrosDosDetalhes>({})
  const [errosPublicacao, setErrosPublicacao] = useState<ErrosDaPublicacao>({})
  const [erroGeral, setErroGeral] = useState<string | null>(null)
  const [publicado, setPublicado] = useState<Formulario | null>(null)
  const [gravando, setGravando] = useState(false)
  const criar = $api.useMutation('post', '/api/rh/formularios')
  const editar = $api.useMutation('put', '/api/rh/formularios/{formularioId}')
  const salvarPub = $api.useMutation('put', '/api/rh/formularios/{formularioId}/publicacao')
  const publicar = $api.useMutation('post', '/api/rh/formularios/{formularioId}/publicar')
  const verificacao = useVerificacaoSeExistir(formulario?.id, etapa === 'verificacao')
  const encerrado = formulario?.situacao === 'ENCERRADO'

  async function atualizar() {
    await invalidarRota(queryClient, '/api/rh/formularios')
  }

  /** Grava a etapa 1: cria na primeira vez, edita nas seguintes. Devolve o id ou null. */
  async function gravarDetalhes(): Promise<number | null> {
    if (encerrado) return formulario?.id ?? null
    const novos: ErrosDosDetalhes = {}
    if (!detalhes.titulo.trim()) novos.titulo = 'Escreva o título.'
    setErrosDetalhes(novos)
    setErroGeral(null)
    if (Object.keys(novos).length) return null
    const { tipo, sentidoAvaliacao } = tipoDoCartao(detalhes.cartao)
    const corpo: Esquema<'FormularioDetalhesComando'> = {
      titulo: detalhes.titulo.trim(),
      descricao: detalhes.descricao.trim() || null,
      tipo,
      sentidoAvaliacao,
      anonimo: tipo === 'OUTRO' ? false : detalhes.anonimo,
    }
    try {
      const salvo = formulario
        ? await editar.mutateAsync({
            params: { path: { formularioId: formulario.id } },
            body: corpo,
          })
        : await criar.mutateAsync({ body: corpo })
      await atualizar()
      return salvo?.id ?? formulario?.id ?? null
    } catch (erro) {
      const campos = errosDeCampo(erro, [
        'titulo',
        'descricao',
        'tipo',
        'anonimo',
        'sentidoAvaliacao',
      ] as const)
      const { sentidoAvaliacao: erroDoSentido, ...resto } = campos
      setErrosDetalhes({ ...resto, ...(erroDoSentido ? { tipo: erroDoSentido } : {}) })
      if (Object.keys(campos).length === 0) setErroGeral(mensagemDeErro(erro))
      return null
    }
  }

  async function gravarPublicacao(): Promise<boolean> {
    if (!formulario || !publicacao || encerrado) return true
    setErrosPublicacao({})
    setErroGeral(null)
    try {
      await salvarPub.mutateAsync({
        params: { path: { formularioId: formulario.id } },
        body: comandoDaPublicacao(publicacao, formulario.tipo),
      })
      await atualizar()
      return true
    } catch (erro) {
      const campos = errosDeCampo(erro, [
        'setorIds',
        'funcionarioIds',
        'prazo',
        'agendarPara',
        'recorrencia',
      ] as const)
      setErrosPublicacao(campos)
      if (Object.keys(campos).length === 0) setErroGeral(mensagemDeErro(erro))
      return false
    }
  }

  async function comTrava(acao: () => Promise<void>) {
    if (gravando) return
    setGravando(true)
    try {
      await acao()
    } finally {
      setGravando(false)
    }
  }

  const salvarESair = () =>
    comTrava(async () => {
      if (etapa === 'detalhes') {
        if ((await gravarDetalhes()) === null) return
      } else if (etapa === 'publicacao') {
        if (!(await gravarPublicacao())) return
        if (
          publicacao?.quando === 'AGENDAR' &&
          publicacao.agendarPara &&
          formulario?.situacao === 'RASCUNHO'
        ) {
          notificacao.sucesso(
            'Rascunho salvo. A data de agendamento só é guardada ao publicar. Escolha de novo ao voltar.',
          )
          fechar()
          return
        }
      }
      notificacao.sucesso('Rascunho salvo.')
      fechar()
    })

  const avancar = () =>
    comTrava(async () => {
      if (etapa === 'detalhes') {
        const id = await gravarDetalhes()
        if (id !== null) irPara('perguntas', id)
      } else if (etapa === 'perguntas') irPara('verificacao')
      else if (etapa === 'verificacao') irPara('publicacao')
    })

  const voltar = () =>
    comTrava(async () => {
      if (etapa === 'publicacao') {
        // A conferência do público mínimo do anônimo olha o público já guardado.
        if (!(await gravarPublicacao())) return
        irPara('verificacao')
      } else if (etapa === 'verificacao') irPara('perguntas')
      else if (etapa === 'perguntas') irPara('detalhes')
    })

  const publicarAgora = () =>
    comTrava(async () => {
      if (!formulario || !publicacao) return
      setErrosPublicacao({})
      setErroGeral(null)
      try {
        const resultado = await publicar.mutateAsync({
          params: { path: { formularioId: formulario.id } },
          body: comandoDaPublicacao(publicacao, formulario.tipo),
        })
        await atualizar()
        if (resultado) {
          setPublicado(resultado)
          notificacao.sucesso(
            resultado.situacao === 'AGENDADO' ? 'Publicação agendada.' : 'Formulário publicado.',
          )
        }
      } catch (erro) {
        const campos = errosDeCampo(erro, [
          'setorIds',
          'funcionarioIds',
          'prazo',
          'agendarPara',
          'recorrencia',
        ] as const)
        setErrosPublicacao(campos)
        if (Object.keys(campos).length === 0) setErroGeral(mensagemDeErro(erro))
      }
    })

  if (publicado) {
    const token = publicado.publicacao.tokenDoLink
    return (
      <Concluido
        formulario={publicado}
        link={token ? linkPublico(token) : null}
        aoFechar={fechar}
      />
    )
  }

  const { tipo, sentidoAvaliacao } = tipoDoCartao(detalhes.cartao)
  const podePublicar = verificacao.data?.podePublicar ?? false
  const legenda =
    etapa === 'publicacao'
      ? [
          publicacao?.prazo
            ? `Aceita respostas até ${formatarDiaMesDaData(publicacao.prazo)}.`
            : null,
          publicacao?.alvo === 'LINK' ? 'O link fica disponível depois de publicar.' : null,
        ]
          .filter(Boolean)
          .join(' ') || 'A prévia muda conforme você preenche.'
      : 'A prévia muda conforme você preenche.'

  const rodape = (
    <div className="flex w-full items-center justify-between gap-3">
      {encerrado ? (
        <Botao variante="texto" onClick={fechar}>
          Fechar
        </Botao>
      ) : (
        <Botao variante="texto" onClick={() => void salvarESair()} disabled={gravando}>
          Salvar rascunho e sair
        </Botao>
      )}
      <div className="flex gap-3">
        {etapa !== 'detalhes' && (
          <Botao variante="secundario" onClick={() => void voltar()} disabled={gravando}>
            Voltar
          </Botao>
        )}
        {etapa === 'publicacao' ? (
          <Botao
            onClick={() => void publicarAgora()}
            carregando={publicar.isPending}
            disabled={encerrado || formulario?.situacao === 'PUBLICADO' || gravando}
          >
            Publicar formulário
          </Botao>
        ) : (
          <Botao
            onClick={() => void avancar()}
            carregando={gravando && etapa === 'detalhes'}
            disabled={etapa === 'verificacao' && !podePublicar}
          >
            Próximo
          </Botao>
        )}
      </div>
    </div>
  )

  return (
    <>
      <div className="grid grid-cols-[minmax(0,1.3fr)_minmax(0,1fr)] gap-7">
        <div className="flex flex-col gap-4">
          {erroGeral && <Aviso tom="erro">{erroGeral}</Aviso>}
          {etapa === 'detalhes' && (
            <EtapaDetalhes
              valores={detalhes}
              aoMudar={setDetalhes}
              erros={errosDetalhes}
              formulario={formulario}
            />
          )}
          {etapa === 'perguntas' && formulario && <EtapaPerguntas formulario={formulario} />}
          {etapa === 'verificacao' && formulario && (
            <EtapaVerificacao formularioId={formulario.id} />
          )}
          {etapa === 'publicacao' && formulario && publicacao && (
            <EtapaPublicacao
              formulario={formulario}
              valores={publicacao}
              aoMudar={setPublicacao}
              erros={errosPublicacao}
            />
          )}
        </div>
        <PreviaDoFormulario
          titulo={detalhes.titulo}
          descricao={detalhes.descricao}
          tipo={tipo}
          sentido={sentidoAvaliacao}
          perguntas={formulario?.perguntas ?? []}
          legenda={legenda}
        />
      </div>
      <div className="mt-6 border-t border-divisor pt-4">{rodape}</div>
    </>
  )
}

function useVerificacaoSeExistir(id: number | undefined, ativa: boolean) {
  // A conferência só é pedida na etapa 3 (e lida para ligar o "Próximo").
  return useVerificacao(id ?? 0, ativa && id !== undefined)
}

function Concluido({
  formulario,
  link,
  aoFechar,
}: {
  formulario: Formulario
  link: string | null
  aoFechar: () => void
}) {
  const notificacao = useNotificacao()
  return (
    <div className="flex flex-col gap-4">
      <Aviso tom="sucesso">
        {formulario.situacao === 'AGENDADO'
          ? `"${formulario.titulo}" foi agendado.`
          : `"${formulario.titulo}" foi publicado.`}
      </Aviso>
      {link && (
        <div className="flex flex-col gap-2">
          <span className="text-tabela font-semibold text-texto">Link para os candidatos</span>
          <div className="flex items-center gap-3">
            <code className="flex-1 truncate rounded-campo bg-superficie-suave px-3 py-2 text-tabela">
              {link}
            </code>
            <Botao
              variante="secundario"
              onClick={() => {
                void navigator.clipboard
                  ?.writeText(link)
                  .then(() => notificacao.sucesso('Link copiado.'))
                  .catch(() =>
                    notificacao.erro('Não foi possível copiar. Selecione o link e copie.'),
                  )
              }}
            >
              Copiar link
            </Botao>
          </div>
        </div>
      )}
      <div className="flex justify-end">
        <Botao onClick={aoFechar}>Fechar</Botao>
      </div>
    </div>
  )
}

/** Telas 17 a 20: o assistente em /rh/formularios/novo e /rh/formularios/:formularioId/editar. */
export function AssistenteDoFormulario() {
  const navegar = useNavigate()
  const { search } = useLocation()
  const [parametros] = useSearchParams()
  const formularioId = inteiroDaUrl(useParams().formularioId)
  const editando = useParams().formularioId !== undefined
  const etapa: Etapa = valorDeEnum(parametros.get('etapa') ?? undefined, CHAVES) ?? 'detalhes'
  const consulta = $api.useQuery(
    'get',
    '/api/rh/formularios/{formularioId}',
    { params: { path: { formularioId: formularioId ?? 0 } } },
    { enabled: formularioId !== undefined },
  )
  const formulario = consulta.data

  function fechar() {
    const p = new URLSearchParams(search)
    p.delete('etapa')
    const texto = p.toString()
    navegar({ pathname: '/rh/formularios', search: texto ? `?${texto}` : '' })
  }

  function irPara(proxima: Etapa, id?: number) {
    const p = new URLSearchParams(search)
    p.set('etapa', proxima)
    const destino = id ?? formularioId
    navegar({ pathname: `/rh/formularios/${destino}/editar`, search: `?${p.toString()}` })
  }

  // Sem formulário criado, só a etapa Detalhes existe.
  const etapaMostrada: Etapa = !editando ? 'detalhes' : etapa
  const carregando = editando && !formulario
  const travas = travasDosDetalhes(formulario)
  return (
    <Janela
      aberta
      aoFechar={fechar}
      titulo={editando ? 'Editar formulário' : 'Novo formulário'}
      tamanho="extra"
    >
      <div className="flex flex-col gap-6">
        <Etapas etapas={ETAPAS} atual={etapaMostrada} />
        {travas.tudo && (
          <Aviso tom="info">Formulário encerrado: o assistente abre só para leitura.</Aviso>
        )}
        {editando && consulta.error ? (
          <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
        ) : carregando ? (
          <div role="status" aria-label="Carregando o formulário" className="flex flex-col gap-3">
            <Esqueleto className="h-10 w-full" />
            <Esqueleto className="h-60 w-full" />
          </div>
        ) : (
          <CorpoDoAssistente
            key={formulario?.id ?? 'novo'}
            formulario={formulario}
            etapa={etapaMostrada}
            irPara={irPara}
            fechar={fechar}
          />
        )}
      </div>
    </Janela>
  )
}
