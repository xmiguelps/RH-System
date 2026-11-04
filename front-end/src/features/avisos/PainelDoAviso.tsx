import { useQueryClient } from '@tanstack/react-query'
import { useRef } from 'react'
import { Controller, useWatch, type UseFormReturn } from 'react-hook-form'
import { useLocation, useNavigate, useParams } from 'react-router'
import { z } from 'zod'
import { invalidarContadoresDoMenu } from '../../app/useContadoresDoMenu'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import type { Esquema } from '../../shared/api/tipos'
import { campoData, campoHora } from '../../shared/form/esquemas'
import { FormularioComErroDoServidor } from '../../shared/form/FormularioComErroDoServidor'
import { dataDoInstante, formatarHora, instanteUtcDaHoraLocal } from '../../shared/formato/datas'
import { inteiroDaUrl } from '../../shared/lista/valoresDeLista'
import { AreaDeTexto } from '../../shared/ui/AreaDeTexto'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Janela } from '../../shared/ui/Janela'
import { OpcoesEmCartao } from '../../shared/ui/OpcoesEmCartao'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { ConteudoDoAviso } from './PecasDosAvisos'
import {
  diaDaExpiracao,
  LIMITE_DO_DETALHADO,
  LIMITE_DO_SIMPLES,
  mensagemDePublicacao,
} from './rotulosDosAvisos'

type Aviso = Esquema<'AvisoDetalheRh'>

const esquema = z
  .object({
    tipo: z.enum(['SIMPLES', 'DETALHADO']),
    titulo: z
      .string()
      .trim()
      .min(1, 'Escreva o título do aviso.')
      .max(200, 'O título tem até 200 caracteres.'),
    conteudo: z.string().trim().min(1, 'Escreva a mensagem do aviso.'),
    publicarEmData: z.string(),
    publicarEmHora: z.string(),
    expiraEm: z.string(),
    fixado: z.boolean(),
  })
  .superRefine((v, ctx) => {
    if (v.tipo === 'SIMPLES' && v.conteudo.length > LIMITE_DO_SIMPLES)
      ctx.addIssue({
        code: 'custom',
        path: ['conteudo'],
        message: `O aviso simples tem até ${LIMITE_DO_SIMPLES} caracteres.`,
      })
    if (v.tipo === 'DETALHADO' && v.conteudo.length > LIMITE_DO_DETALHADO)
      ctx.addIssue({
        code: 'custom',
        path: ['conteudo'],
        message: 'O aviso detalhado tem até 10.000 caracteres.',
      })
    if (v.publicarEmData && !campoData().safeParse(v.publicarEmData).success)
      ctx.addIssue({ code: 'custom', path: ['publicarEmData'], message: 'Data inválida.' })
    if (v.publicarEmHora && !campoHora().safeParse(v.publicarEmHora).success)
      ctx.addIssue({ code: 'custom', path: ['publicarEmHora'], message: 'Hora inválida.' })
    if (v.publicarEmHora && !v.publicarEmData)
      ctx.addIssue({ code: 'custom', path: ['publicarEmData'], message: 'Escolha também o dia.' })
    if (v.expiraEm && !campoData().safeParse(v.expiraEm).success)
      ctx.addIssue({ code: 'custom', path: ['expiraEm'], message: 'Data inválida.' })
    else if (v.expiraEm && v.publicarEmData && v.expiraEm < v.publicarEmData)
      ctx.addIssue({
        code: 'custom',
        path: ['expiraEm'],
        message: 'A expiração não pode ser antes do dia da publicação.',
      })
  })

type Valores = z.input<typeof esquema>
type Saida = z.output<typeof esquema>
type Formulario = UseFormReturn<Valores, unknown, Saida>

function valoresIniciais(aviso: Aviso | undefined): Valores {
  if (!aviso)
    return {
      tipo: 'SIMPLES',
      titulo: '',
      conteudo: '',
      publicarEmData: '',
      publicarEmHora: '',
      expiraEm: '',
      fixado: false,
    }
  return {
    tipo: aviso.tipo,
    titulo: aviso.titulo,
    conteudo: aviso.conteudo,
    publicarEmData: aviso.publicacaoEmUtc ? dataDoInstante(aviso.publicacaoEmUtc) : '',
    publicarEmHora: aviso.publicacaoEmUtc ? formatarHora(aviso.publicacaoEmUtc) : '',
    expiraEm: diaDaExpiracao(aviso.expiraEmUtc) ?? '',
    fixado: aviso.fixado,
  }
}

