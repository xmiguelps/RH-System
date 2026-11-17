import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { hojeEmSaoPaulo } from '../../shared/formato/datas'
import { Aviso } from '../../shared/ui/Aviso'
import { Botao } from '../../shared/ui/Botao'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { Janela } from '../../shared/ui/Janela'
import { useNotificacao } from '../../shared/ui/useNotificacao'

type Cartao = Esquema<'BeneficioAdesaoCartao'>
type Erros = Record<string, string>

/** Os campos que a API recusa vêm em `errors` (com a letra de cima ou de baixo): marca cada campo com o texto dele. */
function errosDeCampo(erro: unknown): Erros {
  const campos: Erros = {}
  if (erro instanceof ErroDeApi)
    for (const [chave, mensagens] of Object.entries(erro.errors))
      campos[chave.charAt(0).toLowerCase() + chave.slice(1)] = mensagens.join(' ')
  return campos
}

/** "Incluir dependente" (tela 44, sem print): o dependente só tem cobertura depois que o RH aprova. */
export function ModalIncluirDependente({
  cartao,
  aoFechar,
}: {
  cartao: Cartao | null
  aoFechar: () => void
}) {
  return (
    <Janela
      aberta={cartao !== null}
      aoFechar={aoFechar}
      titulo="Incluir dependente"
      tamanho="media"
    >
      {cartao && <CorpoDoDependente key={cartao.adesaoId} cartao={cartao} aoFechar={aoFechar} />}
    </Janela>
  )
}

function CorpoDoDependente({ cartao, aoFechar }: { cartao: Cartao; aoFechar: () => void }) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const pedir = $api.useMutation('post', '/api/eu/beneficios-adesoes/{adesaoId}/dependentes')
  const [nome, setNome] = useState('')
  const [parentesco, setParentesco] = useState('')
  const [nascimento, setNascimento] = useState('')
  const [erros, setErros] = useState<Erros>({})
  const [erroGeral, setErroGeral] = useState<string | null>(null)

  async function enviar() {
    if (pedir.isPending) return
    const novos: Erros = {}
    if (!nome.trim()) novos.nome = 'Escreva o nome do dependente.'
    if (!parentesco.trim()) novos.parentesco = 'Escreva o parentesco.'
    if (!nascimento) novos.dataNascimento = 'Escolha a data de nascimento.'
    else if (nascimento > hojeEmSaoPaulo())
      novos.dataNascimento = 'A data de nascimento não pode ser no futuro.'
    setErros(novos)
    setErroGeral(null)
    if (Object.keys(novos).length) return
    try {
      await pedir.mutateAsync({
        params: { path: { adesaoId: cartao.adesaoId } },
        body: { nome: nome.trim(), parentesco: parentesco.trim(), dataNascimento: nascimento },
      })
      notificacao.sucesso('Pedido enviado. O RH vai analisar.')
      await invalidarRota(queryClient, '/api/eu/beneficios')
      aoFechar()
    } catch (e) {
      const campos = errosDeCampo(e)
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
      <p className="text-corpo text-texto-suave">
        {cartao.beneficio.nome}. Enquanto o RH não aprovar, o dependente não tem cobertura.
      </p>
      <Campo rotulo="Nome" erro={erros.nome}>
        <Entrada maxLength={150} value={nome} onChange={(e) => setNome(e.target.value)} />
      </Campo>
      <Campo rotulo="Parentesco" erro={erros.parentesco}>
        <Entrada
          maxLength={30}
          value={parentesco}
          onChange={(e) => setParentesco(e.target.value)}
        />
      </Campo>
      <Campo rotulo="Data de nascimento" erro={erros.dataNascimento}>
        <Entrada
          type="date"
          lang="pt-BR"
          value={nascimento}
          onChange={(e) => setNascimento(e.target.value)}
        />
      </Campo>
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

/** "Atualizar trajeto" (tela 44, sem print): o trajeto atual continua valendo até o RH aprovar o novo. */
export function ModalAtualizarTrajeto({
  cartao,
  aoFechar,
}: {
  cartao: Cartao | null
  aoFechar: () => void
}) {
  return (
    <Janela aberta={cartao !== null} aoFechar={aoFechar} titulo="Atualizar trajeto" tamanho="media">
      {cartao && <CorpoDoTrajeto key={cartao.adesaoId} cartao={cartao} aoFechar={aoFechar} />}
    </Janela>
  )
}

function CorpoDoTrajeto({ cartao, aoFechar }: { cartao: Cartao; aoFechar: () => void }) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const pedir = $api.useMutation('post', '/api/eu/beneficios-adesoes/{adesaoId}/trajetos')
  const atual = cartao.trajetoAtual
  const [origem, setOrigem] = useState(atual?.origem ?? '')
  const [destino, setDestino] = useState(atual?.destino ?? '')
  const [linhasMeios, setLinhasMeios] = useState(atual?.linhasMeios ?? '')
  const [erros, setErros] = useState<Erros>({})
  const [erroGeral, setErroGeral] = useState<string | null>(null)

  async function enviar() {
    if (pedir.isPending) return
    const novos: Erros = {}
    if (!origem.trim()) novos.origem = 'Escreva a origem.'
    if (!destino.trim()) novos.destino = 'Escreva o destino.'
    if (!linhasMeios.trim()) novos.linhasMeios = 'Escreva as linhas ou os meios.'
    setErros(novos)
    setErroGeral(null)
    if (Object.keys(novos).length) return
    try {
      await pedir.mutateAsync({
        params: { path: { adesaoId: cartao.adesaoId } },
        body: { origem: origem.trim(), destino: destino.trim(), linhasMeios: linhasMeios.trim() },
      })
      notificacao.sucesso('Pedido enviado. O RH vai analisar.')
      await invalidarRota(queryClient, '/api/eu/beneficios')
      aoFechar()
    } catch (e) {
      const campos = errosDeCampo(e)
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
      <Aviso tom="info">O trajeto atual continua valendo até o RH aprovar o novo.</Aviso>
      <Campo rotulo="Origem" erro={erros.origem}>
        <Entrada maxLength={200} value={origem} onChange={(e) => setOrigem(e.target.value)} />
      </Campo>
      <Campo rotulo="Destino" erro={erros.destino}>
        <Entrada maxLength={200} value={destino} onChange={(e) => setDestino(e.target.value)} />
      </Campo>
      <Campo rotulo="Linhas ou meios" erro={erros.linhasMeios}>
        <Entrada
          maxLength={255}
          placeholder="Ex.: Ônibus 175T-10 e Metrô Linha 2"
          value={linhasMeios}
          onChange={(e) => setLinhasMeios(e.target.value)}
        />
      </Campo>
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
