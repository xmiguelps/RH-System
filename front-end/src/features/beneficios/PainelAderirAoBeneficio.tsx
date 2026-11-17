import { useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { hojeEmSaoPaulo } from '../../shared/formato/datas'
import { inteiroDaUrl } from '../../shared/lista/valoresDeLista'
import { Aviso } from '../../shared/ui/Aviso'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Janela } from '../../shared/ui/Janela'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { primeiroDiaDoProximoMes, TEXTO_DO_PRAZO_DE_RESPOSTA } from './rotulosDosBeneficios'

type Disponivel = Esquema<'BeneficioAdesaoDisponivel'>
type Dependente = Esquema<'BeneficioAdesaoDependenteComando'>
type Erros = Record<string, string>

type NovoDependente = { chave: number; nome: string; parentesco: string; nascimento: string }

/** Tela 45: o painel "Aderir ao {benefício}" (`/eu/beneficios/aderir/:beneficioId`). */
export function PainelAderirAoBeneficio() {
  const navegar = useNavigate()
  const beneficioId = inteiroDaUrl(useParams().beneficioId)
  const consulta = $api.useQuery('get', '/api/eu/beneficios')
  const item = consulta.data?.disponiveis.find((d) => d.beneficio.id === beneficioId)
  const fechar = () => navegar('/eu/beneficios')
  return (
    <Janela
      aberta
      aoFechar={fechar}
      titulo={item ? `Aderir ao ${item.beneficio.nome.toLowerCase()}` : 'Aderir ao benefício'}
      posicao="direita"
      largura="media"
    >
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      ) : !consulta.data ? (
        <Esqueleto className="h-48 w-full" />
      ) : !item ? (
        <Aviso tom="info">Este benefício não está disponível para você.</Aviso>
      ) : (
        <Formulario
          item={item}
          dependentesCadastrados={consulta.data.dependentesCadastrados}
          aoFechar={fechar}
        />
      )}
    </Janela>
  )
}

