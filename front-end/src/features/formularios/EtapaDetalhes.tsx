import type { Esquema } from '../../shared/api/tipos'
import { AreaDeTexto } from '../../shared/ui/AreaDeTexto'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'
import { Campo } from '../../shared/ui/Campo'
import { Entrada } from '../../shared/ui/Entrada'
import { OpcoesEmCartao } from '../../shared/ui/OpcoesEmCartao'
import { travasDosDetalhes } from './regrasDoAssistente'
import { CARTOES_DE_TIPO, type TipoDoCartao } from './rotulosDosFormularios'

export type Detalhes = {
  titulo: string
  descricao: string
  cartao: TipoDoCartao
  anonimo: boolean
}

export type ErrosDosDetalhes = Partial<Record<'titulo' | 'descricao' | 'tipo' | 'anonimo', string>>

/** Tela 17: a etapa Detalhes (seis tipos; "Respostas anônimas" some na Sugestão). */
export function EtapaDetalhes({
  valores,
  aoMudar,
  erros,
  formulario,
}: {
  valores: Detalhes
  aoMudar: (v: Detalhes) => void
  erros: ErrosDosDetalhes
  formulario: Esquema<'FormularioDetalheRh'> | undefined
}) {
  const travas = travasDosDetalhes(formulario)
  const publicadoComNomeSemResposta =
    formulario?.situacao === 'PUBLICADO' && !formulario.anonimo && !formulario.temRespostas
  const tipoTravado = travas.tudo || (travas.tipoEAnonimo && !publicadoComNomeSemResposta)
  return (
    <div className="flex flex-col gap-5">
      <h2 className="text-t3 font-bold text-texto">Detalhes</h2>
      {travas.dica && <p className="text-pequeno text-texto-suave">{travas.dica}</p>}
      <Campo rotulo="Título" erro={erros.titulo}>
        <Entrada
          maxLength={200}
          value={valores.titulo}
          disabled={travas.tudo}
          onChange={(e) => aoMudar({ ...valores, titulo: e.target.value })}
        />
      </Campo>
      <Campo rotulo="Descrição para quem vai responder" erro={erros.descricao}>
        <AreaDeTexto
          rows={4}
          limite={1000}
          value={valores.descricao}
          disabled={travas.tudo}
          onChange={(e) => aoMudar({ ...valores, descricao: e.target.value })}
        />
      </Campo>
      <OpcoesEmCartao<TipoDoCartao>
        rotulo="Tipo de formulário"
        colunas={2}
        valor={valores.cartao}
        aoMudar={(cartao) =>
          aoMudar({ ...valores, cartao, anonimo: cartao === 'OUTRO' ? false : valores.anonimo })
        }
        opcoes={CARTOES_DE_TIPO.map((c) => ({ ...c, desabilitada: tipoTravado }))}
      />
      {erros.tipo && <p className="text-pequeno text-erro-texto">{erros.tipo}</p>}
      {valores.cartao !== 'OUTRO' && (
        <div className="flex flex-col gap-2 rounded-cartao bg-superficie-suave p-4">
          <CaixaDeSelecao
            marcada={valores.anonimo}
            aoMudar={(anonimo) => aoMudar({ ...valores, anonimo })}
            desabilitada={travas.tudo || travas.tipoEAnonimo}
            rotulo="Respostas anônimas"
            descricao="O RH e o gestor veem só o resultado geral de cada pergunta, nunca quem respondeu."
          />
          <ul className="list-disc pl-6 text-pequeno text-texto-suave">
            <li>Só publica para 5 pessoas ou mais.</li>
            <li>Depois de publicar, o anonimato, as perguntas e quem responde não mudam.</li>
            <li>O resultado só aparece depois de encerrar.</li>
            <li>
              Só aparecem as perguntas com 3 respostas ou mais (na avaliação, os avaliados com 3 ou
              mais).
            </li>
            <li>Não tem envio de arquivo nem link público.</li>
          </ul>
          {erros.anonimo && <p className="text-pequeno text-erro-texto">{erros.anonimo}</p>}
        </div>
      )}
    </div>
  )
}
