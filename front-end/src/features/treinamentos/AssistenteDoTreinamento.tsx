import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useLocation, useNavigate, useParams, useSearchParams } from 'react-router'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { formatarDiaMesDaData } from '../../shared/formato/datas'
import { plural } from '../../shared/formato/texto'
import { inteiroDaUrl, valorDeEnum } from '../../shared/lista/valoresDeLista'
import { Aviso } from '../../shared/ui/Aviso'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Etapas } from '../../shared/ui/Etapas'
import { Janela } from '../../shared/ui/Janela'
import { Markdown } from '../../shared/ui/Markdown'
import { Progresso } from '../../shared/ui/Progresso'
import { Selo } from '../../shared/ui/Selo'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { useEmpresa } from '../../app/useEmpresa'
import { useContagemDoPublico } from './consultasDoTreinamento'
import {
  EtapaCertificadoDoTreinamento,
  EtapaConteudoDoTreinamento,
  EtapaDetalhesDoTreinamento,
  EtapaPublicacaoDoTreinamento,
  type Erros,
} from './EtapasDoTreinamento'
import { comandoDe, rascunhoDe, travasDoTreinamento, type Rascunho } from './regrasDoTreinamento'
import { formatarDuracao, ROTULO_DO_MODULO } from './rotulosDosTreinamentos'

type Treinamento = Esquema<'TreinamentoRh'>
type Etapa = 'detalhes' | 'conteudo' | 'certificado' | 'publicacao'

const ETAPAS = [
  { chave: 'detalhes', rotulo: 'Detalhes' },
  { chave: 'conteudo', rotulo: 'Conteúdo' },
  { chave: 'certificado', rotulo: 'Certificado' },
  { chave: 'publicacao', rotulo: 'Publicação' },
] as const
const CHAVES = ETAPAS.map((e) => e.chave)
const ORDEM: Etapa[] = ['detalhes', 'conteudo', 'certificado', 'publicacao']

function errosDe(erro: unknown): Erros {
  const saida: Erros = {}
  if (erro instanceof ErroDeApi)
    for (const [chave, msgs] of Object.entries(erro.errors))
      saida[chave.charAt(0).toLowerCase() + chave.slice(1)] = msgs.join(' ')
  return saida
}

/** A prévia "Como o colaborador vai ver" (montada no front). */
function PreviaDoTreinamento({
  valores,
  modulos,
}: {
  valores: Rascunho
  modulos: Treinamento['modulos']
}) {
  const empresa = useEmpresa() ?? '[Nome da empresa]'
  const total = modulos.reduce((s, m) => s + (m.duracaoMinutos ?? 0), 0)
  const todasComDuracao = modulos.length > 0 && modulos.every((m) => m.duracaoMinutos)
  return (
    <section aria-label="Prévia" className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <span className="text-tabela font-semibold text-texto">Como o colaborador vai ver</span>
        <Selo tom="info">Prévia</Selo>
      </div>
      <div className="flex flex-col gap-3 rounded-cartao border border-borda bg-superficie p-5">
        <span className="text-tabela font-semibold text-texto">{empresa}</span>
        <span className="flex flex-wrap gap-2">
          <Selo tom={valores.obrigatorio ? 'alerta' : 'info'}>
            {valores.onboarding ? 'Onboarding' : valores.obrigatorio ? 'Obrigatório' : 'Opcional'}
          </Selo>
          {valores.comecaEm && (
            <Selo tom="contorno">Começa em {formatarDiaMesDaData(valores.comecaEm)}</Selo>
          )}
        </span>
        <p className="text-t3 font-bold text-texto">{valores.titulo || 'Título do treinamento'}</p>
        {valores.descricao && (
          <Markdown texto={valores.descricao} className="text-tabela text-texto-suave" />
        )}
        <p className="text-pequeno text-texto-suave">
          {modulos.length} {plural(modulos.length, 'módulo', 'módulos')}
          {todasComDuracao ? `, ${formatarDuracao(total)}` : ''}
        </p>
        <Progresso valor={0} rotulo="Progresso da prévia" />
        <ol className="flex flex-col gap-1 text-tabela text-texto">
          {[...modulos]
            .sort((a, b) => a.ordem - b.ordem)
            .map((m, i) => (
              <li key={m.id}>
                {i + 1}. {m.titulo}{' '}
                <span className="text-texto-suave">({ROTULO_DO_MODULO[m.tipo]})</span>
              </li>
            ))}
          {valores.emiteCertificado && <li>Certificado</li>}
        </ol>
        <div className="flex justify-end">
          <Botao tamanho="pequeno" disabled>
            Começar
          </Botao>
        </div>
      </div>
      <p className="text-pequeno text-texto-suave">A prévia muda conforme você preenche.</p>
    </section>
  )
}