function Formulario({
  item,
  dependentesCadastrados,
  aoFechar,
}: {
  item: Disponivel
  dependentesCadastrados: readonly Dependente[]
  aoFechar: () => void
}) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const pedir = $api.useMutation('post', '/api/eu/beneficios-adesoes')
  const hoje = hojeEmSaoPaulo()
  const [inicio, setInicio] = useState(primeiroDiaDoProximoMes(hoje))
  const [autoriza, setAutoriza] = useState(false)
  const [marcados, setMarcados] = useState<ReadonlySet<number>>(new Set())
  const [novos, setNovos] = useState<readonly NovoDependente[]>([])
  const [trajeto, setTrajeto] = useState({ origem: '', destino: '', linhasMeios: '' })
  const [erros, setErros] = useState<Erros>({})
  const [erroGeral, setErroGeral] = useState<string | null>(null)

  const mudarNovo = (chave: number, parte: Partial<NovoDependente>) =>
    setNovos((atual) => atual.map((n) => (n.chave === chave ? { ...n, ...parte } : n)))

  async function enviar() {
    if (pedir.isPending) return
    const problemas: Erros = {}
    if (!inicio) problemas.dataInicioDesejada = 'Escolha quando começar.'
    else if (inicio < hoje)
      problemas.dataInicioDesejada = 'A data de início não pode ser anterior a hoje.'
    novos.forEach((n) => {
      if (!n.nome.trim() || !n.parentesco.trim() || !n.nascimento)
        problemas[`novo${n.chave}`] = 'Preencha nome, parentesco e data de nascimento.'
      else if (n.nascimento > hoje)
        problemas[`novo${n.chave}`] = 'A data de nascimento não pode ser no futuro.'
    })
    const trajetoPreenchido = Object.values(trajeto).some((v) => v.trim())
    if (item.aceitaTrajeto && trajetoPreenchido && Object.values(trajeto).some((v) => !v.trim()))
      problemas.trajeto = 'Preencha origem, destino e linhas ou meios, ou deixe os três em branco.'
    setErros(problemas)
    setErroGeral(null)
    if (Object.keys(problemas).length) return
    const dependentes: Dependente[] = [
      ...dependentesCadastrados.filter((_, i) => marcados.has(i)),
      ...novos.map((n) => ({
        nome: n.nome.trim(),
        parentesco: n.parentesco.trim(),
        dataNascimento: n.nascimento,
      })),
    ]
    try {
      await pedir.mutateAsync({
        body: {
          beneficioId: item.beneficio.id,
          dataInicioDesejada: inicio,
          autorizaDescontoEmFolha: autoriza,
          ...(item.aceitaDependentes ? { dependentes } : {}),
          ...(item.aceitaTrajeto && trajetoPreenchido
            ? {
                trajeto: {
                  origem: trajeto.origem.trim(),
                  destino: trajeto.destino.trim(),
                  linhasMeios: trajeto.linhasMeios.trim(),
                },
              }
            : {}),
        },
      })
      notificacao.sucesso('Pedido enviado. O RH vai analisar.')
      await invalidarRota(queryClient, '/api/eu/beneficios')
      aoFechar()
    } catch (e) {
      const campos: Erros = {}
      if (e instanceof ErroDeApi)
        for (const [chave, mensagens] of Object.entries(e.errors))
          campos[chave.charAt(0).toLowerCase() + chave.slice(1)] = mensagens.join(' ')
      setErros(campos)
      if (Object.keys(campos).length === 0) setErroGeral(mensagemDeErro(e))
      if (e instanceof ErroDeApi && e.status === 409)
        await invalidarRota(queryClient, '/api/eu/beneficios')
    }
  }

  return (
    <form
      className="flex flex-col gap-5"
      onSubmit={(evento) => {
        evento.preventDefault()
        void enviar()
      }}
    >
      <div className="rounded-cartao bg-superficie-suave p-4">
        <p className="text-corpo font-bold text-texto">{item.beneficio.nome}</p>
        {item.beneficio.descricao && (
          <p className="text-tabela text-texto-suave">{item.beneficio.descricao}</p>
        )}
      </div>

      {item.aceitaDependentes && (
        <fieldset className="flex flex-col gap-3">
          <legend className="text-tabela font-semibold text-texto">Quem vai entrar no plano</legend>
          <CaixaDeSelecao marcada aoMudar={() => undefined} desabilitada rotulo="Você" />
          {dependentesCadastrados.map((d, i) => (
            <CaixaDeSelecao
              key={`${d.nome}-${d.dataNascimento}`}
              marcada={marcados.has(i)}
              aoMudar={(marcada) =>
                setMarcados((atual) => {
                  const nova = new Set(atual)
                  if (marcada) nova.add(i)
                  else nova.delete(i)
                  return nova
                })
              }
              rotulo={d.nome}
              descricao={d.parentesco}
            />
          ))}
          {novos.map((n) => (
            <div
              key={n.chave}
              className="flex flex-col gap-3 rounded-cartao border border-borda p-3"
            >
              <div className="grid grid-cols-2 gap-3">
                <Campo rotulo="Nome do dependente">
                  <Entrada
                    maxLength={150}
                    value={n.nome}
                    onChange={(e) => mudarNovo(n.chave, { nome: e.target.value })}
                  />
                </Campo>
                <Campo rotulo="Parentesco">
                  <Entrada
                    maxLength={30}
                    value={n.parentesco}
                    onChange={(e) => mudarNovo(n.chave, { parentesco: e.target.value })}
                  />
                </Campo>
              </div>
              <Campo rotulo="Data de nascimento" erro={erros[`novo${n.chave}`]}>
                <Entrada
                  type="date"
                  lang="pt-BR"
                  value={n.nascimento}
                  onChange={(e) => mudarNovo(n.chave, { nascimento: e.target.value })}
                />
              </Campo>
              <div>
                <Botao
                  variante="texto"
                  tamanho="pequeno"
                  onClick={() => setNovos((atual) => atual.filter((x) => x.chave !== n.chave))}
                >
                  <Trash2 aria-hidden="true" className="h-4 w-4" />
                  Tirar este dependente
                </Botao>
              </div>
            </div>
          ))}
          <div>
            <Botao
              variante="secundario"
              tamanho="pequeno"
              onClick={() =>
                setNovos((atual) => [
                  ...atual,
                  {
                    chave: (atual.at(-1)?.chave ?? 0) + 1,
                    nome: '',
                    parentesco: '',
                    nascimento: '',
                  },
                ])
              }
            >
              <Plus aria-hidden="true" className="h-4 w-4" />
              Incluir outro dependente
            </Botao>
          </div>
        </fieldset>
      )}

      {item.aceitaTrajeto && (
        <fieldset className="flex flex-col gap-3">
          <legend className="text-tabela font-semibold text-texto">Trajeto</legend>
          <Campo rotulo="Origem">
            <Entrada
              maxLength={200}
              value={trajeto.origem}
              onChange={(e) => setTrajeto({ ...trajeto, origem: e.target.value })}
            />
          </Campo>
          <Campo rotulo="Destino">
            <Entrada
              maxLength={200}
              value={trajeto.destino}
              onChange={(e) => setTrajeto({ ...trajeto, destino: e.target.value })}
            />
          </Campo>
          <Campo rotulo="Linhas ou meios" erro={erros.trajeto}>
            <Entrada
              maxLength={255}
              placeholder="Ex.: Ônibus 175T-10 e Metrô Linha 2"
              value={trajeto.linhasMeios}
              onChange={(e) => setTrajeto({ ...trajeto, linhasMeios: e.target.value })}
            />
          </Campo>
        </fieldset>
      )}

      <Campo rotulo="Começar em" erro={erros.dataInicioDesejada}>
        <Entrada
          type="date"
          lang="pt-BR"
          value={inicio}
          onChange={(e) => setInicio(e.target.value)}
        />
      </Campo>
      <CaixaDeSelecao
        marcada={autoriza}
        aoMudar={setAutoriza}
        rotulo="Autorizo o desconto mensal em folha"
      />
      <p className="text-pequeno text-texto-suave">{TEXTO_DO_PRAZO_DE_RESPOSTA}</p>
      {item.ultimoPedido?.situacao === 'RECUSADO' && item.ultimoPedido.motivoRecusa && (
        <p className="text-pequeno text-texto-suave">
          Seu último pedido foi recusado: {item.ultimoPedido.motivoRecusa}
        </p>
      )}
      {erroGeral && <Aviso tom="erro">{erroGeral}</Aviso>}
      <div className="flex justify-end gap-3">
        <Botao variante="secundario" onClick={aoFechar} disabled={pedir.isPending}>
          Cancelar
        </Botao>
        <Botao type="submit" carregando={pedir.isPending}>
          Enviar pedido
        </Botao>
      </div>
    </form>
  )
}
