import { screen, waitFor, within } from '@testing-library/react'
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { parse } from 'yaml'
import { UFS } from '../../shared/formato/ufs'
import { exemplos } from '../../shared/mocks/gerado/handlers'
import { capturar, problema } from '../../test/capturar'
import { renderizarTela } from '../../test/telas'

type Lista = { itens: Record<string, unknown>[] } & Record<string, unknown>
const USUARIOS = exemplos.listarUsuariosDoSistema.corpo as Lista

describe('ufs', () => {
  it('UFS tem as 27 siglas do contrato, na ordem do enum', () => {
    const contrato = parse(readFileSync('../contrato/openapi.yaml', 'utf8')) as {
      components: { schemas: { EmpresaUf: { enum: string[] } } }
    }
    expect(UFS).toEqual(contrato.components.schemas.EmpresaUf.enum)
    expect(UFS).toHaveLength(27)
  })
})

describe('Cadastros: Empresa', () => {
  it('mostra o formulario preenchido e os apoios', async () => {
    const leitura = capturar('obterEmpresa')
    renderizarTela('/rh/cadastros/empresa')
    expect(await screen.findByLabelText('Nome da empresa')).toHaveValue('Comercial Aurora Ltda')
    expect(screen.getByRole('combobox', { name: 'UF' })).toHaveTextContent('SP')
    expect(screen.getByLabelText('Município')).toHaveValue('São Paulo')
    expect(screen.getByLabelText('Horário padrão de entrada')).toHaveValue('08:00')
    expect(screen.getByLabelText('Horário padrão de saída')).toHaveValue('17:48')
    expect(screen.getByLabelText('Tolerância de atraso, em minutos')).toHaveValue(10)
    expect(screen.getByLabelText('Dia do pagamento')).toHaveValue(6)
    expect(
      screen.getByText(/A saída pode ser menor que a entrada \(turno que atravessa a meia-noite\)/),
    ).toBeInTheDocument()
    leitura.conforme()
  })

  it('404 EMPRESA_NAO_CONFIGURADA nao e erro: formulario vazio com a faixa', async () => {
    capturar(
      'obterEmpresa',
      problema(404, {
        codigo: 'EMPRESA_NAO_CONFIGURADA',
        detail: 'A empresa ainda não foi configurada.',
      }),
    )
    renderizarTela('/rh/cadastros/empresa')
    expect(await screen.findByText('A empresa ainda não foi configurada.')).toBeInTheDocument()
    expect(screen.getByLabelText('Nome da empresa')).toHaveValue('')
    expect(screen.queryByRole('button', { name: 'Tentar de novo' })).toBeNull()
  })

  it('valida tolerancia de 0 a 120 e dia de 1 a 31', async () => {
    const salvar = capturar('salvarEmpresa')
    const { user } = renderizarTela('/rh/cadastros/empresa')
    const tolerancia = await screen.findByLabelText('Tolerância de atraso, em minutos')
    await user.clear(tolerancia)
    await user.type(tolerancia, '200')
    const dia = screen.getByLabelText('Dia do pagamento')
    await user.clear(dia)
    await user.type(dia, '40')
    await user.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(await screen.findByText('Use um número inteiro de 0 a 120.')).toBeInTheDocument()
    expect(screen.getByText('Use um dia de 1 a 31.')).toBeInTheDocument()
    expect(salvar.quantas()).toBe(0)
  })

  it('envia o corpo conforme; sucesso mostra Configuração salva. e o nome novo no menu', async () => {
    const salvar = capturar('salvarEmpresa')
    const { user } = renderizarTela('/rh/cadastros/empresa')
    const nome = await screen.findByLabelText('Nome da empresa')
    await user.clear(nome)
    await user.type(nome, 'Aurora Nova Ltda')
    capturar('obterEmpresa', {
      corpo: { ...(exemplos.obterEmpresa.corpo as object), nomeEmpresa: 'Aurora Nova Ltda' },
    })
    await user.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(salvar.quantas()).toBe(1))
    expect(salvar.ultima().corpo).toEqual({
      nomeEmpresa: 'Aurora Nova Ltda',
      uf: 'SP',
      municipio: 'São Paulo',
      horarioEntrada: '08:00',
      horarioSaida: '17:48',
      toleranciaAtrasoMinutos: 10,
      diaPagamento: 6,
    })
    salvar.conforme()
    expect(await screen.findByText('Configuração salva.')).toBeInTheDocument()
    const menu = screen.getByRole('navigation', { name: 'Menu principal' }).closest('aside')!
    expect(await within(menu).findByText('Aurora Nova Ltda')).toBeInTheDocument()
  })

  it('400 por campo marca o campo; erro de leitura diferente de 404 mostra Tentar de novo', async () => {
    capturar('salvarEmpresa', problema(400, { errors: { diaPagamento: ['Dia inválido.'] } }))
    const { user, unmount } = renderizarTela('/rh/cadastros/empresa')
    await screen.findByLabelText('Nome da empresa')
    await user.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(await screen.findByText('Dia inválido.')).toBeInTheDocument()
    unmount()
    capturar('obterEmpresa', problema(500))
    renderizarTela('/rh/cadastros/empresa')
    expect(await screen.findByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
  })
})

