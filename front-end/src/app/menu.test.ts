import { describe, expect, it } from 'vitest'
import type { Perfil } from '../shared/api/tipos'
import { PERFIS_POR_FAMILIA } from './caminhos'
import { MENU_POR_PERFIL, menuVisivel, todosOsCaminhosDoMenu, type GrupoDeMenu } from './menu'

function resumo(grupos: readonly GrupoDeMenu[]): string[] {
  return grupos.map((g) => `${g.rotulo}: ${g.itens.map((i) => i.rotulo).join(', ')}`)
}

const PERFIS: Perfil[] = ['RH', 'Funcionario', 'Gestor']

describe('menu', () => {
  it('o menu do RH, do funcionario e do gestor tem os grupos e itens do Figma, na ordem, quando todas as rotas existem', () => {
    expect(resumo(menuVisivel('RH', todosOsCaminhosDoMenu('RH')))).toEqual([
      'Visão geral: Painel',
      'Pessoas: Colaboradores, Advertências, Formulários, Treinamentos',
      'Rotina: Ponto e feriados, Avisos, Meus documentos',
      'Sistema: Cadastros, Log de auditoria',
      'Meu espaço: Meu ponto, Férias, Holerites, Minhas advertências, Meus formulários, Meus treinamentos, Benefícios',
    ])
    expect(resumo(menuVisivel('Funcionario', todosOsCaminhosDoMenu('Funcionario')))).toEqual([
      'Seu dia a dia: Início, Meu ponto, Férias, Holerites',
      'Comunicação: Avisos, Formulários',
      'Você na empresa: Treinamentos, Advertências, Benefícios, Meus documentos',
    ])
    expect(resumo(menuVisivel('Gestor', todosOsCaminhosDoMenu('Gestor')))).toEqual([
      'Seu dia a dia: Início, Meu ponto, Férias, Holerites, Meus documentos, Benefícios',
      'Seu setor: Painel da equipe, Ponto da equipe, Formulários, Treinamentos, Advertências',
      'Comunicação: Avisos',
      'Meu espaço: Meus formulários, Meus treinamentos, Minhas advertências',
    ])
  })

  it('Meu espaco (RH e gestor): cada item leva a rota pessoal certa e o grupo some enquanto as rotas nao existem', () => {
    const meuEspaco = (perfil: Perfil) =>
      MENU_POR_PERFIL[perfil].find((g) => g.rotulo === 'Meu espaço')
    expect(meuEspaco('RH')?.itens.map((i) => i.caminho)).toEqual([
      '/eu/meu-ponto',
      '/eu/ferias',
      '/eu/holerites',
      '/eu/advertencias',
      '/eu/formularios',
      '/eu/treinamentos',
      '/eu/beneficios',
    ])
    expect(meuEspaco('Gestor')?.itens.map((i) => i.caminho)).toEqual([
      '/eu/formularios',
      '/eu/treinamentos',
      '/eu/advertencias',
    ])
    expect(meuEspaco('Funcionario')).toBeUndefined()
    const soPainel = menuVisivel('RH', new Set(['/eu/inicio', '/rh/painel']))
    expect(soPainel.find((g) => g.rotulo === 'Meu espaço')).toBeUndefined()
    for (const perfil of ['RH', 'Gestor'] as const) {
      const item = meuEspaco(perfil)?.itens.find((i) => i.rotulo === 'Meus formulários')
      expect(item?.contador).toBe('formulariosPendentes')
    }
  })

  it('so aparece item cujo caminho tem rota e grupo sem item some', () => {
    expect(resumo(menuVisivel('Funcionario', new Set(['/eu/inicio'])))).toEqual([
      'Seu dia a dia: Início',
    ])
  })

  it('todo caminho do menu pertence a uma familia que o perfil pode abrir', () => {
    for (const perfil of PERFIS) {
      for (const caminho of todosOsCaminhosDoMenu(perfil)) {
        const familia = caminho.split('/')[1] as keyof typeof PERFIS_POR_FAMILIA
        expect(PERFIS_POR_FAMILIA[familia], `${perfil}: ${caminho}`).toContain(perfil)
      }
    }
  })

  it('cada contador do menu e entregue ao perfil que o usa', () => {
    const contadores = (perfil: Perfil) =>
      MENU_POR_PERFIL[perfil].flatMap((g) =>
        g.itens.flatMap((i) => (i.contador ? [i.contador] : [])),
      )
    expect(new Set(contadores('RH'))).toEqual(
      new Set(['pedidosDeAjusteDePonto', 'documentosPendentes', 'formulariosPendentes']),
    )
    expect(new Set(contadores('Funcionario'))).toEqual(
      new Set(['avisosNaoLidos', 'formulariosPendentes', 'documentosPendentes']),
    )
    expect(new Set(contadores('Gestor'))).toEqual(
      new Set([
        'documentosPendentes',
        'pendenciasDaEquipe',
        'avisosNaoLidos',
        'formulariosPendentes',
      ]),
    )
  })
})
