import { lazy, Suspense, type ReactElement } from 'react'
import { PaginaDeAuditoria } from '../features/auditoria/PaginaDeAuditoria'
import { CadastroDeBeneficios } from '../features/cadastros/beneficios/CadastroDeBeneficios'
import { CadastroDeCargos } from '../features/cadastros/cargos/CadastroDeCargos'
import { CadastroDeEmpresa } from '../features/cadastros/empresa/CadastroDeEmpresa'
import { CadastroDeGestores } from '../features/cadastros/gestores/CadastroDeGestores'
import { PaginaDeCadastros } from '../features/cadastros/PaginaDeCadastros'
import { CadastroDeSetores } from '../features/cadastros/setores/CadastroDeSetores'
import { CadastroDeUsuarios } from '../features/cadastros/usuarios/CadastroDeUsuarios'
import { ListaDeColaboradores } from '../features/colaboradores/ListaDeColaboradores'
import { PainelCadastrar } from '../features/colaboradores/PainelCadastrar'
import { PainelDaFicha } from '../features/colaboradores/PainelDaFicha'
import { ModalMeuAviso } from '../features/avisos/ModalMeuAviso'
import { PaginaAvisosRh } from '../features/avisos/PaginaAvisosRh'
import { PaginaMeusAvisos } from '../features/avisos/PaginaMeusAvisos'
import { PainelDoAviso } from '../features/avisos/PainelDoAviso'
import { PaginaMeusHolerites } from '../features/holerites/PaginaMeusHolerites'
import {
  PaginaAdvertenciasDaEquipe,
  RotaDetalheDaAdvertenciaDaEquipe,
  RotaRegistrarAdvertenciaDaEquipe,
} from '../features/advertencias/PaginaAdvertenciasDaEquipe'
import {
  PaginaAdvertenciasRh,
  RotaDetalheDaAdvertenciaRh,
  RotaNovaAdvertenciaRh,
} from '../features/advertencias/PaginaAdvertenciasRh'
import { PaginaMinhasAdvertencias } from '../features/advertencias/PaginaMinhasAdvertencias'
import { AssistenteDoFormulario } from '../features/formularios/AssistenteDoFormulario'
import {
  ModalRespostasDoCandidato,
  ModalRespostasDoFuncionario,
} from '../features/formularios/ModaisDeRespostas'
import { PaginaFormularioPublico } from '../features/formularios/PaginaFormularioPublico'
import { PaginaFormulariosDaEquipe } from '../features/formularios/PaginaFormulariosDaEquipe'
import { PaginaFormulariosRh } from '../features/formularios/PaginaFormulariosRh'
import { PaginaMeusFormularios } from '../features/formularios/PaginaMeusFormularios'
import { PaginaMinhasRespostas } from '../features/formularios/PaginaMinhasRespostas'
import { PaginaQuemRespondeu } from '../features/formularios/PaginaQuemRespondeu'
import { PaginaResponder } from '../features/formularios/PaginaResponder'
import { PaginaResultado } from '../features/formularios/PaginaResultado'
import { AssistenteDoTreinamento } from '../features/treinamentos/AssistenteDoTreinamento'
import { PaginaConferirCertificado } from '../features/treinamentos/PaginaConferirCertificado'
import { PaginaFazerTreinamento } from '../features/treinamentos/PaginaFazerTreinamento'
import { PaginaMeusTreinamentos } from '../features/treinamentos/PaginaMeusTreinamentos'
import { PaginaTreinamentosRh } from '../features/treinamentos/PaginaTreinamentosRh'
import { PaginaTreinamentosDaEquipe } from '../features/treinamentos/TelasDoGestor'
import { PaginaDefinirSenha } from '../features/autenticacao/PaginaDefinirSenha'
import { PaginaEsqueciSenha } from '../features/autenticacao/PaginaEsqueciSenha'
import { PaginaMeusDocumentos } from '../features/documentos/PaginaMeusDocumentos'
import { PainelDaFichaDaEquipe } from '../features/equipe/PainelDaFichaDaEquipe'
import { PaginaMeuPonto } from '../features/ponto/PaginaMeuPonto'
import { PaginaPontoDaEquipe } from '../features/ponto/PaginaPontoDaEquipe'
import { PaginaPontoEFeriados } from '../features/ponto/PaginaPontoEFeriados'
import {
  ModalPedidoDaEquipe,
  PainelAtestadosDaEquipe,
  PainelEspelhoDaPessoa,
} from '../features/ponto/PaineisDaEquipe'
import { PainelDeAtestadosRh } from '../features/ponto/PainelDeAtestadosRh'
import { PainelDoExtrato } from '../features/ponto/PainelDoExtrato'
import { Carregando } from '../shared/ui/Carregando'
import { PaginaDeEntrada } from './PaginaDeEntrada'
import { InicioDaPessoa } from './paginas/InicioDaPessoa'
import { InicioDoRh } from './paginas/InicioDoRh'