function CorpoDoAssistente({
  treinamento,
  etapa,
  irPara,
  fechar,
}: {
  treinamento: Treinamento | undefined
  etapa: Etapa
  irPara: (e: Etapa, id?: number) => void
  fechar: () => void
}) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const [valores, setValores] = useState<Rascunho>(() => rascunhoDe(treinamento))
  const [quando, setQuando] = useState<'PUBLICAR' | 'RASCUNHO'>('PUBLICAR')
  const [erros, setErros] = useState<Erros>({})
  const [erroGeral, setErroGeral] = useState<string | null>(null)
  const [gravando, setGravando] = useState(false)
  const criar = $api.useMutation('post', '/api/rh/treinamentos')
  const atualizar = $api.useMutation('put', '/api/rh/treinamentos/{treinamentoId}')
  const publicar = $api.useMutation('post', '/api/rh/treinamentos/{treinamentoId}/publicar')
  const contagem = useContagemDoPublico(valores, etapa === 'publicacao')
  const t = travasDoTreinamento(treinamento)

  /** Grava o comando inteiro (cria na primeira vez). Devolve o id ou null. */
  async function gravar(): Promise<number | null> {
    if (t.encerrado) return treinamento?.id ?? null
    const novos: Erros = {}
    if (valores.titulo.trim().length < 3) novos.titulo = 'O título tem de 3 a 200 caracteres.'
    setErros(novos)
    setErroGeral(null)
    if (Object.keys(novos).length) return null
    try {
      const salvo = treinamento
        ? await atualizar.mutateAsync({
            params: { path: { treinamentoId: treinamento.id } },
            body: comandoDe(valores),
          })
        : await criar.mutateAsync({ body: comandoDe(valores) })
      await invalidarRota(queryClient, '/api/rh/treinamentos')
      return salvo?.id ?? treinamento?.id ?? null
    } catch (erro) {
      const campos = errosDe(erro)
      setErros(campos)
      if (Object.keys(campos).length === 0) setErroGeral(mensagemDeErro(erro))
      return null
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

  const indice = ORDEM.indexOf(etapa)
  const salvarESair = () =>
    comTrava(async () => {
      if ((await gravar()) === null) return
      notificacao.sucesso('Rascunho salvo.')
      fechar()
    })
  const avancar = () =>
    comTrava(async () => {
      const id = etapa === 'conteudo' ? (treinamento?.id ?? null) : await gravar()
      if (id !== null) irPara(ORDEM[indice + 1]!, id)
    })
  const voltar = () =>
    comTrava(async () => {
      if (etapa !== 'conteudo' && (await gravar()) === null) return
      irPara(ORDEM[indice - 1]!)
    })
  const concluir = () =>
    comTrava(async () => {
      const id = await gravar()
      if (id === null || !treinamento) return
      if (quando === 'RASCUNHO') {
        notificacao.sucesso('Rascunho salvo.')
        fechar()
        return
      }
      try {
        const publicado = await publicar.mutateAsync({ params: { path: { treinamentoId: id } } })
        await invalidarRota(queryClient, '/api/rh/treinamentos')
        notificacao.sucesso(
          publicado?.situacao === 'AGENDADO' && publicado.comecaEm
            ? `Treinamento publicado: aparece em Meus treinamentos a partir de ${formatarDiaMesDaData(publicado.comecaEm)}. Ninguém foi avisado por e-mail; para avisar, publique um Aviso.`
            : 'Treinamento publicado. Ninguém foi avisado por e-mail; para avisar, publique um Aviso.',
        )
        fechar()
      } catch (erro) {
        const campos = errosDe(erro)
        setErros(campos)
        setErroGeral(
          Object.keys(campos).length
            ? Object.values(campos).filter(Boolean).join(' ')
            : mensagemDeErro(erro),
        )
      }
    })

  const textoDoBotao = t.publicado
    ? 'Salvar alterações'
    : quando === 'RASCUNHO'
      ? 'Salvar rascunho'
      : 'Publicar treinamento'
  return (
    <>
      <div className="grid grid-cols-[minmax(0,1.3fr)_minmax(0,1fr)] gap-7">
        <div className="flex flex-col gap-4">
          {erroGeral && <Aviso tom="erro">{erroGeral}</Aviso>}
          {etapa === 'detalhes' && (
            <EtapaDetalhesDoTreinamento
              valores={valores}
              aoMudar={setValores}
              erros={erros}
              treinamento={treinamento}
            />
          )}
          {etapa === 'conteudo' && treinamento && (
            <EtapaConteudoDoTreinamento treinamento={treinamento} />
          )}
          {etapa === 'certificado' && treinamento && (
            <EtapaCertificadoDoTreinamento
              valores={valores}
              aoMudar={setValores}
              treinamento={treinamento}
            />
          )}
          {etapa === 'publicacao' && treinamento && (
            <EtapaPublicacaoDoTreinamento
              valores={valores}
              aoMudar={setValores}
              treinamento={treinamento}
              quando={quando}
              aoMudarQuando={setQuando}
              total={contagem.data?.total}
              erros={erros}
            />
          )}
        </div>
        <PreviaDoTreinamento valores={valores} modulos={treinamento?.modulos ?? []} />
      </div>
      <div className="mt-6 flex items-center justify-between gap-3 border-t border-divisor pt-4">
        {t.encerrado ? (
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
            <Botao onClick={() => void concluir()} carregando={gravando} disabled={t.encerrado}>
              {textoDoBotao}
            </Botao>
          ) : (
            <Botao onClick={() => void avancar()} carregando={gravando}>
              Próximo
            </Botao>
          )}
        </div>
      </div>
    </>
  )
}

/** Telas 23 a 26: o assistente em /rh/treinamentos/novo e /rh/treinamentos/:treinamentoId/editar. */
export function AssistenteDoTreinamento() {
  const navegar = useNavigate()
  const { search } = useLocation()
  const [parametros] = useSearchParams()
  const id = inteiroDaUrl(useParams().treinamentoId)
  const editando = useParams().treinamentoId !== undefined
  const etapa: Etapa = editando
    ? (valorDeEnum(parametros.get('etapa') ?? undefined, CHAVES) ?? 'detalhes')
    : 'detalhes'
  const consulta = $api.useQuery(
    'get',
    '/api/rh/treinamentos/{treinamentoId}',
    { params: { path: { treinamentoId: id ?? 0 } } },
    { enabled: id !== undefined },
  )
  const treinamento = consulta.data

  function fechar() {
    const p = new URLSearchParams(search)
    p.delete('etapa')
    const texto = p.toString()
    navegar({ pathname: '/rh/treinamentos', search: texto ? `?${texto}` : '' })
  }
  function irPara(proxima: Etapa, novoId?: number) {
    const p = new URLSearchParams(search)
    p.set('etapa', proxima)
    navegar({ pathname: `/rh/treinamentos/${novoId ?? id}/editar`, search: `?${p.toString()}` })
  }

  return (
    <Janela
      aberta
      aoFechar={fechar}
      titulo={editando ? 'Editar treinamento' : 'Novo treinamento'}
      tamanho="extra"
    >
      <div className="flex flex-col gap-6">
        <Etapas etapas={ETAPAS} atual={etapa} />
        {treinamento?.situacao === 'ENCERRADO' && (
          <Aviso tom="info">Este treinamento foi encerrado e não pode mais ser alterado.</Aviso>
        )}
        {editando && consulta.error ? (
          <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
        ) : editando && !treinamento ? (
          <div role="status" aria-label="Carregando o treinamento" className="flex flex-col gap-3">
            <Esqueleto className="h-10 w-full" />
            <Esqueleto className="h-60 w-full" />
          </div>
        ) : (
          <CorpoDoAssistente
            key={treinamento?.id ?? 'novo'}
            treinamento={treinamento}
            etapa={etapa}
            irPara={irPara}
            fechar={fechar}
          />
        )}
      </div>
    </Janela>
  )
}
