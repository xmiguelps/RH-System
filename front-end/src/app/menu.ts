import type { Perfil } from '../shared/api/tipos'

export type ChaveDeContador =
  | 'avisosNaoLidos'
  | 'formulariosPendentes'
  | 'documentosPendentes'
  | 'pendenciasDaEquipe'
  | 'pedidosDeAjusteDePonto'

export type ItemDeMenu = { rotulo: string; caminho: string; contador?: ChaveDeContador }
export type GrupoDeMenu = { rotulo: string; itens: readonly ItemDeMenu[] }

// A tabela do menu (PNG 00, 32 e 46; telas\fatia-0.md). "Meu espaço": decisão do dono D-menu, 2026-11-02.
export const MENU_POR_PERFIL: Record<Perfil, readonly GrupoDeMenu[]> = {
  RH: [
    { rotulo: 'Visão geral', itens: [{ rotulo: 'Painel', caminho: '/rh/painel' }] },
    {
      rotulo: 'Pessoas',
      itens: [
        { rotulo: 'Colaboradores', caminho: '/rh/colaboradores' },
        { rotulo: 'Advertências', caminho: '/rh/advertencias' },
        { rotulo: 'Formulários', caminho: '/rh/formularios' },
        { rotulo: 'Treinamentos', caminho: '/rh/treinamentos' },
      ],
    },
    {
      rotulo: 'Rotina',
      itens: [
        {
          rotulo: 'Ponto e feriados',
          caminho: '/rh/ponto-e-feriados',
          contador: 'pedidosDeAjusteDePonto',
        },
        { rotulo: 'Avisos', caminho: '/rh/avisos' },
        {
          rotulo: 'Meus documentos',
          caminho: '/eu/meus-documentos',
          contador: 'documentosPendentes',
        },
      ],
    },
    {
      rotulo: 'Sistema',
      itens: [
        { rotulo: 'Cadastros', caminho: '/rh/cadastros' },
        { rotulo: 'Log de auditoria', caminho: '/rh/auditoria' },
      ],
    },
    {
      rotulo: 'Meu espaço',
      itens: [
        { rotulo: 'Meu ponto', caminho: '/eu/meu-ponto' },
        { rotulo: 'Férias', caminho: '/eu/ferias' },
        { rotulo: 'Holerites', caminho: '/eu/holerites' },
        { rotulo: 'Minhas advertências', caminho: '/eu/advertencias' },
        {
          rotulo: 'Meus formulários',
          caminho: '/eu/formularios',
          contador: 'formulariosPendentes',
        },
        { rotulo: 'Meus treinamentos', caminho: '/eu/treinamentos' },
        { rotulo: 'Benefícios', caminho: '/eu/beneficios' },
      ],
    },
  ],
  Funcionario: [
    {
      rotulo: 'Seu dia a dia',
      itens: [
        { rotulo: 'Início', caminho: '/eu/inicio' },
        { rotulo: 'Meu ponto', caminho: '/eu/meu-ponto' },
        { rotulo: 'Férias', caminho: '/eu/ferias' },
        { rotulo: 'Holerites', caminho: '/eu/holerites' },
      ],
    },
    {
      rotulo: 'Comunicação',
      itens: [
        { rotulo: 'Avisos', caminho: '/eu/avisos', contador: 'avisosNaoLidos' },
        { rotulo: 'Formulários', caminho: '/eu/formularios', contador: 'formulariosPendentes' },
      ],
    },
    {
      rotulo: 'Você na empresa',
      itens: [
        { rotulo: 'Treinamentos', caminho: '/eu/treinamentos' },
        { rotulo: 'Advertências', caminho: '/eu/advertencias' },
        { rotulo: 'Benefícios', caminho: '/eu/beneficios' },
        {
          rotulo: 'Meus documentos',
          caminho: '/eu/meus-documentos',
          contador: 'documentosPendentes',
        },
      ],
    },
  ],
  Gestor: [
    {
      rotulo: 'Seu dia a dia',
      itens: [
        { rotulo: 'Início', caminho: '/eu/inicio' },
        { rotulo: 'Meu ponto', caminho: '/eu/meu-ponto' },
        { rotulo: 'Férias', caminho: '/eu/ferias' },
        { rotulo: 'Holerites', caminho: '/eu/holerites' },
        {
          rotulo: 'Meus documentos',
          caminho: '/eu/meus-documentos',
          contador: 'documentosPendentes',
        },
        { rotulo: 'Benefícios', caminho: '/eu/beneficios' },
      ],
    },
    {
      rotulo: 'Seu setor',
      itens: [
        { rotulo: 'Painel da equipe', caminho: '/gestor/equipe', contador: 'pendenciasDaEquipe' },
        { rotulo: 'Ponto da equipe', caminho: '/gestor/ponto-da-equipe' },
        { rotulo: 'Formulários', caminho: '/gestor/formularios' },
        { rotulo: 'Treinamentos', caminho: '/gestor/treinamentos' },
        { rotulo: 'Advertências', caminho: '/gestor/advertencias' },
      ],
    },
    {
      rotulo: 'Comunicação',
      itens: [{ rotulo: 'Avisos', caminho: '/eu/avisos', contador: 'avisosNaoLidos' }],
    },
    {
      rotulo: 'Meu espaço',
      itens: [
        {
          rotulo: 'Meus formulários',
          caminho: '/eu/formularios',
          contador: 'formulariosPendentes',
        },
        { rotulo: 'Meus treinamentos', caminho: '/eu/treinamentos' },
        { rotulo: 'Minhas advertências', caminho: '/eu/advertencias' },
      ],
    },
  ],
}

/** Só os itens cuja rota existe; grupo sem item some. */
export function menuVisivel(
  perfil: Perfil,
  caminhosExistentes: ReadonlySet<string>,
): readonly GrupoDeMenu[] {
  return MENU_POR_PERFIL[perfil]
    .map((grupo) => ({
      ...grupo,
      itens: grupo.itens.filter((item) => caminhosExistentes.has(item.caminho)),
    }))
    .filter((grupo) => grupo.itens.length > 0)
}

export function todosOsCaminhosDoMenu(perfil: Perfil): ReadonlySet<string> {
  return new Set(MENU_POR_PERFIL[perfil].flatMap((grupo) => grupo.itens.map((i) => i.caminho)))
}