export type RotaDoPortal = {
  caminho: string
  elemento: ReactElement
  filhas?: readonly RotaDoPortal[]
}

// A tabela de rotas: uma linha por tela. O perfil vem da família (/rh, /gestor, /eu); o item do
// menu (menu.ts) aparece sozinho quando a rota dele existe.
export const ROTAS_RH: readonly RotaDoPortal[] = [
  { caminho: 'painel', elemento: <InicioDoRh /> },
  {
    caminho: 'colaboradores',
    elemento: <ListaDeColaboradores />,
    filhas: [
      { caminho: 'novo', elemento: <PainelCadastrar /> },
      { caminho: ':funcionarioId', elemento: <PainelDaFicha /> },
    ],
  },
  {
    caminho: 'advertencias',
    elemento: <PaginaAdvertenciasRh />,
    filhas: [
      { caminho: 'nova', elemento: <RotaNovaAdvertenciaRh /> },
      { caminho: ':advertenciaId', elemento: <RotaDetalheDaAdvertenciaRh /> },
    ],
  },
  {
    caminho: 'formularios',
    elemento: <PaginaFormulariosRh />,
    filhas: [
      { caminho: 'novo', elemento: <AssistenteDoFormulario /> },
      { caminho: ':formularioId/editar', elemento: <AssistenteDoFormulario /> },
    ],
  },
  {
    caminho: 'formularios/:formularioId/respondentes',
    elemento: <PaginaQuemRespondeu />,
    filhas: [
      { caminho: 'funcionarios/:funcionarioId', elemento: <ModalRespostasDoFuncionario /> },
      { caminho: 'candidatos/:candidatoId', elemento: <ModalRespostasDoCandidato /> },
    ],
  },
  { caminho: 'formularios/:formularioId/resultado', elemento: <PaginaResultado /> },
  {
    caminho: 'treinamentos',
    elemento: <PaginaTreinamentosRh />,
    filhas: [
      { caminho: 'novo', elemento: <AssistenteDoTreinamento /> },
      { caminho: ':treinamentoId/editar', elemento: <AssistenteDoTreinamento /> },
    ],
  },
  { caminho: 'auditoria', elemento: <PaginaDeAuditoria /> },
  {
    caminho: 'ponto-e-feriados',
    elemento: <PaginaPontoEFeriados />,
    filhas: [{ caminho: 'atestados', elemento: <PainelDeAtestadosRh /> }],
  },
  {
    caminho: 'avisos',
    elemento: <PaginaAvisosRh />,
    filhas: [
      { caminho: 'novo', elemento: <PainelDoAviso /> },
      { caminho: ':avisoId/editar', elemento: <PainelDoAviso /> },
    ],
  },
  {
    caminho: 'cadastros',
    elemento: <PaginaDeCadastros />,
    filhas: [
      { caminho: 'cargos', elemento: <CadastroDeCargos /> },
      { caminho: 'setores', elemento: <CadastroDeSetores /> },
      { caminho: 'gestores', elemento: <CadastroDeGestores /> },
      { caminho: 'beneficios', elemento: <CadastroDeBeneficios /> },
      { caminho: 'usuarios', elemento: <CadastroDeUsuarios /> },
      { caminho: 'empresa', elemento: <CadastroDeEmpresa /> },
    ],
  },
]

