import { useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowUp } from 'lucide-react'
import { useState } from 'react'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { AreaDeTexto } from '../../shared/ui/AreaDeTexto'
import { Aviso } from '../../shared/ui/Aviso'
import { Botao } from '../../shared/ui/Botao'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { Selo } from '../../shared/ui/Selo'
import { perguntasCongeladas } from './regrasDoAssistente'
import { ROTULO_DA_PERGUNTA, TIPOS_DE_PERGUNTA } from './rotulosDosFormularios'

type Formulario = Esquema<'FormularioDetalheRh'>
type Pergunta = Esquema<'PerguntaFormulario'>
type PerguntaTipo = Esquema<'PerguntaTipo'>
type Alternativa = { id?: number; texto: string }
type Edicao = {
  id?: number
  tipo: PerguntaTipo
  texto: string
  obrigatoria: boolean
  alternativas: Alternativa[]
  ordem?: number
}

function comandoDa(p: Pergunta, ordem?: number): Esquema<'PerguntaComando'> {
  return {
    texto: p.texto,
    tipo: p.tipo,
    obrigatoria: p.obrigatoria,
    alternativas: p.alternativas.map((a) => ({ id: a.id, texto: a.texto })),
    ...(ordem !== undefined ? { ordem } : {}),
  }
}

function errosDoServidor(erro: unknown): { texto?: string; alternativas?: string; geral?: string } {
  if (erro instanceof ErroDeApi && erro.status === 400) {
    const e = erro.errors
    const saida = {
      texto: e.texto?.join(' '),
      alternativas: e.alternativas?.join(' '),
    }
    if (saida.texto || saida.alternativas) return saida
  }
  return { geral: mensagemDeErro(erro) }
}

