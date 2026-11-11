import { screen, waitFor } from '@testing-library/react'
import type { UserEvent } from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type { Perfil } from '../shared/api/tipos'
import { renderizarTela } from '../test/telas'
import { acharTextoProibido, rotulosDaTela } from '../test/textosProibidos'

type Caso = {
  rota: string
  perfil?: Perfil
  /** Espera a tela ficar pronta (com os dados) antes de ler os rótulos. */
  pronta: () => Promise<unknown>
  /** Para chegar a um modal da tela. */
  acao?: (user: UserEvent) => Promise<unknown>
  textos: readonly string[]
}

const fichaCarregada = () => screen.findByText('Bruno Lima', { selector: 'p' })

/** O que o dono cortou de cada tela (`telas\fatia-1.md`). */
const TELAS_E_TEXTOS_PROIBIDOS: readonly (readonly [string, Caso])[] = [
  [
    'lista de colaboradores',
    {
      rota: '/rh/colaboradores',
      pronta: () => screen.findByRole('table', { name: 'Colaboradores' }),
      textos: ['Experiência'],
    },
  ],
  [
    'ficha do colaborador',
    { rota: '/rh/colaboradores/12', pronta: fichaCarregada, textos: ['Unidade', 'Matriz', '***'] },
  ],
  [
    'Cadastros',
    {
      rota: '/rh/cadastros/cargos',
      pronta: () => screen.findByRole('heading', { level: 2, name: 'Cargos' }),
      textos: ['Níveis de acesso', 'chave de aprovação automática'],
    },
  ],
  [
    'aba Documentos',
    {
      rota: '/rh/colaboradores/12?aba=documentos',
      pronta: () => screen.findByRole('table', { name: /Documentos/ }),
      textos: ['Lembrar'],
    },
  ],
  [
    'modal de documento',
    {
      rota: '/rh/colaboradores/12?aba=documentos',
      pronta: () => screen.findByRole('table', { name: /Documentos/ }),
      acao: async (user) => {
        await user.click(screen.getByRole('button', { name: 'Adicionar documento' }))
        await screen.findByRole('dialog', { name: /^Adicionar documento de / })
      },
      textos: ['Ela recebe um aviso'],
    },
  ],
  [
    'log de auditoria',
    {
      rota: '/rh/auditoria',
      pronta: () => screen.findByRole('table', { name: /auditoria/i }),
      textos: ['Ninguém consegue editar nem apagar'],
    },
  ],
  [
    'usuarios do sistema',
    {
      rota: '/rh/cadastros/usuarios',
      pronta: () => screen.findByRole('table', { name: /Usuários/ }),
      textos: ['Excluir', 'Apagar'],
    },
  ],
  // Fatia 3 (ficha 3, item 13)
  [
    'Meu ponto (33 e 47)',
    {
      rota: '/eu/meu-ponto',
      pronta: () => screen.findByRole('list', { name: 'Últimos lançamentos' }),
      textos: ['Compensação'],
    },
  ],
  [
    'extrato completo do banco de horas',
    {
      rota: '/eu/meu-ponto/extrato',
      pronta: () => screen.findByRole('table', { name: 'Lançamentos do banco de horas' }),
      textos: ['Compensação'],
    },
  ],
  [
    'ponto da equipe (54)',
    {
      rota: '/gestor/ponto-da-equipe',
      perfil: 'Gestor',
      pronta: () => screen.findByRole('table', { name: 'Hoje no setor' }),
      textos: ['Baixar espelho do setor'],
    },
  ],
  [
    'aba Ponto da ficha (04)',
    {
      rota: '/rh/colaboradores/12?aba=ponto',
      pronta: () => screen.findByRole('table', { name: 'Ponto do mês' }),
      textos: ['Lançar no banco de horas'],
    },
  ],
  [
    'formulario de feriado (12)',
    {
      rota: '/rh/ponto-e-feriados',
      pronta: () => screen.findByRole('list', { name: 'Feriados deste mês' }),
      textos: ['Vale para', 'Todas as unidades'],
    },
  ],
  [
    'cadastro de colaborador',
    {
      rota: '/rh/colaboradores/novo',
      pronta: () => screen.findByRole('dialog', { name: 'Adicionar colaborador' }),
      textos: ['Matrícula', 'Unidade'],
    },
  ],
  // Fatia 4 (ficha 4, item 12)
  ...(
    [
      ['avisos do RH (27)', '/rh/avisos', 'RH', () => screen.findByText('Quem viu')],
      [
        'avisos da pessoa (37)',
        '/eu/avisos',
        'Funcionario',
        () => screen.findByRole('list', { name: 'Lista de avisos' }),
      ],
      [
        'avisos do gestor (50)',
        '/eu/avisos',
        'Gestor',
        () => screen.findByRole('list', { name: 'Lista de avisos' }),
      ],
      [
        'aviso aberto (38)',
        '/eu/avisos/409',
        'Funcionario',
        () => screen.findByRole('heading', { level: 3, name: 'O que mudou' }),
      ],
    ] as const
  ).map(
    ([nome, rota, perfil, pronta]) =>
      [
        nome,
        {
          rota,
          perfil,
          pronta,
          textos: ['Para:', 'Baixar a política completa', 'ou para setores', 'e para o seu setor'],
        },
      ] as const,
  ),
  [
    'novo aviso (28)',
    {
      rota: '/rh/avisos/novo',
      pronta: () => screen.findByRole('dialog', { name: 'Novo aviso' }),
      textos: ['Para quem', 'Enviar por e-mail também', 'e anexo'],
    },
  ],
  [
    'holerites da pessoa (36)',
    {
      rota: '/eu/holerites',
      perfil: 'Funcionario',
      pronta: () => screen.findByText('Setembro de 2026'),
      textos: ['Novo'],
    },
  ],
  [
    'holerites do gestor (49)',
    {
      rota: '/eu/holerites',
      perfil: 'Gestor',
      pronta: () => screen.findByText('Setembro de 2026'),
      textos: ['Novo'],
    },
  ],
  [
    'modal Enviar holerite (08)',
    {
      rota: '/rh/colaboradores/12?aba=holerites',
      pronta: () => screen.findByText('Setembro de 2026'),
      acao: async (user) => {
        await user.click(screen.getByRole('button', { name: 'Enviar holerite' }))
        await screen.findByRole('dialog', { name: 'Enviar holerite de Bruno' })
      },
      textos: ['Avisar', 'por e-mail quando publicar'],
    },
  ],
  [
    'nova advertencia do RH (15)',
    {
      rota: '/rh/advertencias/nova',
      pronta: () => screen.findByRole('dialog', { name: 'Nova advertência' }),
      textos: ['Avisar o colaborador'],
    },
  ],
  [
    'registrar advertencia do gestor (60)',
    {
      rota: '/gestor/advertencias/nova',
      perfil: 'Gestor',
      pronta: () => screen.findByRole('dialog', { name: 'Registrar advertência' }),
      textos: ['Avisar o colaborador'],
    },
  ],
  [
    'registrar advertencia pela ficha da equipe (53)',
    {
      rota: '/gestor/equipe/21',
      perfil: 'Gestor',
      pronta: () => screen.findByText('Diego Farias', { selector: 'p' }),
      acao: async (user) => {
        await user.click(screen.getByRole('button', { name: 'Registrar advertência' }))
        await screen.findByRole('dialog', { name: 'Registrar advertência' })
      },
      textos: ['Avisar o colaborador'],
    },
  ],
  [
    'advertencias do setor (59)',
    {
      rota: '/gestor/advertencias',
      perfil: 'Gestor',
      pronta: () => screen.findByText('Atraso na abertura da loja'),
      textos: ['Contestação', 'Resposta do RH'],
    },
  ],
  [
    'detalhe da advertencia do gestor',
    {
      rota: '/gestor/advertencias/109',
      perfil: 'Gestor',
      pronta: () => screen.findByText('Ainda sem ciência'),
      textos: ['Contestação', 'Resposta do RH'],
    },
  ],
  // Fatia 6 (ficha 6, seção 7): os cortes valem em todas as telas da fatia.
  ...(
    [
      [
        'treinamentos do RH (22)',
        '/rh/treinamentos',
        'RH',
        () => screen.findByText('LGPD na prática'),
      ],
      [
        'novo treinamento, Detalhes (23)',
        '/rh/treinamentos/novo',
        'RH',
        () => screen.findByRole('checkbox', { name: /Obrigatório/ }),
      ],
      [
        'Conteudo (24)',
        '/rh/treinamentos/6/editar?etapa=conteudo',
        'RH',
        () => screen.findByRole('list', { name: 'Módulos do treinamento' }),
      ],
      [
        'Certificado (25)',
        '/rh/treinamentos/6/editar?etapa=certificado',
        'RH',
        () => screen.findByRole('checkbox', { name: /Emitir certificado/ }),
      ],
      [
        'Publicacao (26)',
        '/rh/treinamentos/6/editar?etapa=publicacao',
        'RH',
        () => screen.findByRole('radio', { name: /Pessoas escolhidas/ }),
      ],
      [
        'meus treinamentos (41)',
        '/eu/treinamentos',
        'Funcionario',
        () => screen.findByRole('list', { name: 'Meus treinamentos' }),
      ],
      [
        'concluidos (42)',
        '/eu/treinamentos?aba=concluidos',
        'Funcionario',
        () => screen.findByRole('list', { name: 'Meus treinamentos' }),
      ],
      [
        'treinamentos do setor (58)',
        '/gestor/treinamentos',
        'Gestor',
        () => screen.findByRole('region', { name: 'Quem fez' }),
      ],
      [
        'fazer o treinamento',
        '/eu/treinamentos/2',
        'Funcionario',
        () => screen.findByRole('list', { name: 'Módulos' }),
      ],
    ] as const
  ).map(
    ([nome, rota, perfil, pronta]) =>
      [
        nome,
        {
          rota,
          perfil,
          pronta,
          textos: [
            'Quiz',
            'Nota',
            'Termina em',
            'Prazos até',
            'Aula presencial',
            'Híbrido',
            'Presencial',
            'Em breve',
            'Lembrar quem não terminou',
            'Lembrar quem falta',
            'Avisar por e-mail',
          ],
        },
      ] as const,
  ),
  // Fatia 5 (ficha 5, seção 7): os cortes valem em todas as telas da fatia.
  ...(
    [
      [
        'formularios do RH (16)',
        '/rh/formularios',
        'RH',
        () => screen.findByText('Pesquisa de clima 2026'),
      ],
      [
        'novo formulario, Detalhes (17)',
        '/rh/formularios/novo',
        'RH',
        () => screen.findByRole('radio', { name: /Sugestão/ }),
      ],
      [
        'Perguntas (18)',
        '/rh/formularios/12/editar?etapa=perguntas',
        'RH',
        () => screen.findByRole('list', { name: 'Perguntas do formulário' }),
      ],
      [
        'Verificacao (19)',
        '/rh/formularios/12/editar?etapa=verificacao',
        'RH',
        () => screen.findByRole('list', { name: 'Conferência' }),
      ],
      [
        'Publicacao (20)',
        '/rh/formularios/12/editar?etapa=publicacao',
        'RH',
        () => screen.findByRole('radio', { name: /Pessoas escolhidas/ }),
      ],
      [
        'quem respondeu (21)',
        '/rh/formularios/12/respondentes',
        'RH',
        () => screen.findByRole('table', { name: 'Candidatos' }),
      ],
      [
        'meus formularios (39)',
        '/eu/formularios',
        'Funcionario',
        () => screen.findByRole('list', { name: 'Meus formulários' }),
      ],
      [
        'respondidos (40)',
        '/eu/formularios?aba=respondidos',
        'Funcionario',
        () => screen.findByRole('list', { name: 'Meus formulários' }),
      ],
      [
        'formularios do setor (57)',
        '/gestor/formularios',
        'Gestor',
        () => screen.findByRole('region', { name: 'Quem respondeu' }),
      ],
    ] as const
  ).map(
    ([nome, rota, perfil, pronta]) =>
      [
        nome,
        {
          rota,
          perfil,
          pronta,
          textos: [
            'Quiz',
            'nota',
            'Eliminatória',
            'Nota dada pelo RH',
            'Nota mínima',
            'Enviar o resultado por e-mail',
            'Convidar aprovados',
            'agrupados por setor',
          ],
        },
      ] as const,
  ),
  [
    'aba Advertencias da ficha da equipe',
    {
      rota: '/gestor/equipe/21?aba=advertencias',
      perfil: 'Gestor',
      pronta: () => screen.findByText('Atraso na abertura da loja'),
      textos: ['Contestação', 'Resposta do RH'],
    },
  ],
]

describe('textos cortados pelo dono', { timeout: 15000 }, () => {
  it.each(TELAS_E_TEXTOS_PROIBIDOS)(
    '%s nao escreve os textos cortados pelo dono',
    async (_, caso) => {
      const { container, user } = renderizarTela(caso.rota, { perfil: caso.perfil })
      await caso.pronta()
      if (caso.acao) await caso.acao(user)
      await waitFor(() =>
        expect(screen.queryAllByRole('status', { name: /Carregando/ })).toHaveLength(0),
      )
      const rotulos = rotulosDaTela(container)
      expect(rotulos.length).toBeGreaterThan(0)
      const achados = caso.textos.filter((texto) => acharTextoProibido(rotulos, texto))
      expect(achados).toEqual([])
    },
  )
})