export const ROTAS_GESTOR: readonly RotaDoPortal[] = [
  { caminho: 'equipe/:funcionarioId', elemento: <PainelDaFichaDaEquipe /> },
  {
    caminho: 'ponto-da-equipe',
    elemento: <PaginaPontoDaEquipe />,
    filhas: [
      { caminho: 'pedidos/:pedidoId', elemento: <ModalPedidoDaEquipe /> },
      { caminho: 'espelho/:funcionarioId', elemento: <PainelEspelhoDaPessoa /> },
      { caminho: 'atestados', elemento: <PainelAtestadosDaEquipe /> },
    ],
  },
  { caminho: 'formularios', elemento: <PaginaFormulariosDaEquipe /> },
  { caminho: 'treinamentos', elemento: <PaginaTreinamentosDaEquipe /> },
  {
    caminho: 'advertencias',
    elemento: <PaginaAdvertenciasDaEquipe />,
    filhas: [
      { caminho: 'nova', elemento: <RotaRegistrarAdvertenciaDaEquipe /> },
      { caminho: ':advertenciaId', elemento: <RotaDetalheDaAdvertenciaDaEquipe /> },
    ],
  },
]

/** A vitrine só existe no build de prova (VITE_VITRINE=sim): no build normal o código nem vai ao dist. */
function rotasDaVitrine(): RotaDoPortal[] {
  if (import.meta.env.VITE_VITRINE !== 'sim') return []
  const Vitrine = lazy(() => import('./vitrine/Vitrine'))
  return [
    {
      caminho: 'vitrine',
      elemento: (
        <Suspense fallback={<Carregando />}>
          <Vitrine />
        </Suspense>
      ),
    },
  ]
}

export const ROTAS_EU: readonly RotaDoPortal[] = [
  { caminho: 'inicio', elemento: <InicioDaPessoa /> },
  { caminho: 'meus-documentos', elemento: <PaginaMeusDocumentos /> },
  {
    caminho: 'meu-ponto',
    elemento: <PaginaMeuPonto />,
    filhas: [{ caminho: 'extrato', elemento: <PainelDoExtrato /> }],
  },
  {
    caminho: 'avisos',
    elemento: <PaginaMeusAvisos />,
    filhas: [{ caminho: ':avisoId', elemento: <ModalMeuAviso /> }],
  },
  { caminho: 'holerites', elemento: <PaginaMeusHolerites /> },
  { caminho: 'advertencias', elemento: <PaginaMinhasAdvertencias /> },
  { caminho: 'formularios', elemento: <PaginaMeusFormularios /> },
  { caminho: 'formularios/:formularioId/responder', elemento: <PaginaResponder /> },
  { caminho: 'formularios/:formularioId/minhas-respostas', elemento: <PaginaMinhasRespostas /> },
  { caminho: 'treinamentos', elemento: <PaginaMeusTreinamentos /> },
  { caminho: 'treinamentos/:treinamentoId', elemento: <PaginaFazerTreinamento /> },
  ...rotasDaVitrine(),
]

export const ROTAS_PUBLICAS: readonly RotaDoPortal[] = [
  { caminho: '/entrar', elemento: <PaginaDeEntrada /> },
  { caminho: '/esqueci-senha', elemento: <PaginaEsqueciSenha /> },
  { caminho: '/definir-senha', elemento: <PaginaDefinirSenha /> },
  { caminho: '/formulario/:token', elemento: <PaginaFormularioPublico /> },
  { caminho: '/certificado', elemento: <PaginaConferirCertificado /> },
  { caminho: '/certificado/:codigo', elemento: <PaginaConferirCertificado /> },
]

export function caminhosDasRotas(): ReadonlySet<string> {
  const caminhos = new Set<string>()
  const familias: [string, readonly RotaDoPortal[]][] = [
    ['/rh', ROTAS_RH],
    ['/gestor', ROTAS_GESTOR],
    ['/eu', ROTAS_EU],
  ]
  for (const [prefixo, rotas] of familias) {
    for (const rota of rotas) caminhos.add(`${prefixo}/${rota.caminho}`)
  }
  return caminhos
}
