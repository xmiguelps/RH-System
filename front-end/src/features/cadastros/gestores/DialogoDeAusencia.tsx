import { useState } from 'react'
import type { Esquema } from '../../../shared/api/tipos'
import { hojeEmSaoPaulo } from '../../../shared/formato/datas'
import { Botao } from '../../../shared/ui/Botao'
import { Campo } from '../../../shared/ui/Campo'
import { Entrada } from '../../../shared/ui/Entrada'
import { Janela } from '../../../shared/ui/Janela'

type Gestor = Esquema<'GestorItem'>

function Conteudo({
  gestor,
  aoFechar,
  aoConfirmar,
  enviando,
  erroDoServidor,
}: {
  gestor: Gestor
  aoFechar: () => void
  aoConfirmar: (ausenteAte: string | null) => void
  enviando: boolean
  erroDoServidor: string | null
}) {
  const [dataDeVolta, setDataDeVolta] = useState(gestor.ausenteAte ?? '')
  const [erro, setErro] = useState<string | null>(null)
  const confirmar = () => {
    if (dataDeVolta && dataDeVolta <= hojeEmSaoPaulo()) {
      setErro('A data de volta precisa ser depois de hoje.')
      return
    }
    setErro(null)
    aoConfirmar(dataDeVolta || null)
  }
  return (
    <Janela
      aberta
      aoFechar={aoFechar}
      titulo={`Marcar ${gestor.funcionario.nome} como ausente`}
      tamanho="media"
      rodape={
        <>
          <Botao variante="secundario" onClick={aoFechar}>
            Cancelar
          </Botao>
          <Botao carregando={enviando} onClick={confirmar}>
            Marcar como ausente
          </Botao>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <p className="text-corpo text-texto-suave">
          Enquanto estiver marcado, a etapa do gestor nos pedidos de ponto e de férias da equipe é
          aprovada sozinha e o RH ainda decide depois. Os pedidos que já esperam por ele também
          passam para o RH, menos pedidos de férias com sugestão de outra data em andamento.
        </p>
        <Campo rotulo="Data de volta (opcional)" erro={erro ?? erroDoServidor}>
          <Entrada
            type="date"
            lang="pt-BR"
            value={dataDeVolta}
            onChange={(evento) => setDataDeVolta(evento.target.value)}
          />
        </Campo>
      </div>
    </Janela>
  )
}

export function DialogoDeAusencia({
  gestor,
  aberto,
  aoFechar,
  aoConfirmar,
  enviando = false,
  erroDoServidor = null,
}: {
  gestor: Gestor
  aberto: boolean
  aoFechar: () => void
  aoConfirmar: (ausenteAte: string | null) => void
  enviando?: boolean
  erroDoServidor?: string | null
}) {
  if (!aberto) return null
  return (
    <Conteudo
      gestor={gestor}
      aoFechar={aoFechar}
      aoConfirmar={aoConfirmar}
      enviando={enviando}
      erroDoServidor={erroDoServidor}
    />
  )
}
