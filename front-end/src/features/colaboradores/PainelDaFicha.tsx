import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { useParams, useSearchParams } from 'react-router'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { formatarData } from '../../shared/formato/datas'
import { usePainelSobreLista } from '../../shared/lista/usePainelSobreLista'
import { inteiroDaUrl } from '../../shared/lista/valoresDeLista'
import { Aviso } from '../../shared/ui/Aviso'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Janela } from '../../shared/ui/Janela'
import { JanelaDeConfirmacao } from '../../shared/ui/JanelaDeConfirmacao'
import { MenuDeAcoes, type AcaoDoMenu } from '../../shared/ui/MenuDeAcoes'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { ABAS_DA_FICHA_RH } from './abasDaFicha'
import { DialogoInativar } from './DialogoInativar'
import { DialogoReadmitir } from './DialogoReadmitir'
import { EtiquetaDaSituacao } from './EtiquetaDaSituacao'
import { MolduraDaFicha } from './MolduraDaFicha'

type Ficha = Esquema<'FuncionarioFichaRh'>
type Dialogo = 'inativar' | 'cancelar-desligamento' | 'readmitir' | null

function linhaDoCabecalho(ficha: Ficha): string {
  return [
    ficha.cargo?.nome,
    ficha.setor ? `setor ${ficha.setor.nome}` : null,
    ficha.matricula ? `matrícula ${ficha.matricula}` : null,
  ]
    .filter(Boolean)
    .join(', ')
}

function ehNaoEncontrado(erro: unknown): boolean {
  return erro instanceof ErroDeApi && erro.status === 404
}

