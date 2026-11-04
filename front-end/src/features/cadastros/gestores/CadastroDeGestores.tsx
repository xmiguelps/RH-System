import { useQueryClient } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { $api } from '../../../shared/api/api'
import { ErroDeApi } from '../../../shared/api/erroDeApi'
import { mensagemDeErro } from '../../../shared/api/mensagemDeErro'
import type { Esquema } from '../../../shared/api/tipos'
import { formatarData } from '../../../shared/formato/datas'
import { plural } from '../../../shared/formato/texto'
import { Botao } from '../../../shared/ui/Botao'
import { Interruptor } from '../../../shared/ui/Interruptor'
import type { ColunaDaTabela } from '../../../shared/ui/Tabela'
import { useNotificacao } from '../../../shared/ui/useNotificacao'
import { invalidarCadastros } from '../invalidarCadastros'
import { ListaDeCadastro } from '../ListaDeCadastro'
import { DialogoDeAusencia } from './DialogoDeAusencia'
import { mensagemDeAusencia } from './mensagemDeAusencia'
import { ModalCadastrarGestor } from './ModalCadastrarGestor'

type Gestor = Esquema<'GestorItem'>

export function CadastroDeGestores() {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const consulta = $api.useQuery('get', '/api/rh/gestores')
  const ausencia = $api.useMutation('put', '/api/rh/gestores/{gestorId}/ausencia')
  const [enviandoPara, setEnviandoPara] = useState<number | null>(null)
  const [dialogo, setDialogo] = useState<Gestor | null>(null)
  const [erroDaData, setErroDaData] = useState<string | null>(null)
  const [cadastrando, setCadastrando] = useState(false)
  const emAndamento = useRef(false)

  const configurar = async (gestor: Gestor, ausente: boolean, ausenteAte: string | null) => {
    if (emAndamento.current) return
    emAndamento.current = true
    setEnviandoPara(gestor.gestorId)
    setErroDaData(null)
    try {
      const resposta = await ausencia.mutateAsync({
        params: { path: { gestorId: gestor.gestorId } },
        body: { ausente, ausenteAte: ausente ? ausenteAte : null },
      })
      notificacao.sucesso(mensagemDeAusencia(resposta))
      setDialogo(null)
    } catch (erro) {
      const doCampo = erro instanceof ErroDeApi ? erro.errors.ausenteAte : undefined
      if (doCampo && ausente) setErroDaData(doCampo.join(' '))
      else {
        notificacao.erro(mensagemDeErro(erro))
        setDialogo(null)
      }
    } finally {
      emAndamento.current = false
      setEnviandoPara(null)
      await invalidarCadastros(queryClient, '/api/rh/gestores')
    }
  }

  const colunas: ColunaDaTabela<Gestor>[] = [
    {
      chave: 'gestor',
      titulo: 'Gestor',
      celula: (g) => (
        <div className="flex flex-col">
          <span className="font-semibold text-texto">{g.funcionario.nome}</span>
          {g.funcionario.cargo && (
            <span className="text-pequeno text-texto-suave">{g.funcionario.cargo}</span>
          )}
        </div>
      ),
    },
    {
      chave: 'setor',
      titulo: 'Setor',
      celula: (g) =>
        g.soDecidePedidosDeGestores ? 'Só decide pedidos de gestores' : (g.setor?.nome ?? '—'),
    },
    {
      chave: 'equipe',
      titulo: 'Equipe',
      celula: (g) => `${g.quantidadeEquipe} ${plural(g.quantidadeEquipe, 'pessoa', 'pessoas')}`,
    },
    {
      chave: 'ausente',
      titulo: 'Gestor ausente',
      celula: (g) => {
        const semEquipe = g.soDecidePedidosDeGestores
        return (
          <div className="flex flex-col gap-1">
            <span
              title={
                semEquipe
                  ? 'Não tem equipe: não há etapa do gestor para aprovar sozinha.'
                  : undefined
              }
            >
              <Interruptor
                ligado={g.ausenteHoje}
                rotulo={`Gestor ausente: ${g.funcionario.nome}`}
                desabilitado={semEquipe || enviandoPara === g.gestorId}
                aoMudar={(ligado) => {
                  if (ligado) {
                    setErroDaData(null)
                    setDialogo(g)
                  } else void configurar(g, false, null)
                }}
              />
            </span>
            {semEquipe && (
              <span className="text-pequeno text-texto-suave">
                Não tem equipe: não há etapa do gestor para aprovar sozinha.
              </span>
            )}
            {g.ausenteHoje && (
              <span className="flex items-center gap-2 text-pequeno text-texto-suave">
                {g.ausenteAte ? `Volta em ${formatarData(g.ausenteAte)}` : 'Sem data de volta'}
                <Botao
                  variante="texto"
                  tamanho="pequeno"
                  onClick={() => {
                    setErroDaData(null)
                    setDialogo(g)
                  }}
                >
                  Mudar a data de volta
                </Botao>
              </span>
            )}
          </div>
        )
      },
    },
  ]

  return (
    <>
      <ListaDeCadastro
        titulo="Gestores"
        apoio="Quem gerencia cada setor"
        rotuloDaTabela="Gestores"
        botaoNovo={{ rotulo: 'Cadastrar gestor', aoClicar: () => setCadastrando(true) }}
        colunas={colunas}
        chaveDaLinha={(g) => g.gestorId}
        dados={consulta.data}
        carregando={consulta.isFetching}
        erro={consulta.error}
        aoTentarDeNovo={() => void consulta.refetch()}
        vazio="Nenhum gestor cadastrado."
        vazioComBusca="Nenhum gestor cadastrado."
      />
      {dialogo && (
        <DialogoDeAusencia
          gestor={dialogo}
          aberto
          enviando={enviandoPara === dialogo.gestorId}
          erroDoServidor={erroDaData}
          aoFechar={() => setDialogo(null)}
          aoConfirmar={(ausenteAte) => void configurar(dialogo, true, ausenteAte)}
        />
      )}
      <ModalCadastrarGestor aberta={cadastrando} aoFechar={() => setCadastrando(false)} />
    </>
  )
}