function publicarEmUtc(v: Saida): string | null {
  if (!v.publicarEmData) return null
  return instanteUtcDaHoraLocal(v.publicarEmData, v.publicarEmHora || '00:00')
}

function CamposDoAviso({ formulario, aviso }: { formulario: Formulario; aviso?: Aviso }) {
  const { control, register, formState } = formulario
  const tipo = useWatch({ control, name: 'tipo' })
  const conteudo = useWatch({ control, name: 'conteudo' })
  const e = formState.errors
  const editando = aviso !== undefined
  const dataEditavel = !aviso || aviso.situacao === 'RASCUNHO' || aviso.situacao === 'AGENDADO'
  const limite = tipo === 'SIMPLES' ? LIMITE_DO_SIMPLES : LIMITE_DO_DETALHADO
  return (
    <div className="flex flex-col gap-5">
      <Controller
        control={control}
        name="tipo"
        render={({ field }) => (
          <OpcoesEmCartao
            rotulo="Tipo de aviso"
            valor={field.value}
            aoMudar={field.onChange}
            opcoes={[
              {
                valor: 'SIMPLES',
                titulo: 'Simples',
                descricao: 'Título e uma mensagem curta',
                desabilitada: editando,
              },
              {
                valor: 'DETALHADO',
                titulo: 'Detalhado',
                descricao: 'Texto longo, com seções',
                desabilitada: editando,
              },
            ]}
          />
        )}
      />
      <Campo rotulo="Título" erro={e.titulo?.message}>
        <Entrada placeholder="Ex.: inventário de estoque no sábado" {...register('titulo')} />
      </Campo>
      <Campo
        rotulo={tipo === 'SIMPLES' ? 'Mensagem' : 'Conteúdo'}
        dica={
          tipo === 'DETALHADO' ? 'Use ## no começo de uma linha para criar uma seção.' : undefined
        }
        erro={e.conteudo?.message}
      >
        <AreaDeTexto
          rows={tipo === 'SIMPLES' ? 4 : 12}
          placeholder={tipo === 'SIMPLES' ? 'Uma mensagem curta, de até 280 caracteres' : undefined}
          {...register('conteudo')}
        />
      </Campo>
      <p className="-mt-4 self-end text-mini text-texto-suave" aria-live="polite">
        {conteudo.length}/{limite}
      </p>
      {tipo === 'DETALHADO' && conteudo.trim() && (
        <section aria-label="Prévia do aviso" className="rounded-cartao bg-superficie-suave p-4">
          <ConteudoDoAviso tipo="DETALHADO" conteudo={conteudo} />
        </section>
      )}
      <div className="grid grid-cols-3 gap-4">
        <Campo
          rotulo="Publicar em"
          dica={dataEditavel ? 'Vazio publica agora.' : undefined}
          erro={e.publicarEmData?.message}
        >
          <Entrada
            type="date"
            lang="pt-BR"
            disabled={!dataEditavel}
            {...register('publicarEmData')}
          />
        </Campo>
        <Campo rotulo="Hora da publicação" erro={e.publicarEmHora?.message}>
          <Entrada type="time" disabled={!dataEditavel} {...register('publicarEmHora')} />
        </Campo>
        <Campo
          rotulo="Expira em"
          dica={
            aviso?.situacao === 'EXPIRADO'
              ? 'Ao escolher uma nova data de expiração, o aviso volta a ficar ativo.'
              : 'Vazio não expira.'
          }
          erro={e.expiraEm?.message}
        >
          <Entrada type="date" lang="pt-BR" {...register('expiraEm')} />
        </Campo>
      </div>
      <Controller
        control={control}
        name="fixado"
        render={({ field }) => (
          <CaixaDeSelecao
            marcada={field.value}
            aoMudar={field.onChange}
            rotulo="Fixar no topo da tela inicial até expirar"
          />
        )}
      />
    </div>
  )
}