export function PainelDaFicha() {
  const { fechar } = usePainelSobreLista()
  const notificacao = useNotificacao()
  const queryClient = useQueryClient()
  const parametros = useParams()
  const [busca, definirBusca] = useSearchParams()
  const funcionarioId = inteiroDaUrl(parametros.funcionarioId)
  const [editando, setEditando] = useState(false)
  const [dialogo, setDialogo] = useState<Dialogo>(null)
  const [acessoDesativado, setAcessoDesativado] = useState(false)

  const consulta = $api.useQuery(
    'get',
    '/api/rh/funcionarios/{funcionarioId}',
    { params: { path: { funcionarioId: funcionarioId ?? 0 } } },
    { enabled: funcionarioId !== undefined },
  )
  const convite = $api.useMutation('post', '/api/rh/usuarios/{funcionarioId}/convite')
  const cancelarDesligamento = $api.useMutation(
    'delete',
    '/api/rh/funcionarios/{funcionarioId}/desligamento',
  )
  const [erroDoCancelamento, setErroDoCancelamento] = useState<unknown>(null)

  const naoEncontrado = funcionarioId === undefined || ehNaoEncontrado(consulta.error)
  const jaAvisou = useRef(false)
  useEffect(() => {
    if (!naoEncontrado || jaAvisou.current) return
    jaAvisou.current = true
    notificacao.erro(mensagemDeErro(new ErroDeApi(404, 'Não encontrado.')))
    fechar()
  }, [naoEncontrado, notificacao, fechar])

  const abaDaUrl = busca.get('aba')
  const aba = ABAS_DA_FICHA_RH.find((a) => a.valor === abaDaUrl) ?? ABAS_DA_FICHA_RH[0]!
  const mudarAba = (valor: string) =>
    definirBusca(
      (atual) => {
        const novo = new URLSearchParams(atual)
        if (valor === 'dados') novo.delete('aba')
        else novo.set('aba', valor)
        return novo
      },
      { replace: true },
    )

  const avisarNaoEncontrado = (erro: unknown) => {
    notificacao.erro(mensagemDeErro(erro))
    fechar()
  }

  const ficha = consulta.data
  if (naoEncontrado) return null

  if (!ficha) {
    return (
      <Janela
        aberta
        aoFechar={() => fechar()}
        titulo="Ficha do colaborador"
        posicao="direita"
        largura="larga"
      >
        {consulta.error ? (
          <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
        ) : (
          <div role="status" aria-label="Carregando a ficha" className="flex flex-col gap-4">
            <Esqueleto className="h-20 w-full" />
            <Esqueleto className="h-40 w-full" />
            <Esqueleto className="h-40 w-full" />
          </div>
        )}
      </Janela>
    )
  }

  const inativo = ficha.situacao === 'INATIVO'
  const acoesDoMenu: AcaoDoMenu[] = []
  if (!inativo && ficha.acesso.situacao !== 'SENHA_DEFINIDA') {
    acoesDoMenu.push({
      chave: 'convite',
      rotulo:
        ficha.acesso.situacao === 'SEM_CONVITE' ? 'Enviar convite de acesso' : 'Reenviar convite',
      desabilitada: !ficha.emailCorporativo || convite.isPending,
      dica: ficha.emailCorporativo
        ? undefined
        : 'Esta pessoa não tem e-mail corporativo cadastrado.',
      aoEscolher: () => {
        convite.mutate(
          { params: { path: { funcionarioId: ficha.id } } },
          {
            onSuccess: () => notificacao.sucesso(`Convite enviado para ${ficha.emailCorporativo}.`),
            onError: (erro) => notificacao.erro(mensagemDeErro(erro)),
            onSettled: () => void invalidarRota(queryClient, '/api/rh/funcionarios'),
          },
        )
      },
    })
  }

  const desligamentoAgendado = ficha.desligamento?.situacao === 'AGENDADO'

  return (
    <MolduraDaFicha
      trilha={{ rotulo: 'Colaboradores', aoClicar: () => fechar() }}
      nome={ficha.nome}
      linhaDeApoio={linhaDoCabecalho(ficha)}
      etiqueta={<EtiquetaDaSituacao situacao={ficha.situacao} situacaoHoje={ficha.situacaoHoje} />}
      aoFechar={() => fechar()}
      abas={ABAS_DA_FICHA_RH}
      aba={aba.valor}
      aoMudarAba={mudarAba}
      acoes={
        <>
          <Botao
            onClick={() => {
              mudarAba('dados')
              setEditando(true)
            }}
          >
            Editar dados
          </Botao>
          {inativo ? (
            <Botao variante="secundario" onClick={() => setDialogo('readmitir')}>
              Readmitir
            </Botao>
          ) : (
            <Botao variante="perigo" onClick={() => setDialogo('inativar')}>
              Inativar
            </Botao>
          )}
          {acoesDoMenu.length > 0 && <MenuDeAcoes rotulo="Mais ações" acoes={acoesDoMenu} />}
        </>
      }
      faixas={
        <>
          {desligamentoAgendado && ficha.desligamento && (
            <Aviso
              tom="alerta"
              acao={
                <Botao
                  variante="secundario"
                  tamanho="pequeno"
                  onClick={() => setDialogo('cancelar-desligamento')}
                >
                  Cancelar desligamento
                </Botao>
              }
            >
              {`Desligamento agendado: o último dia de trabalho é ${formatarData(ficha.desligamento.ultimoDiaDeTrabalho)}. A pessoa continua com acesso até o dia seguinte.`}
            </Aviso>
          )}
          {acessoDesativado && (
            <Aviso
              tom="info"
              acao={
                <BotaoLink to="/rh/cadastros/usuarios" variante="secundario" tamanho="pequeno">
                  Usuários do sistema
                </BotaoLink>
              }
            >
              O acesso ao portal continua desativado. Libere em Cadastros, Usuários do sistema
              (Ativar).
            </Aviso>
          )}
        </>
      }
    >
      <aba.Componente
        ficha={ficha}
        editando={editando}
        aoEncerrarEdicao={() => setEditando(false)}
      />

      <DialogoInativar
        ficha={ficha}
        aberto={dialogo === 'inativar'}
        aoFechar={() => setDialogo(null)}
        aoNaoEncontrar={avisarNaoEncontrado}
      />
      <JanelaDeConfirmacao
        aberta={dialogo === 'cancelar-desligamento'}
        titulo={`Cancelar o desligamento agendado de ${ficha.nome}?`}
        descricao="A pessoa continua ativa e o último dia de trabalho deixa de valer."
        rotuloDeCancelar="Manter agendado"
        rotuloDeConfirmar="Cancelar desligamento"
        confirmando={cancelarDesligamento.isPending}
        erro={erroDoCancelamento ?? undefined}
        aoCancelar={() => {
          setErroDoCancelamento(null)
          setDialogo(null)
        }}
        aoConfirmar={async () => {
          setErroDoCancelamento(null)
          try {
            await cancelarDesligamento.mutateAsync({
              params: { path: { funcionarioId: ficha.id } },
            })
            await invalidarRota(queryClient, '/api/rh/funcionarios')
            setDialogo(null)
          } catch (erro) {
            if (ehNaoEncontrado(erro)) {
              setDialogo(null)
              avisarNaoEncontrado(erro)
              return
            }
            if (erro instanceof ErroDeApi && erro.codigo === 'SEM_DESLIGAMENTO_AGENDADO') {
              await invalidarRota(queryClient, '/api/rh/funcionarios')
              notificacao.erro(mensagemDeErro(erro))
              setDialogo(null)
              return
            }
            setErroDoCancelamento(erro)
          }
        }}
      />
      {inativo && (
        <DialogoReadmitir
          ficha={ficha}
          aberto={dialogo === 'readmitir'}
          aoFechar={() => setDialogo(null)}
          aoReadmitir={(readmitida) => {
            setDialogo(null)
            setAcessoDesativado(!readmitida.acesso.ativo)
          }}
        />
      )}
    </MolduraDaFicha>
  )
}