describe('Cadastros: Usuários do sistema', () => {
  it('tabela com as colunas, Colaborador, Nunca entrou, filtros, e a requisicao conforme', async () => {
    const lista = capturar('listarUsuariosDoSistema')
    const { user } = renderizarTela('/rh/cadastros/usuarios')
    const tabela = await screen.findByRole('table', { name: 'Usuários do sistema' })
    expect(
      within(tabela)
        .getAllByRole('columnheader')
        .map((c) => c.textContent),
    ).toEqual([
      'Pessoa',
      'Nível de acesso',
      'Situação',
      'Acesso',
      'Último acesso',
      'Convite enviado em',
      'Ações',
    ])
    expect(await within(tabela).findAllByText('Colaborador')).toHaveLength(3)
    expect(within(tabela).getAllByText('Nunca entrou')).toHaveLength(2)
    expect(within(tabela).getByText('Desativado')).toBeInTheDocument()
    await user.click(screen.getByRole('combobox', { name: 'Acesso' }))
    await user.click(await screen.findByRole('option', { name: 'Desativados' }))
    await waitFor(() => expect(lista.ultima().consulta.get('ativo')).toBe('false'))
    lista.conforme()
  })

  it('botoes por linha: Bruno so Desativar; Rafael Reenviar e Desativar; Lucas Enviar convite e nenhum Ativar nem Desativar', async () => {
    renderizarTela('/rh/cadastros/usuarios')
    const tabela = await screen.findByRole('table', { name: 'Usuários do sistema' })
    const nomes = (linha: HTMLElement) =>
      within(linha)
        .getAllByRole('button')
        .map((b) => b.textContent)
    expect(nomes((await within(tabela).findByText('Bruno Lima')).closest('tr')!)).toEqual([
      'Desativar',
    ])
    expect(nomes(within(tabela).getByText('Rafael Torres').closest('tr')!)).toEqual([
      'Reenviar convite',
      'Desativar',
    ])
    expect(nomes(within(tabela).getByText('Lucas Prado').closest('tr')!)).toEqual([
      'Enviar convite',
    ])
  })

  it('uma linha desativada com convite vencido tem Ativar, sem confirmacao, conforme', async () => {
    capturar('listarUsuariosDoSistema', {
      corpo: {
        ...USUARIOS,
        itens: [{ ...USUARIOS.itens[1], ativo: false, situacao: 'CONVITE_EXPIRADO' }],
      },
    })
    const ativar = capturar('ativarUsuarioDoSistema')
    const { user } = renderizarTela('/rh/cadastros/usuarios')
    await user.click(await screen.findByRole('button', { name: 'Ativar' }))
    await waitFor(() => expect(ativar.quantas()).toBe(1))
    expect(screen.queryByRole('alertdialog')).toBeNull()
    expect(ativar.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '104' })
    ativar.conforme()
  })

  it('Reenviar convite envia e avisa; e a pagina e refeita', async () => {
    const lista = capturar('listarUsuariosDoSistema')
    const convite = capturar('enviarConviteDeAcesso')
    const { user } = renderizarTela('/rh/cadastros/usuarios')
    await user.click(await screen.findByRole('button', { name: 'Reenviar convite' }))
    expect(
      await screen.findByText('Convite enviado para rafael.torres@empresa.com.br.'),
    ).toBeInTheDocument()
    expect(convite.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '104' })
    convite.conforme()
    await waitFor(() => expect(lista.quantas()).toBeGreaterThan(1))
  })

  it('Desativar pede confirmacao com o texto da nota e chama a rota da linha', async () => {
    const desativar = capturar('desativarUsuarioDoSistema')
    const { user } = renderizarTela('/rh/cadastros/usuarios')
    const tabela = await screen.findByRole('table', { name: 'Usuários do sistema' })
    const linha = (await within(tabela).findByText('Bruno Lima')).closest('tr')!
    await user.click(within(linha).getByRole('button', { name: 'Desativar' }))
    const dialogo = await screen.findByRole('alertdialog', {
      name: 'Desativar o acesso de Bruno Lima?',
    })
    expect(
      within(dialogo).getByText(/O login fica bloqueado e as sessões abertas dela são encerradas/),
    ).toBeInTheDocument()
    await user.click(within(dialogo).getByRole('button', { name: 'Desativar' }))
    await waitFor(() => expect(desativar.quantas()).toBe(1))
    expect(desativar.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '12' })
    desativar.conforme()
  })

  it('a propria linha tem Desativar desabilitado com a dica', async () => {
    capturar('listarUsuariosDoSistema', {
      corpo: {
        ...USUARIOS,
        itens: [{ ...USUARIOS.itens[0], funcionarioId: 1, nome: 'Ana Souza' }],
      },
    })
    renderizarTela('/rh/cadastros/usuarios')
    const botao = await screen.findByRole('button', { name: 'Desativar' })
    expect(botao).toBeDisabled()
    expect(botao).toHaveAttribute('title', 'Você não pode desativar o seu próprio login.')
  })

  it('vazio e erro', async () => {
    capturar('listarUsuariosDoSistema', {
      corpo: { ...USUARIOS, itens: [], total: 0, totalPaginas: 0 },
    })
    const { unmount } = renderizarTela('/rh/cadastros/usuarios')
    expect(await screen.findByText('Nenhum usuário encontrado.')).toBeInTheDocument()
    unmount()
    capturar('listarUsuariosDoSistema', problema(500))
    renderizarTela('/rh/cadastros/usuarios')
    expect(await screen.findByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
  })
})