/** Tela 28: o painel "Novo aviso" (e "Editar aviso", o mesmo painel). */
export function PainelDoAviso() {
  const navegar = useNavigate()
  const { search } = useLocation()
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const avisoId = inteiroDaUrl(useParams().avisoId)
  const editando = useParams().avisoId !== undefined
  const consulta = $api.useQuery(
    'get',
    '/api/rh/avisos/{avisoId}',
    { params: { path: { avisoId: avisoId ?? 0 } } },
    { enabled: avisoId !== undefined },
  )
  const criar = $api.useMutation('post', '/api/rh/avisos')
  const editar = $api.useMutation('put', '/api/rh/avisos/{avisoId}')
  const publicarAviso = $api.useMutation('post', '/api/rh/avisos/{avisoId}/publicar')
  const publicarDepois = useRef(false)
  const aviso = editando ? consulta.data : undefined

  function fechar(escolhido?: Esquema<'AvisoItemRh'>) {
    const parametros = new URLSearchParams(search)
    if (escolhido) {
      parametros.set('aviso', String(escolhido.id))
      if (escolhido.situacao === 'ATIVO') parametros.delete('situacao')
      else parametros.set('situacao', escolhido.situacao)
      parametros.delete('pagina')
    }
    const texto = parametros.toString()
    navegar({ pathname: '/rh/avisos', search: texto ? `?${texto}` : '' })
  }

  async function salvar(v: Saida) {
    const publicar = publicarDepois.current
    let salvo: Aviso | undefined
    if (!aviso) {
      salvo = await criar.mutateAsync({
        body: {
          tipo: v.tipo,
          titulo: v.titulo,
          conteudo: v.conteudo,
          publicarEmUtc: publicarEmUtc(v),
          expiraEm: v.expiraEm || null,
          fixado: v.fixado,
          publicar,
        },
      })
    } else {
      const dataEditavel = aviso.situacao === 'RASCUNHO' || aviso.situacao === 'AGENDADO'
      salvo = await editar.mutateAsync({
        params: { path: { avisoId: aviso.id } },
        body: {
          titulo: v.titulo,
          conteudo: v.conteudo,
          ...(dataEditavel ? { publicarEmUtc: publicarEmUtc(v) } : {}),
          expiraEm: v.expiraEm || null,
          fixado: v.fixado,
        },
      })
      if (publicar && aviso.situacao === 'RASCUNHO') {
        salvo = await publicarAviso.mutateAsync({ params: { path: { avisoId: aviso.id } } })
      }
    }
    if (!salvo) throw new ErroDeApi(500, 'Resposta inesperada da API.')
    notificacao.sucesso(
      salvo.situacao === 'RASCUNHO'
        ? 'Rascunho salvo.'
        : publicar
          ? mensagemDePublicacao(salvo)
          : 'Alterações salvas.',
    )
    await Promise.all([
      invalidarRota(queryClient, '/api/rh/avisos'),
      invalidarContadoresDoMenu(queryClient),
    ])
    fechar(salvo)
  }

  const carregandoEdicao = editando && !aviso
  const rascunhoOuNovo = !aviso || aviso.situacao === 'RASCUNHO'
  const salvando = criar.isPending || editar.isPending || publicarAviso.isPending
  return (
    <Janela
      aberta
      aoFechar={() => fechar()}
      titulo={editando ? 'Editar aviso' : 'Novo aviso'}
      posicao="direita"
      largura="media"
      rodape={
        <div className="flex w-full flex-col gap-3">
          <p className="text-pequeno text-texto-suave">
            {editando
              ? 'Editar não envia o e-mail de novo.'
              : 'Ao publicar, todos os colaboradores ativos recebem um e-mail.'}
          </p>
          <div className="flex justify-end gap-3">
            {rascunhoOuNovo ? (
              <>
                <Botao
                  variante="secundario"
                  type="submit"
                  form="form-aviso"
                  disabled={salvando || carregandoEdicao}
                  onClick={() => {
                    publicarDepois.current = false
                  }}
                >
                  Salvar rascunho
                </Botao>
                <Botao
                  type="submit"
                  form="form-aviso"
                  disabled={salvando || carregandoEdicao}
                  onClick={() => {
                    publicarDepois.current = true
                  }}
                >
                  {salvando ? 'Salvando...' : 'Publicar aviso'}
                </Botao>
              </>
            ) : (
              <Botao type="submit" form="form-aviso" carregando={salvando}>
                Salvar alterações
              </Botao>
            )}
          </div>
        </div>
      }
    >
      {editando && consulta.error ? (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      ) : carregandoEdicao ? (
        <div role="status" aria-label="Carregando o aviso" className="flex flex-col gap-3">
          <Esqueleto className="h-24 w-full" />
          <Esqueleto className="h-40 w-full" />
        </div>
      ) : (
        <FormularioComErroDoServidor
          id="form-aviso"
          esquema={esquema}
          valoresIniciais={valoresIniciais(aviso)}
          aoEnviar={salvar}
        >
          {(formulario) => <CamposDoAviso formulario={formulario} aviso={aviso} />}
        </FormularioComErroDoServidor>
      )}
    </Janela>
  )
}