function EditorDePergunta({
  formularioId,
  edicao,
  aoFechar,
}: {
  formularioId: number
  edicao: Edicao
  aoFechar: () => void
}) {
  const queryClient = useQueryClient()
  const [valor, setValor] = useState<Edicao>(edicao)
  const [erros, setErros] = useState<{ texto?: string; alternativas?: string; geral?: string }>({})
  const adicionar = $api.useMutation('post', '/api/rh/formularios/{formularioId}/perguntas')
  const editar = $api.useMutation(
    'put',
    '/api/rh/formularios/{formularioId}/perguntas/{perguntaId}',
  )
  const apagar = $api.useMutation(
    'delete',
    '/api/rh/formularios/{formularioId}/perguntas/{perguntaId}',
  )
  const salvando = adicionar.isPending || editar.isPending || apagar.isPending
  const ehMultipla = valor.tipo === 'MULTIPLA_ESCOLHA'

  async function atualizar() {
    await invalidarRota(queryClient, '/api/rh/formularios/{formularioId}')
  }

  async function salvar() {
    if (salvando) return
    const novos: typeof erros = {}
    if (!valor.texto.trim()) novos.texto = 'Escreva a pergunta.'
    const alternativas = valor.alternativas.filter((a) => a.texto.trim())
    if (ehMultipla && alternativas.length < 2) novos.alternativas = 'Escreva pelo menos 2 opções.'
    setErros(novos)
    if (Object.keys(novos).length) return
    const corpo: Esquema<'PerguntaComando'> = {
      texto: valor.texto.trim(),
      tipo: valor.tipo,
      obrigatoria: valor.obrigatoria,
      alternativas: ehMultipla
        ? alternativas.map((a) => ({ ...(a.id ? { id: a.id } : {}), texto: a.texto.trim() }))
        : [],
      ...(valor.ordem !== undefined ? { ordem: valor.ordem } : {}),
    }
    try {
      if (valor.id === undefined)
        await adicionar.mutateAsync({ params: { path: { formularioId } }, body: corpo })
      else
        await editar.mutateAsync({
          params: { path: { formularioId, perguntaId: valor.id } },
          body: corpo,
        })
      await atualizar()
      aoFechar()
    } catch (e) {
      setErros(errosDoServidor(e))
    }
  }

  async function excluir() {
    if (valor.id === undefined || salvando) return
    try {
      await apagar.mutateAsync({ params: { path: { formularioId, perguntaId: valor.id } } })
      await atualizar()
      aoFechar()
    } catch (e) {
      setErros({ geral: mensagemDeErro(e) })
    }
  }

  return (
    <section
      aria-label={valor.id === undefined ? 'Nova pergunta' : 'Editar pergunta'}
      className="flex flex-col gap-4 rounded-cartao border-2 border-primaria bg-superficie p-5"
    >
      <p className="text-tabela font-semibold text-texto">{ROTULO_DA_PERGUNTA[valor.tipo]}</p>
      <Campo rotulo="Texto da pergunta" erro={erros.texto}>
        <AreaDeTexto
          rows={2}
          limite={500}
          value={valor.texto}
          onChange={(e) => setValor({ ...valor, texto: e.target.value })}
        />
      </Campo>
      {ehMultipla && (
        <fieldset className="flex flex-col gap-2">
          <legend className="text-tabela font-semibold text-texto">Opções (escolha de uma)</legend>
          {valor.alternativas.map((a, i) => (
            <div key={a.id ?? `nova-${i}`} className="flex items-center gap-2">
              <Entrada
                aria-label={`Opção ${i + 1}`}
                value={a.texto}
                onChange={(e) =>
                  setValor({
                    ...valor,
                    alternativas: valor.alternativas.map((x, j) =>
                      j === i ? { ...x, texto: e.target.value } : x,
                    ),
                  })
                }
              />
              <Botao
                variante="texto"
                tamanho="pequeno"
                aria-label={`Remover a opção ${i + 1}`}
                onClick={() =>
                  setValor({ ...valor, alternativas: valor.alternativas.filter((_, j) => j !== i) })
                }
              >
                Remover
              </Botao>
            </div>
          ))}
          {valor.alternativas.length < 10 && (
            <div>
              <Botao
                variante="secundario"
                tamanho="pequeno"
                onClick={() =>
                  setValor({ ...valor, alternativas: [...valor.alternativas, { texto: '' }] })
                }
              >
                Adicionar opção
              </Botao>
            </div>
          )}
          {erros.alternativas && (
            <p className="text-pequeno text-erro-texto">{erros.alternativas}</p>
          )}
        </fieldset>
      )}
      <CaixaDeSelecao
        marcada={valor.obrigatoria}
        aoMudar={(obrigatoria) => setValor({ ...valor, obrigatoria })}
        rotulo="Obrigatória"
      />
      {erros.geral && <Aviso tom="erro">{erros.geral}</Aviso>}
      <div className="flex flex-wrap justify-between gap-3">
        <div>
          {valor.id !== undefined && (
            <Botao variante="perigo" tamanho="pequeno" onClick={() => void excluir()}>
              Apagar pergunta
            </Botao>
          )}
        </div>
        <div className="flex gap-3">
          <Botao variante="secundario" tamanho="pequeno" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao tamanho="pequeno" onClick={() => void salvar()} carregando={salvando}>
            Salvar pergunta
          </Botao>
        </div>
      </div>
    </section>
  )
}

