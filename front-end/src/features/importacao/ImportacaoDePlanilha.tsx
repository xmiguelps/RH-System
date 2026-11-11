import { Download } from 'lucide-react'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import { formatarDataHora, formatarHora } from '../../shared/formato/datas'
import { formatarNumero } from '../../shared/formato/numeros'
import { plural } from '../../shared/formato/texto'
import { Aviso } from '../../shared/ui/Aviso'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'
import { CaixaDoFormularioDeAdmissao } from '../formularios/CaixaDoFormularioDeAdmissao'
import { CaixaDoOnboarding } from '../treinamentos/CaixaDoOnboarding'
import { SeletorDeArquivo } from '../../shared/ui/SeletorDeArquivo'
import { LIMITE_DA_PLANILHA_MB, TIPO_XLSX } from './rotulosDaImportacao'
import { TabelaDaPrevia } from './TabelaDaPrevia'
import type { Importacao } from './useImportacao'

const colaboradores = (n: number) =>
  `${formatarNumero(n)} ${plural(n, 'colaborador', 'colaboradores')}`

function Numero({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className="flex flex-col gap-1 rounded-cartao bg-superficie-suave p-4">
      <span className="text-tabela text-texto-suave">{rotulo}</span>
      <span className="text-t3 font-bold text-texto tabular-nums">{valor}</span>
    </div>
  )
}

function EscolherArquivo({ importacao }: { importacao: Importacao }) {
  const { etapa } = importacao
  const enviando = etapa.tipo === 'enviando'
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col gap-1">
        <h3 className="text-t3 font-bold text-texto">Importar planilha</h3>
        <p className="text-corpo text-texto-suave">
          Cadastre vários colaboradores de uma vez. Baixe o modelo, preencha no Excel e envie aqui.
          Só cadastra pessoas novas.
        </p>
      </div>
      <div>
        <Botao
          variante="secundario"
          onClick={() => void importacao.baixarModelo()}
          carregando={importacao.baixandoModelo}
          disabled={enviando}
        >
          <Download size={16} strokeWidth={1.75} aria-hidden="true" />
          {importacao.baixandoModelo ? 'Baixando...' : 'Baixar modelo'}
        </Botao>
      </div>
      {!!importacao.erroDoModelo && <AvisoDeErro erro={importacao.erroDoModelo} />}
      {etapa.tipo === 'escolher' && !!etapa.erro && <AvisoDeErro erro={etapa.erro} />}
      {enviando ? (
        <div
          role="status"
          className="flex flex-col items-center gap-2 rounded-cartao border border-dashed border-tracejado px-6 py-8 text-center"
        >
          <p className="text-corpo font-semibold text-texto">{etapa.arquivo.name}</p>
          <p className="text-pequeno text-texto-suave">Lendo a planilha...</p>
        </div>
      ) : (
        <SeletorDeArquivo
          arquivo={null}
          aoMudar={(arquivo) => {
            if (arquivo) void importacao.enviar(arquivo)
          }}
          tiposAceitos={[TIPO_XLSX]}
          tamanhoMaximoMb={LIMITE_DA_PLANILHA_MB}
          textoDeAjuda="Só .xlsx, até 5 MB e 500 linhas"
          erro={etapa.tipo === 'escolher' ? etapa.erroDoArquivo : undefined}
        />
      )}
    </div>
  )
}

function Previa({ importacao }: { importacao: Importacao }) {
  const { etapa } = importacao
  if (etapa.tipo !== 'previa') return null
  const { previa } = etapa
  const ate = formatarHora(previa.expiraEmUtc)
  return (
    <div className="flex flex-col gap-4">
      {previa.podeConfirmar ? (
        <Aviso tom="sucesso">
          {formatarNumero(previa.totalLinhas)}{' '}
          {plural(previa.totalLinhas, 'linha lida', 'linhas lidas')}, todas certas. Pode cadastrar.
        </Aviso>
      ) : (
        <Aviso tom="alerta">
          {formatarNumero(previa.totalLinhas)} linhas lidas: {formatarNumero(previa.linhasValidas)}{' '}
          {plural(previa.linhasValidas, 'certa', 'certas')} e {formatarNumero(previa.linhasComErro)}{' '}
          com erro. Corrija a planilha e envie de novo. Nada foi cadastrado.
        </Aviso>
      )}
      <p className="text-pequeno text-texto-suave">
        Arquivo: {previa.nomeArquivo}. Esta prévia vale até {ate}.
      </p>
      {!!etapa.erro && <AvisoDeErro erro={etapa.erro} />}
      <TabelaDaPrevia key={previa.importacaoId} previa={previa} />
      {previa.podeConfirmar ? (
        <fieldset className="flex flex-col gap-3 rounded-cartao bg-superficie-suave p-5">
          <legend className="mb-2 text-t3 font-bold text-texto">Depois de salvar</legend>
          <CaixaDeSelecao
            rotulo="Enviar convite de acesso ao portal"
            marcada={importacao.enviarConvite}
            aoMudar={importacao.setEnviarConvite}
            desabilitada={importacao.confirmando}
          />
          <CaixaDoOnboarding
            marcada={importacao.incluirNoOnboarding}
            aoMudar={importacao.setIncluirNoOnboarding}
            desabilitada={importacao.confirmando}
          />
          <CaixaDoFormularioDeAdmissao
            valor={importacao.formularioAdmissaoId}
            aoMudar={importacao.setFormularioAdmissaoId}
            desabilitada={importacao.confirmando}
          />
          <p className="text-pequeno text-texto-suave">
            Ninguém recebe senha: cada pessoa recebe um convite por e-mail para criar a sua. Nenhum
            e-mail sai pelas outras duas opções.
          </p>
        </fieldset>
      ) : (
        <p className="text-pequeno text-texto-suave">
          Só dá para cadastrar quando todas as linhas estão certas.
        </p>
      )}
    </div>
  )
}

