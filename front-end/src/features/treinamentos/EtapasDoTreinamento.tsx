import { useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, X } from 'lucide-react'
import { useState } from 'react'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { formatarDiaMesDaData, hojeEmSaoPaulo } from '../../shared/formato/datas'
import { plural } from '../../shared/formato/texto'
import { AreaDeTexto } from '../../shared/ui/AreaDeTexto'
import { Aviso } from '../../shared/ui/Aviso'
import { Botao } from '../../shared/ui/Botao'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'
import { Campo } from '../../shared/ui/Campo'
import { ComboDeBusca } from '../../shared/ui/ComboDeBusca'
import { Entrada } from '../../shared/ui/Entrada'
import { OpcoesEmCartao } from '../../shared/ui/OpcoesEmCartao'
import { SeletorDeArquivo } from '../../shared/ui/SeletorDeArquivo'
import { useBuscaDeColaborador } from '../ponto/useBuscaDeColaborador'
import { adicionarModulo, atualizarModulo } from './enviarModulo'
import {
  DICA_DO_PUBLICADO,
  DICA_DOS_MODULOS,
  travasDoTreinamento,
  type Rascunho,
} from './regrasDoTreinamento'
import {
  linhaDoModulo,
  ROTULO_DO_MODULO,
  TAMANHO_DO_PDF_MB,
  TIPOS_DE_MODULO,
} from './rotulosDosTreinamentos'

type Treinamento = Esquema<'TreinamentoRh'>
type Modulo = Esquema<'TreinamentoModuloRh'>
type ModuloTipo = Esquema<'TreinamentoModuloTipo'>
type Publico = Esquema<'TreinamentoPublico'>

export type Erros = Record<string, string | undefined>

/** Tela 23: Detalhes (sem "Tipo" e sem "Termina em"; com a caixa do onboarding). */
export function EtapaDetalhesDoTreinamento({
  valores,
  aoMudar,
  erros,
  treinamento,
}: {
  valores: Rascunho
  aoMudar: (v: Rascunho) => void
  erros: Erros
  treinamento: Treinamento | undefined
}) {
  const t = travasDoTreinamento(treinamento)
  const travado = t.encerrado || t.publicado
  return (
    <div className="flex flex-col gap-5">
      <h2 className="text-t3 font-bold text-texto">Detalhes</h2>
      {t.publicado && !t.encerrado && (
        <p className="text-pequeno text-texto-suave">{DICA_DO_PUBLICADO}</p>
      )}
      <Campo rotulo="Título" erro={erros.titulo}>
        <Entrada
          maxLength={200}
          value={valores.titulo}
          disabled={t.encerrado}
          onChange={(e) => aoMudar({ ...valores, titulo: e.target.value })}
        />
      </Campo>
      <Campo rotulo="Descrição" erro={erros.descricao}>
        <AreaDeTexto
          rows={4}
          limite={2000}
          value={valores.descricao ?? ''}
          disabled={t.encerrado}
          onChange={(e) => aoMudar({ ...valores, descricao: e.target.value })}
        />
      </Campo>
      <div className="w-60">
        <Campo
          rotulo="Começa em"
          dica="Só libera o acesso; data futura deixa o treinamento agendado."
          erro={erros.comecaEm}
        >
          <Entrada
            type="date"
            lang="pt-BR"
            value={valores.comecaEm ?? ''}
            disabled={t.encerrado || !t.comecaEmLivre}
            onChange={(e) => aoMudar({ ...valores, comecaEm: e.target.value })}
          />
        </Campo>
      </div>
      <CaixaDeSelecao
        marcada={valores.obrigatorio}
        aoMudar={(obrigatorio) => aoMudar({ ...valores, obrigatorio })}
        desabilitada={travado}
        rotulo="Obrigatório"
        descricao="Quem não concluir aparece como pendente para o RH e para o gestor enquanto o treinamento estiver em andamento."
      />
      <CaixaDeSelecao
        marcada={valores.onboarding}
        aoMudar={(onboarding) => aoMudar({ ...valores, onboarding })}
        desabilitada={travado}
        rotulo="É o treinamento de onboarding"
        descricao="Quem for cadastrado pode ser incluído nele."
      />
    </div>
  )
}