/** Tela 18: a etapa Perguntas (sem quiz, sem nota, sem "Eliminatória"). */
export function EtapaPerguntas({ formulario }: { formulario: Formulario }) {
  const queryClient = useQueryClient()
  const [edicao, setEdicao] = useState<Edicao | null>(null)
  const [erroAoMover, setErroAoMover] = useState<string | null>(null)
  const mover = $api.useMutation('put', '/api/rh/formularios/{formularioId}/perguntas/{perguntaId}')
  const congeladas = perguntasCongeladas(formulario)
  const perguntas = [...formulario.perguntas].sort((a, b) => a.ordem - b.ordem)

  async function moverPara(p: Pergunta, ordem: number) {
    setErroAoMover(null)
    try {
      await mover.mutateAsync({
        params: { path: { formularioId: formulario.id, perguntaId: p.id } },
        body: comandoDa(p, ordem),
      })
      await invalidarRota(queryClient, '/api/rh/formularios/{formularioId}')
    } catch (e) {
      setErroAoMover(mensagemDeErro(e))
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <h2 className="text-t3 font-bold text-texto">Perguntas</h2>
      {congeladas && <Aviso tom="info">{congeladas}</Aviso>}
      {perguntas.length === 0 && !edicao && (
        <p className="text-corpo text-texto-suave">Nenhuma pergunta ainda.</p>
      )}
      <ol aria-label="Perguntas do formulário" className="flex flex-col gap-3">
        {perguntas.map((p, i) =>
          edicao?.id === p.id ? (
            <li key={p.id}>
              <EditorDePergunta
                formularioId={formulario.id}
                edicao={edicao}
                aoFechar={() => setEdicao(null)}
              />
            </li>
          ) : (
            <li
              key={p.id}
              className="flex flex-col gap-2 rounded-cartao border border-borda bg-superficie p-4"
            >
              <div className="flex items-center justify-between gap-3">
                <span className="text-pequeno font-semibold text-texto-suave">
                  {i + 1}. {ROTULO_DA_PERGUNTA[p.tipo]}
                  {!p.obrigatoria && ', opcional'}
                </span>
                {!congeladas && (
                  <span className="flex gap-1">
                    <Botao
                      variante="texto"
                      tamanho="pequeno"
                      icone={ArrowUp}
                      aria-label={`Subir a pergunta ${i + 1}`}
                      disabled={i === 0 || mover.isPending}
                      onClick={() => void moverPara(p, p.ordem - 1)}
                    />
                    <Botao
                      variante="texto"
                      tamanho="pequeno"
                      icone={ArrowDown}
                      aria-label={`Descer a pergunta ${i + 1}`}
                      disabled={i === perguntas.length - 1 || mover.isPending}
                      onClick={() => void moverPara(p, p.ordem + 1)}
                    />
                    <Botao
                      variante="secundario"
                      tamanho="pequeno"
                      aria-label={`Editar a pergunta ${i + 1}`}
                      onClick={() =>
                        setEdicao({
                          id: p.id,
                          tipo: p.tipo,
                          texto: p.texto,
                          obrigatoria: p.obrigatoria,
                          alternativas: p.alternativas.map((a) => ({ id: a.id, texto: a.texto })),
                          ordem: p.ordem,
                        })
                      }
                    >
                      Editar
                    </Botao>
                  </span>
                )}
              </div>
              <p className="text-corpo text-texto">{p.texto}</p>
              {p.alternativas.length > 0 && (
                <span className="flex flex-wrap gap-2">
                  {p.alternativas.map((a) => (
                    <Selo key={a.id} tom="neutro">
                      {a.texto}
                    </Selo>
                  ))}
                </span>
              )}
            </li>
          ),
        )}
      </ol>
      {erroAoMover && <Aviso tom="erro">{erroAoMover}</Aviso>}
      {edicao && edicao.id === undefined && (
        <EditorDePergunta
          formularioId={formulario.id}
          edicao={edicao}
          aoFechar={() => setEdicao(null)}
        />
      )}
      {!congeladas && !edicao && (
        <div className="flex flex-col gap-3 rounded-cartao border border-dashed border-tracejado p-4">
          <span className="text-tabela font-semibold text-texto">Adicionar pergunta:</span>
          <div className="flex flex-wrap gap-2">
            {TIPOS_DE_PERGUNTA.map((tipo) => {
              const semArquivo = tipo === 'ARQUIVO' && formulario.anonimo
              return (
                <Botao
                  key={tipo}
                  variante="secundario"
                  tamanho="pequeno"
                  disabled={semArquivo}
                  title={semArquivo ? 'Formulário anônimo não tem envio de arquivo' : undefined}
                  onClick={() =>
                    setEdicao({
                      tipo,
                      texto: '',
                      obrigatoria: true,
                      alternativas:
                        tipo === 'MULTIPLA_ESCOLHA' ? [{ texto: '' }, { texto: '' }] : [],
                    })
                  }
                >
                  {ROTULO_DA_PERGUNTA[tipo]}
                </Botao>
              )
            })}
          </div>
          {formulario.anonimo && (
            <p className="text-pequeno text-texto-suave">
              Formulário anônimo não tem envio de arquivo.
            </p>
          )}
        </div>
      )}
    </div>
  )
}