function Resultado({ importacao }: { importacao: Importacao }) {
  const { etapa } = importacao
  if (etapa.tipo !== 'resultado') return null
  const { resultado } = etapa
  return (
    <div className="flex flex-col gap-4">
      <Aviso tom="sucesso">{colaboradores(resultado.totalCriados)} cadastrados.</Aviso>
      <div className="grid grid-cols-2 gap-3">
        <Numero rotulo="Convites na fila" valor={formatarNumero(resultado.convitesEnviados)} />
        <Numero
          rotulo="Incluídos no onboarding"
          valor={formatarNumero(resultado.incluidosNoOnboarding)}
        />
        <Numero
          rotulo="Incluídos no formulário de admissão"
          valor={formatarNumero(resultado.incluidosNoFormularioDeAdmissao)}
        />
        <Numero rotulo="Confirmada em" valor={formatarDataHora(resultado.confirmadaEmUtc)} />
      </div>
      <ul aria-label="Colaboradores cadastrados" className="flex flex-col divide-y divide-divisor">
        {resultado.funcionarios.map((f) => (
          <li key={f.id} className="flex items-center justify-between gap-3 py-3">
            <div className="flex flex-col">
              <span className="text-corpo font-semibold text-texto">{f.nome}</span>
              <span className="text-pequeno text-texto-suave">
                {[f.cargo, f.setor].filter(Boolean).join(' · ') || '—'}
              </span>
            </div>
            <BotaoLink
              variante="texto"
              tamanho="pequeno"
              to={`/rh/colaboradores/${f.id}`}
              aria-label={`Abrir ficha de ${f.nome}`}
            >
              Abrir ficha
            </BotaoLink>
          </li>
        ))}
      </ul>
    </div>
  )
}

/** O corpo do painel no modo "Importar planilha" (desenho do plano: o Figma não tem esta tela). */
export function CorpoDaImportacao({ importacao }: { importacao: Importacao }) {
  const { etapa } = importacao
  if (etapa.tipo === 'escolher' || etapa.tipo === 'enviando') {
    return <EscolherArquivo importacao={importacao} />
  }
  if (etapa.tipo === 'previa') return <Previa importacao={importacao} />
  if (etapa.tipo === 'resultado') return <Resultado importacao={importacao} />
  return <Aviso tom="info">{mensagemDeErro(etapa.erro)}</Aviso>
}

export function RodapeDaImportacao({
  importacao,
  aoFechar,
}: {
  importacao: Importacao
  aoFechar: () => void
}) {
  const { etapa } = importacao
  if (etapa.tipo === 'previa') {
    return (
      <>
        <Botao
          key="enviar-outra"
          variante="secundario"
          onClick={importacao.reiniciar}
          disabled={importacao.confirmando}
        >
          Enviar outra planilha
        </Botao>
        <Botao
          key="cadastrar"
          onClick={() => void importacao.confirmar()}
          disabled={!etapa.previa.podeConfirmar}
          carregando={importacao.confirmando}
        >
          {importacao.confirmando
            ? 'Cadastrando...'
            : `Cadastrar ${colaboradores(etapa.previa.linhasValidas)}`}
        </Botao>
      </>
    )
  }
  if (etapa.tipo === 'resultado') {
    return (
      <>
        <Botao key="fechar-resultado" variante="secundario" onClick={aoFechar}>
          Fechar
        </Botao>
        <Botao key="importar-outra" onClick={importacao.reiniciar}>
          Importar outra planilha
        </Botao>
      </>
    )
  }
  return (
    <Botao variante="secundario" onClick={aoFechar} disabled={etapa.tipo === 'enviando'}>
      Fechar
    </Botao>
  )
}
