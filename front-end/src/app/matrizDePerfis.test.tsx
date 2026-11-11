import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { tokenDeAcesso } from '../features/autenticacao/armazenamentoDaSessao'
import type { Perfil } from '../shared/api/tipos'
import { servidor } from '../shared/mocks/servidor'
import { capturar } from '../test/capturar'
import { renderizarApp } from '../test/renderizarApp'
import { sessaoDeTeste } from '../test/sessao'
import { renderizarTela } from '../test/telas'
import { caminhosDasRotas } from './tabelaDeRotas'
import { menuVisivel } from './menu'

type RotaDaMatriz = {
  caminho: string
  /** Espera o conteúdo da tela (o título ou o diálogo). */
  aguarda: () => Promise<unknown>
  perfis: readonly Perfil[]
  /** A operação de negócio da tela: no 403 ela nunca é pedida. */
  operacao?: string
}

const titulo = (nivel: 1 | 2, nome: string) => () =>
  screen.findByRole('heading', { level: nivel, name: nome })
const dialogo = (nome: string) => () => screen.findByRole('dialog', { name: nome })
/** A ficha troca o diálogo do carregamento pelo da moldura: espera o nome da pessoa. */
const ficha = (pessoa: string) => () => screen.findByText(pessoa, { selector: 'p' })

const SO_RH: readonly Perfil[] = ['RH']
const PERFIS: readonly Perfil[] = ['RH', 'Gestor', 'Funcionario']

const ROTAS_DA_FATIA_1: readonly RotaDaMatriz[] = [
  {
    caminho: '/rh/colaboradores',
    aguarda: titulo(1, 'Colaboradores'),
    perfis: SO_RH,
    operacao: 'listarFuncionarios',
  },
  {
    caminho: '/rh/colaboradores/novo',
    aguarda: dialogo('Adicionar colaborador'),
    perfis: SO_RH,
    operacao: 'listarSetores',
  },
  {
    caminho: '/rh/colaboradores/12',
    aguarda: ficha('Bruno Lima'),
    perfis: SO_RH,
    operacao: 'obterFichaDoFuncionario',
  },
  {
    caminho: '/rh/colaboradores/12?aba=documentos',
    aguarda: () => screen.findByRole('tab', { name: 'Documentos', selected: true }),
    perfis: SO_RH,
    operacao: 'listarDocumentosDoFuncionario',
  },
  {
    caminho: '/rh/cadastros/cargos',
    aguarda: titulo(2, 'Cargos'),
    perfis: SO_RH,
    operacao: 'listarCargos',
  },
  {
    caminho: '/rh/cadastros/setores',
    aguarda: titulo(2, 'Setores'),
    perfis: SO_RH,
    operacao: 'listarSetores',
  },
  {
    caminho: '/rh/cadastros/gestores',
    aguarda: titulo(2, 'Gestores'),
    perfis: SO_RH,
    operacao: 'listarGestores',
  },
  {
    caminho: '/rh/cadastros/beneficios',
    aguarda: titulo(2, 'Benefícios'),
    perfis: SO_RH,
    operacao: 'listarBeneficiosDoCatalogo',
  },
  {
    caminho: '/rh/cadastros/usuarios',
    aguarda: titulo(2, 'Usuários do sistema'),
    perfis: SO_RH,
    operacao: 'listarUsuariosDoSistema',
  },
  // `obterEmpresa` também é pedido pelo menu (nome da empresa) em todos os perfis: aqui sem operação.
  { caminho: '/rh/cadastros/empresa', aguarda: titulo(2, 'Empresa'), perfis: SO_RH },
  {
    caminho: '/rh/auditoria',
    aguarda: titulo(1, 'Log de auditoria'),
    perfis: SO_RH,
    operacao: 'listarEventosDeAuditoria',
  },
  {
    caminho: '/gestor/equipe/21',
    aguarda: ficha('Diego Farias'),
    perfis: ['Gestor'],
    operacao: 'obterFichaDaEquipe',
  },
  {
    caminho: '/eu/meus-documentos',
    aguarda: titulo(1, 'Meus documentos'),
    perfis: PERFIS,
    operacao: 'listarMeusDocumentos',
  },
]

const CASOS = ROTAS_DA_FATIA_1.flatMap((rota) =>
  PERFIS.map((perfil) => [rota.caminho, perfil, rota] as const),
)

beforeEach(() => {
  localStorage.clear()
  tokenDeAcesso.definir(null)
})

