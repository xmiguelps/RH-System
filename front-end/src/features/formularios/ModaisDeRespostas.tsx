import { useNavigate, useParams, useSearchParams } from 'react-router'
import { $api, clienteHttp } from '../../shared/api/api'
import { useBaixarPorLink } from '../../shared/arquivos/useBaixarPorLink'
import { formatarDataDoInstante, formatarHora } from '../../shared/formato/datas'
import { inteiroDaUrl } from '../../shared/lista/valoresDeLista'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { Janela } from '../../shared/ui/Janela'
import { RespostasDadas } from './RespostasDadas'

function useVoltar() {
  const navegar = useNavigate()
  const formularioId = useParams().formularioId
  return () => navegar(`/rh/formularios/${formularioId}/respondentes`)
}

function useBaixarArquivo(formularioId: number) {
  const { baixar } = useBaixarPorLink()
  return (respostaId: number) =>
    void baixar(() =>
      clienteHttp.GET('/api/rh/formularios/{formularioId}/arquivos/{respostaId}/download', {
        params: { path: { formularioId, respostaId } },
      }),
    )
}

const quando = (instante: string) =>
  `Respondido em ${formatarDataDoInstante(instante)} às ${formatarHora(instante)}`

/** "Ver respostas" de uma pessoa (sem print). Avaliação leva `avaliadoId`; Sugestão, `respondidoEmUtc`. */
export function ModalRespostasDoFuncionario() {
  const voltar = useVoltar()
  const params = useParams()
  const [busca] = useSearchParams()
  const formularioId = inteiroDaUrl(params.formularioId) ?? 0
  const funcionarioId = inteiroDaUrl(params.funcionarioId) ?? 0
  const avaliadoId = inteiroDaUrl(busca.get('avaliadoId') ?? undefined)
  const respondidoEmUtc = busca.get('respondidoEmUtc') ?? undefined
  const baixarArquivo = useBaixarArquivo(formularioId)
  const consulta = $api.useQuery(
    'get',
    '/api/rh/formularios/{formularioId}/respostas/funcionarios/{funcionarioId}',
    {
      params: {
        path: { formularioId, funcionarioId },
        query: {
          ...(avaliadoId ? { avaliadoId } : {}),
          ...(respondidoEmUtc ? { respondidoEmUtc } : {}),
        },
      },
    },
  )
  const r = consulta.data
  return (
    <Janela
      aberta
      aoFechar={voltar}
      titulo={r ? `Respostas de ${r.funcionario.nome}` : 'Respostas'}
      tamanho="larga"
      rodape={<Botao onClick={voltar}>Fechar</Botao>}
    >
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} />
      ) : !r ? (
        <Esqueleto className="h-60 w-full" />
      ) : (
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-1">
            <p className="text-corpo text-texto">
              {[r.funcionario.cargo, r.funcionario.setor].filter(Boolean).join(', ')}
            </p>
            <p className="text-tabela text-texto-suave">{r.titulo}</p>
            <p className="text-tabela text-texto-suave">{quando(r.respondidoEmUtc)}</p>
            {r.avaliado && <p className="text-tabela text-texto">Avaliando: {r.avaliado.nome}</p>}
          </div>
          <RespostasDadas respostas={r.respostas} aoBaixarArquivo={baixarArquivo} />
        </div>
      )}
    </Janela>
  )
}

/** "Ver respostas" de um candidato (formulário por link). */
export function ModalRespostasDoCandidato() {
  const voltar = useVoltar()
  const params = useParams()
  const formularioId = inteiroDaUrl(params.formularioId) ?? 0
  const candidatoId = inteiroDaUrl(params.candidatoId) ?? 0
  const baixarArquivo = useBaixarArquivo(formularioId)
  const { baixar } = useBaixarPorLink()
  const consulta = $api.useQuery(
    'get',
    '/api/rh/formularios/{formularioId}/respostas/candidatos/{candidatoId}',
    { params: { path: { formularioId, candidatoId } } },
  )
  const r = consulta.data
  return (
    <Janela
      aberta
      aoFechar={voltar}
      titulo={r ? `Respostas de ${r.candidato.nome}` : 'Respostas'}
      tamanho="larga"
      rodape={<Botao onClick={voltar}>Fechar</Botao>}
    >
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} />
      ) : !r ? (
        <Esqueleto className="h-60 w-full" />
      ) : (
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-1">
            <p className="text-corpo text-texto">
              {[r.candidato.email, r.candidato.telefone].filter(Boolean).join(', ')}
            </p>
            <p className="text-tabela text-texto-suave">{r.titulo}</p>
            <p className="text-tabela text-texto-suave">{quando(r.candidato.respondidoEmUtc)}</p>
            {r.candidato.temCurriculo && (
              <div>
                <Botao
                  variante="secundario"
                  tamanho="pequeno"
                  onClick={() =>
                    void baixar(() =>
                      clienteHttp.GET(
                        '/api/rh/formularios/{formularioId}/candidatos/{candidatoId}/curriculo/download',
                        { params: { path: { formularioId, candidatoId } } },
                      ),
                    )
                  }
                >
                  Baixar currículo
                </Botao>
              </div>
            )}
          </div>
          <RespostasDadas respostas={r.respostas} aoBaixarArquivo={baixarArquivo} />
        </div>
      )}
    </Janela>
  )
}
