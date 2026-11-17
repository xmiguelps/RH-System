import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { clienteHttp } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import type { Esquema } from '../../shared/api/tipos'
import { Aviso } from '../../shared/ui/Aviso'
import { Botao } from '../../shared/ui/Botao'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { Janela } from '../../shared/ui/Janela'
import { useNotificacao } from '../../shared/ui/useNotificacao'

type Item = Esquema<'FormularioRhItem'>

/** "Alterar prazo" do formulário publicado (`alterarPrazoDoFormulario`, contrato 0.3.0): só o dia do prazo, que não pode ser tirado nem ser anterior a hoje. O resto das recusas (recorrente, encerrado, não publicado) vem do contrato e aparece dentro da janela. */
export function ModalDoPrazoDoFormulario({
  formulario,
  aoFechar,
}: {
  formulario: Item | null
  aoFechar: () => void
}) {
  return (
    <Janela
      aberta={formulario !== null}
      aoFechar={aoFechar}
      titulo="Alterar o prazo"
      tamanho="media"
    >
      {formulario && <Corpo key={formulario.id} formulario={formulario} aoFechar={aoFechar} />}
    </Janela>
  )
}

function Corpo({ formulario, aoFechar }: { formulario: Item; aoFechar: () => void }) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const [prazo, setPrazo] = useState(formulario.prazo ?? '')
  const [salvando, setSalvando] = useState(false)
  const [erro, setErro] = useState<unknown>(null)
  const erroDoCampo = erro instanceof ErroDeApi ? erro.errors.prazo?.join(' ') : undefined

  async function salvar() {
    if (salvando) return
    setSalvando(true)
    setErro(null)
    try {
      await clienteHttp.PUT('/api/rh/formularios/{formularioId}/prazo', {
        params: { path: { formularioId: formulario.id } },
        body: { prazo },
      })
      notificacao.sucesso('Prazo alterado.')
      await invalidarRota(queryClient, '/api/rh/formularios')
      aoFechar()
    } catch (e) {
      setErro(e)
    } finally {
      setSalvando(false)
    }
  }

  return (
    <form
      className="flex flex-col gap-5"
      onSubmit={(evento) => {
        evento.preventDefault()
        void salvar()
      }}
    >
      <p className="text-corpo text-texto-suave">{formulario.titulo}</p>
      <Campo rotulo="Novo prazo" erro={erroDoCampo}>
        <Entrada type="date" value={prazo} onChange={(e) => setPrazo(e.target.value)} />
      </Campo>
      {erro !== null && !erroDoCampo && <Aviso tom="erro">{mensagemDeErro(erro)}</Aviso>}
      <div className="flex justify-end gap-3">
        <Botao variante="secundario" onClick={aoFechar} disabled={salvando}>
          Cancelar
        </Botao>
        <Botao type="submit" carregando={salvando} disabled={!prazo}>
          Salvar prazo
        </Botao>
      </div>
    </form>
  )
}