describe('matriz de perfis da fatia 1', { timeout: 15000 }, () => {
  it.each(CASOS)(
    '%s como %s: perfil certo abre, perfil errado ve o 403 sem o conteudo e sem perder o menu',
    async (caminho, perfil, rota) => {
      const operacao = rota.operacao ? capturar(rota.operacao) : undefined
      renderizarTela(caminho, { perfil })
      // Com um painel aberto o resto fica inerte (fora da árvore de acessibilidade): `hidden`.
      const menu = await screen.findByRole('navigation', { name: 'Menu principal', hidden: true })
      if (rota.perfis.includes(perfil)) {
        expect(await rota.aguarda()).toBeInTheDocument()
        expect(screen.queryByText('Você não tem permissão para ver esta página.')).toBeNull()
        if (operacao) await waitFor(() => expect(operacao.quantas()).toBeGreaterThan(0))
      } else {
        expect(
          await screen.findByText('Você não tem permissão para ver esta página.'),
        ).toBeInTheDocument()
        expect(within(menu).getAllByRole('link').length).toBeGreaterThan(0)
        expect(screen.queryByRole('dialog')).toBeNull()
        if (operacao) expect(operacao.quantas()).toBe(0)
      }
    },
  )

  it.each(ROTAS_DA_FATIA_1.map((r) => [r.caminho, r] as const))(
    '%s sem sessao vai a /entrar?voltar=... e volta ao destino depois de entrar',
    async (caminho, rota) => {
      const { roteador } = renderizarApp({ rota: caminho })
      await waitFor(() => expect(roteador.state.location.pathname).toBe('/entrar'))
      expect(roteador.state.location.search).toBe(`?voltar=${encodeURIComponent(caminho)}`)
      servidor.use(
        http.post('*/api/auth/entrar', () => HttpResponse.json(sessaoDeTeste(rota.perfis[0]!))),
      )
      const usuario = userEvent.setup()
      await usuario.type(await screen.findByLabelText('E-mail'), 'ana.souza@empresa.com.br')
      await usuario.type(screen.getByLabelText('Senha'), 'Senha#Forte123')
      await usuario.click(screen.getByRole('button', { name: 'Entrar' }))
      const [caminhoSemBusca, busca = ''] = caminho.split('?')
      await waitFor(() => expect(roteador.state.location.pathname).toBe(caminhoSemBusca))
      expect(roteador.state.location.search).toBe(busca ? `?${busca}` : '')
      expect(await rota.aguarda()).toBeInTheDocument()
    },
  )

  it.each(['/esqueci-senha', '/definir-senha?token=teste'])(
    'a pagina publica %s abre sem sessao e com sessao, sem redirecionar',
    async (caminho) => {
      const [semBusca] = caminho.split('?')
      const a = renderizarApp({ rota: caminho })
      await screen.findByRole('heading', { level: 1 })
      expect(a.roteador.state.location.pathname).toBe(semBusca)
      a.unmount()
      const b = renderizarTela(caminho, { perfil: 'Funcionario' })
      await screen.findByRole('heading', { level: 1 })
      expect(b.roteador.state.location.pathname).toBe(semBusca)
    },
  )

  it('o menu de cada perfil so mostra o que tem rota', () => {
    const caminhos = caminhosDasRotas()
    const rotulos = (perfil: Perfil) =>
      menuVisivel(perfil, caminhos).flatMap((g) => g.itens.map((i) => i.rotulo))
    const grupos = (perfil: Perfil) => menuVisivel(perfil, caminhos).map((g) => g.rotulo)

    expect(rotulos('RH')).toEqual(
      expect.arrayContaining(['Colaboradores', 'Cadastros', 'Log de auditoria', 'Meus documentos']),
    )
    for (const perfil of ['Gestor', 'Funcionario'] as const) {
      expect(rotulos(perfil)).toContain('Meus documentos')
      for (const doRh of ['Colaboradores', 'Cadastros', 'Log de auditoria']) {
        expect(rotulos(perfil)).not.toContain(doRh)
      }
    }
    expect(rotulos('Gestor')).not.toContain('Painel da equipe')
    // Fatias 3 e 4: no "Meu espaço" do RH só aparecem os itens cuja rota já existe.
    const meuEspacoDoRh = menuVisivel('RH', caminhos).find((g) => g.rotulo === 'Meu espaço')
    expect(meuEspacoDoRh?.itens.map((i) => i.rotulo)).toEqual([
      'Meu ponto',
      'Holerites',
      'Minhas advertências',
      'Meus formulários',
      'Meus treinamentos',
    ])
    // O do gestor: "Meus formulários" (fatia 5) e "Minhas advertências" (fatia 4); treinamentos (fatia 6).
    const meuEspacoDoGestor = menuVisivel('Gestor', caminhos).find((g) => g.rotulo === 'Meu espaço')
    expect(meuEspacoDoGestor?.itens.map((i) => i.rotulo)).toEqual([
      'Meus formulários',
      'Meus treinamentos',
      'Minhas advertências',
    ])
    expect(grupos('Gestor')).toContain('Meu espaço')
  })
})