type EdicaoDoModulo = {
  id?: number
  tipo: ModuloTipo
  titulo: string
  duracao: string
  urlExterna: string
  conteudoTexto: string
  arquivo: File | null
  nomeArquivo: string | null
}

function EditorDeModulo({
  treinamentoId,
  edicao,
  publicado,
  aoFechar,
}: {
  treinamentoId: number
  edicao: EdicaoDoModulo
  publicado: boolean
  aoFechar: () => void
}) {
  const queryClient = useQueryClient()
  const [v, setV] = useState(edicao)
  const [erros, setErros] = useState<Erros>({})
  const [salvando, setSalvando] = useState(false)
  const excluir = $api.useMutation(
    'delete',
    '/api/rh/treinamentos/{treinamentoId}/modulos/{moduloId}',
  )

  async function salvar() {
    if (salvando) return
    const novos: Erros = {}
    if (!v.titulo.trim()) novos.titulo = 'Escreva o título do módulo.'
    if ((v.tipo === 'VIDEO' || v.tipo === 'LINK') && !/^https?:\/\//.test(v.urlExterna.trim()))
      novos.urlExterna = 'O endereço precisa começar com http:// ou https://.'
    if (v.tipo === 'TEXTO' && !v.conteudoTexto.trim())
      novos.conteudoTexto = 'Escreva o texto do módulo.'
    if (v.tipo === 'PDF' && !v.arquivo && !v.nomeArquivo) novos.arquivo = 'Escolha o PDF.'
    const duracao = v.duracao ? Number(v.duracao) : null
    if (duracao !== null && (!Number.isInteger(duracao) || duracao < 1 || duracao > 32767))
      novos.duracaoMinutos = 'Use um número de minutos de 1 a 32767.'
    setErros(novos)
    if (Object.keys(novos).length) return
    setSalvando(true)
    const campos = {
      titulo: v.titulo.trim(),
      tipo: v.tipo,
      duracaoMinutos: duracao,
      urlExterna: v.urlExterna.trim() || null,
      conteudoTexto: v.conteudoTexto.trim() || null,
      arquivo: v.arquivo,
    }
    try {
      if (v.id === undefined) await adicionarModulo(treinamentoId, campos)
      else await atualizarModulo(treinamentoId, v.id, campos)
      await invalidarRota(queryClient, '/api/rh/treinamentos/{treinamentoId}')
      aoFechar()
    } catch (e) {
      if (e instanceof ErroDeApi && Object.keys(e.errors).length) {
        const deCampo: Erros = {}
        for (const [chave, msgs] of Object.entries(e.errors))
          deCampo[chave.charAt(0).toLowerCase() + chave.slice(1)] = msgs.join(' ')
        setErros(deCampo)
      } else setErros({ geral: mensagemDeErro(e) })
    } finally {
      setSalvando(false)
    }
  }

  async function remover() {
    if (v.id === undefined) return
    try {
      await excluir.mutateAsync({ params: { path: { treinamentoId, moduloId: v.id } } })
      await invalidarRota(queryClient, '/api/rh/treinamentos/{treinamentoId}')
      aoFechar()
    } catch (e) {
      setErros({ geral: mensagemDeErro(e) })
    }
  }

  return (
    <section
      aria-label={v.id === undefined ? 'Novo módulo' : 'Editar módulo'}
      className="flex flex-col gap-4 rounded-cartao border-2 border-primaria bg-superficie p-5"
    >
      <p className="text-tabela font-semibold text-texto">{ROTULO_DO_MODULO[v.tipo]}</p>
      <Campo rotulo="Título do módulo" erro={erros.titulo}>
        <Entrada
          maxLength={200}
          value={v.titulo}
          onChange={(e) => setV({ ...v, titulo: e.target.value })}
        />
      </Campo>
      <div className="w-48">
        <Campo rotulo="Duração em minutos" erro={erros.duracaoMinutos}>
          <Entrada
            type="number"
            min={1}
            max={32767}
            value={v.duracao}
            onChange={(e) => setV({ ...v, duracao: e.target.value })}
          />
        </Campo>
      </div>
      {(v.tipo === 'VIDEO' || v.tipo === 'LINK') && (
        <Campo
          rotulo={v.tipo === 'VIDEO' ? 'Endereço do vídeo' : 'Endereço do link'}
          dica={
            v.tipo === 'VIDEO'
              ? 'O vídeo abre em outra aba, para quem faz o treinamento.'
              : undefined
          }
          erro={erros.urlExterna}
        >
          <Entrada
            type="url"
            value={v.urlExterna}
            onChange={(e) => setV({ ...v, urlExterna: e.target.value })}
          />
        </Campo>
      )}
      {v.tipo === 'TEXTO' && (
        <Campo rotulo="Texto do módulo" erro={erros.conteudoTexto}>
          <AreaDeTexto
            rows={8}
            limite={50000}
            value={v.conteudoTexto}
            onChange={(e) => setV({ ...v, conteudoTexto: e.target.value })}
          />
        </Campo>
      )}
      {v.tipo === 'PDF' && (
        <div className="flex flex-col gap-2">
          {v.nomeArquivo && !v.arquivo && (
            <p className="text-pequeno text-texto-suave">
              Arquivo atual: {v.nomeArquivo}. Sem arquivo novo, ele fica.
            </p>
          )}
          <SeletorDeArquivo
            arquivo={v.arquivo}
            aoMudar={(arquivo) => setV({ ...v, arquivo })}
            tiposAceitos={['application/pdf']}
            tamanhoMaximoMb={TAMANHO_DO_PDF_MB}
            titulo="Arraste o PDF aqui"
            textoDeAjuda="Só PDF, até 10 MB"
            erro={erros.arquivo}
          />
        </div>
      )}
      {erros.geral && <Aviso tom="erro">{erros.geral}</Aviso>}
      <div className="flex flex-wrap justify-between gap-3">
        <div>
          {v.id !== undefined && !publicado && (
            <Botao variante="perigo" tamanho="pequeno" onClick={() => void remover()}>
              Remover módulo
            </Botao>
          )}
        </div>
        <div className="flex gap-3">
          <Botao variante="secundario" tamanho="pequeno" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao tamanho="pequeno" onClick={() => void salvar()} carregando={salvando}>
            Salvar módulo
          </Botao>
        </div>
      </div>
    </section>
  )
}

function edicaoDe(m: Modulo): EdicaoDoModulo {
  return {
    id: m.id,
    tipo: m.tipo,
    titulo: m.titulo,
    duracao: m.duracaoMinutos ? String(m.duracaoMinutos) : '',
    urlExterna: m.urlExterna ?? '',
    conteudoTexto: m.conteudoTexto ?? '',
    arquivo: null,
    nomeArquivo: m.nomeArquivo,
  }
}

/** Tela 24: Conteúdo (Vídeo, Arquivo PDF, Texto e Link; sem "Aula presencial"). */
export function EtapaConteudoDoTreinamento({ treinamento }: { treinamento: Treinamento }) {
  const queryClient = useQueryClient()
  const [edicao, setEdicao] = useState<EdicaoDoModulo | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const reordenar = $api.useMutation('put', '/api/rh/treinamentos/{treinamentoId}/modulos/ordem')
  const t = travasDoTreinamento(treinamento)
  const modulos = [...treinamento.modulos].sort((a, b) => a.ordem - b.ordem)

  async function mover(indice: number, para: number) {
    const ids = modulos.map((m) => m.id)
    const [tirado] = ids.splice(indice, 1)
    ids.splice(para, 0, tirado!)
    setErro(null)
    try {
      await reordenar.mutateAsync({
        params: { path: { treinamentoId: treinamento.id } },
        body: { moduloIds: ids },
      })
      await invalidarRota(queryClient, '/api/rh/treinamentos/{treinamentoId}')
    } catch (e) {
      setErro(mensagemDeErro(e))
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <h2 className="text-t3 font-bold text-texto">Conteúdo</h2>
      {t.publicado && <Aviso tom="info">{DICA_DOS_MODULOS}</Aviso>}
      {modulos.length === 0 && !edicao && (
        <p className="text-corpo text-texto-suave">Nenhum módulo ainda. Adicione o primeiro.</p>
      )}
      <ol aria-label="Módulos do treinamento" className="flex flex-col gap-3">
        {modulos.map((m, i) =>
          edicao?.id === m.id ? (
            <li key={m.id}>
              <EditorDeModulo
                treinamentoId={treinamento.id}
                edicao={edicao}
                publicado={t.publicado}
                aoFechar={() => setEdicao(null)}
              />
            </li>
          ) : (
            <li
              key={m.id}
              className="flex items-center justify-between gap-3 rounded-cartao border border-borda bg-superficie p-4"
            >
              <span className="flex flex-col">
                <span className="font-semibold text-texto">
                  {i + 1}. {m.titulo}
                </span>
                <span className="text-pequeno text-texto-suave">{linhaDoModulo(m)}</span>
              </span>
              {!t.encerrado && (
                <span className="flex gap-1">
                  {!t.publicado && (
                    <>
                      <Botao
                        variante="texto"
                        tamanho="pequeno"
                        icone={ArrowUp}
                        aria-label={`Subir o módulo ${i + 1}`}
                        disabled={i === 0 || reordenar.isPending}
                        onClick={() => void mover(i, i - 1)}
                      />
                      <Botao
                        variante="texto"
                        tamanho="pequeno"
                        icone={ArrowDown}
                        aria-label={`Descer o módulo ${i + 1}`}
                        disabled={i === modulos.length - 1 || reordenar.isPending}
                        onClick={() => void mover(i, i + 1)}
                      />
                    </>
                  )}
                  <Botao
                    variante="secundario"
                    tamanho="pequeno"
                    aria-label={`Editar o módulo ${i + 1}`}
                    onClick={() => setEdicao(edicaoDe(m))}
                  >
                    Editar
                  </Botao>
                </span>
              )}
            </li>
          ),
        )}
      </ol>
      {erro && <Aviso tom="erro">{erro}</Aviso>}
      {edicao && edicao.id === undefined && (
        <EditorDeModulo
          treinamentoId={treinamento.id}
          edicao={edicao}
          publicado={t.publicado}
          aoFechar={() => setEdicao(null)}
        />
      )}
      {!t.publicado && !edicao && (
        <div className="flex flex-col gap-3 rounded-cartao border border-dashed border-tracejado p-4">
          <span className="text-tabela font-semibold text-texto">Adicionar módulo:</span>
          <div className="flex flex-wrap gap-2">
            {TIPOS_DE_MODULO.map((tipo) => (
              <Botao
                key={tipo}
                variante="secundario"
                tamanho="pequeno"
                onClick={() =>
                  setEdicao({
                    tipo,
                    titulo: '',
                    duracao: '',
                    urlExterna: '',
                    conteudoTexto: '',
                    arquivo: null,
                    nomeArquivo: null,
                  })
                }
              >
                {ROTULO_DO_MODULO[tipo]}
              </Botao>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

/** Tela 25 (a etapa vira "Certificado"): sem quiz e sem nota. */
export function EtapaCertificadoDoTreinamento({
  valores,
  aoMudar,
  treinamento,
}: {
  valores: Rascunho
  aoMudar: (v: Rascunho) => void
  treinamento: Treinamento
}) {
  const t = travasDoTreinamento(treinamento)
  const quantos = treinamento.modulos.length
  return (
    <div className="flex flex-col gap-5">
      <h2 className="text-t3 font-bold text-texto">Certificado</h2>
      <CaixaDeSelecao
        marcada={valores.emiteCertificado}
        aoMudar={(emiteCertificado) => aoMudar({ ...valores, emiteCertificado })}
        desabilitada={t.publicado}
        rotulo="Emitir certificado com código de validação ao concluir"
        descricao={t.publicado ? 'Depois de publicado, o certificado não muda.' : undefined}
      />
      {quantos > 0 ? (
        <Aviso tom="sucesso">
          {quantos} {plural(quantos, 'módulo com conteúdo', 'módulos com conteúdo')}
        </Aviso>
      ) : (
        <Aviso tom="alerta">Inclua pelo menos um módulo para poder publicar</Aviso>
      )}
    </div>
  )
}

function SetoresDoPublico({
  valor,
  aoMudar,
  desabilitado,
}: {
  valor: readonly number[]
  aoMudar: (ids: number[]) => void
  desabilitado: boolean
}) {
  const setores = $api.useQuery('get', '/api/rh/setores', {
    params: { query: { pagina: 1, tamanhoPagina: 100 } },
  })
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="text-tabela font-semibold text-texto">Setores</legend>
      {(setores.data?.itens ?? []).map((s) => (
        <CaixaDeSelecao
          key={s.id}
          rotulo={s.nome}
          marcada={valor.includes(s.id)}
          desabilitada={desabilitado}
          aoMudar={(m) => aoMudar(m ? [...valor, s.id] : valor.filter((id) => id !== s.id))}
        />
      ))}
    </fieldset>
  )
}

function PessoasDoPublico({
  valor,
  aoMudar,
  desabilitado,
}: {
  valor: readonly { id: number; nome: string }[]
  aoMudar: (p: { id: number; nome: string }[]) => void
  desabilitado: boolean
}) {
  const busca = useBuscaDeColaborador()
  return (
    <div className="flex flex-col gap-2">
      <Campo rotulo="Pessoas">
        <ComboDeBusca
          rotulo="Pessoas"
          placeholder="Buscar pessoa"
          opcoes={busca.opcoes}
          valor={null}
          aoDigitar={busca.aoDigitar}
          carregando={busca.carregando}
          aoMudar={(id) => {
            const o = busca.opcoes.find((x) => x.valor === id)
            if (o && !valor.some((p) => p.id === Number(o.valor)) && valor.length < 500)
              aoMudar([...valor, { id: Number(o.valor), nome: o.rotulo }])
          }}
        />
      </Campo>
      {valor.length > 0 && (
        <ul aria-label="Pessoas escolhidas" className="flex flex-wrap gap-2">
          {valor.map((p) => (
            <li
              key={p.id}
              className="flex items-center gap-1 rounded-full bg-superficie-destaque px-3 py-1 text-tabela text-texto"
            >
              {p.nome}
              <button
                type="button"
                disabled={desabilitado}
                aria-label={`Tirar ${p.nome}`}
                onClick={() => aoMudar(valor.filter((x) => x.id !== p.id))}
                className="flex h-5 w-5 items-center justify-center rounded-full text-texto-suave hover:bg-borda"
              >
                <X size={12} aria-hidden="true" />
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

/** Tela 26: Publicação (sem "Avisar por e-mail" e sem lembrete; sem "por cargo"). */
export function EtapaPublicacaoDoTreinamento({
  valores,
  aoMudar,
  treinamento,
  quando,
  aoMudarQuando,
  total,
  erros,
}: {
  valores: Rascunho
  aoMudar: (v: Rascunho) => void
  treinamento: Treinamento
  quando: 'PUBLICAR' | 'RASCUNHO'
  aoMudarQuando: (q: 'PUBLICAR' | 'RASCUNHO') => void
  total: number | undefined
  erros: Erros
}) {
  const t = travasDoTreinamento(treinamento)
  const hoje = hojeEmSaoPaulo()
  const futuro = !!valores.comecaEm && valores.comecaEm > hoje
  const acesso = futuro
    ? `com acesso liberado em ${formatarDiaMesDaData(valores.comecaEm)}`
    : 'com acesso liberado agora'
  const pessoas = total === undefined ? null : `${total} ${plural(total, 'pessoa', 'pessoas')}`
  return (
    <div className="flex flex-col gap-5">
      <h2 className="text-t3 font-bold text-texto">Publicação</h2>
      <OpcoesEmCartao<Publico>
        rotulo={valores.obrigatorio ? 'Quem deve fazer' : 'Quem pode se inscrever'}
        colunas={3}
        valor={valores.publico ?? 'TODOS'}
        aoMudar={(publico) => aoMudar({ ...valores, publico })}
        opcoes={[
          {
            valor: 'TODOS',
            titulo: 'Todos',
            descricao: valores.publico === 'TODOS' && pessoas ? pessoas : 'A empresa toda',
            desabilitada: t.publicado,
          },
          {
            valor: 'SETORES',
            titulo: 'Setores',
            descricao: 'Um ou mais setores',
            desabilitada: t.publicado,
          },
          {
            valor: 'PESSOAS',
            titulo: 'Pessoas escolhidas',
            descricao: 'Você marca quem faz',
            desabilitada: t.publicado,
          },
        ]}
      />
      {valores.publico === 'SETORES' && (
        <SetoresDoPublico
          valor={valores.setorIds}
          desabilitado={t.publicado}
          aoMudar={(setorIds) => aoMudar({ ...valores, setorIds })}
        />
      )}
      {valores.publico === 'PESSOAS' && (
        <PessoasDoPublico
          valor={valores.pessoas}
          desabilitado={t.publicado}
          aoMudar={(p) => aoMudar({ ...valores, pessoas: p })}
        />
      )}
      {(erros.publico || erros.setorIds || erros.funcionarioIds) && (
        <p className="text-pequeno text-erro-texto">
          {erros.publico ?? erros.setorIds ?? erros.funcionarioIds}
        </p>
      )}
      {!t.publicado && (
        <OpcoesEmCartao<'PUBLICAR' | 'RASCUNHO'>
          rotulo="Quando publicar"
          valor={quando}
          aoMudar={aoMudarQuando}
          opcoes={[
            { valor: 'PUBLICAR', titulo: `Publicar agora, ${acesso}` },
            { valor: 'RASCUNHO', titulo: 'Salvar como rascunho' },
          ]}
        />
      )}
      <CaixaDeSelecao
        marcada={valores.obrigatorio && valores.incluiAdmitidos}
        aoMudar={(incluiAdmitidos) => aoMudar({ ...valores, incluiAdmitidos })}
        desabilitada={!valores.obrigatorio || t.encerrado}
        rotulo="Incluir quem for admitido depois"
        descricao={valores.obrigatorio ? undefined : 'Só vale para treinamento obrigatório.'}
      />
      {pessoas && (
        <p className="text-tabela text-texto-suave">
          {valores.obrigatorio
            ? `Aparece em Meus treinamentos de ${pessoas} ${futuro ? `a partir de ${formatarDiaMesDaData(valores.comecaEm)}` : 'assim que publicar'}.`
            : `Fica disponível em Meus treinamentos de ${pessoas} ${futuro ? `a partir de ${formatarDiaMesDaData(valores.comecaEm)}` : 'assim que publicar'}, para quem quiser se inscrever.`}
        </p>
      )}
      <p className="text-pequeno text-texto-suave">
        Ninguém recebe e-mail ao publicar. Para avisar, publique um Aviso.
      </p>
    </div>
  )
}
