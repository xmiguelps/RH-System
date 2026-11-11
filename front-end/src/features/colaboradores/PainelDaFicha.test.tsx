import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { Esquema } from '../../shared/api/tipos'
import { exemplos } from '../../shared/mocks/gerado/handlers'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar, problema } from '../../test/capturar'
import { escolherOpcao } from '../../test/formulario'
import { renderizarTela } from '../../test/telas'

type Ficha = Esquema<'FuncionarioFichaRh'>
const FICHA = exemplos.obterFichaDoFuncionario.corpo as Ficha

function comFicha(parcial: Partial<Ficha>) {
  return capturar('obterFichaDoFuncionario', { corpo: { ...FICHA, ...parcial } })
}

async function abrir(rota = '/rh/colaboradores/12', perfil: 'RH' | 'Gestor' = 'RH') {
  const tela = renderizarTela(rota, { perfil })
  await screen.findByRole('dialog', { name: 'Ficha do colaborador' })
  return tela
}

const SEED: Partial<Ficha> = {
  cpf: null,
  telefone: null,
  dataNascimento: null,
  rg: null,
  rgOrgaoEmissor: null,
  emailPessoal: null,
  endereco: {
    cep: null,
    logradouro: null,
    numero: null,
    complemento: null,
    bairro: null,
    cidade: null,
    uf: null,
  },
  cargo: null,
  setor: null,
  gestor: null,
  tipoContrato: null,
  perfil: null,
  jornadaSemanalHoras: null,
  matricula: null,
  beneficios: [],
}

afterEach(() => {
  vi.useRealTimers()
})

describe('Ficha do colaborador (tela 03): leitura', { timeout: 15000 }, () => {
  it('abre direto no endereco com a lista atras e as requisicoes sao conformes', async () => {
    const ficha = capturar('obterFichaDoFuncionario')
    const indicadores = capturar('obterIndicadoresDoFuncionario')
    await abrir()
    expect(await screen.findByText('Bruno Lima', { selector: 'p' })).toBeInTheDocument()
    expect(screen.getByRole('table', { name: 'Colaboradores', hidden: true })).toBeInTheDocument()
    await waitFor(() => expect(indicadores.quantas()).toBeGreaterThan(0))
    expect(ficha.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '12' })
    expect(indicadores.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '12' })
    ficha.conforme()
    indicadores.conforme()
  })

  it('cabecalho: nome, "Analista pleno, setor Financeiro, matrícula 00482", etiqueta e os botoes', async () => {
    await abrir()
    await screen.findByText('Bruno Lima', { selector: 'p' })
    const janela = screen.getByRole('dialog', { name: 'Ficha do colaborador' })
    expect(within(janela).getByText('Atestado')).toBeInTheDocument()
    expect(
      within(janela).getByText('Analista pleno, setor Financeiro, matrícula 00482'),
    ).toBeInTheDocument()
    expect(within(janela).getByText('Atestado')).toBeInTheDocument()
    expect(within(janela).getByRole('button', { name: 'Editar dados' })).toBeInTheDocument()
    expect(within(janela).getByRole('button', { name: 'Inativar' })).toBeInTheDocument()
    expect(within(janela).getByRole('link', { name: 'Colaboradores' })).toBeInTheDocument()
  })

  it('cargo, setor e matricula null omitem as partes do cabecalho', async () => {
    comFicha({ cargo: null, setor: null, matricula: null })
    await abrir()
    expect(await screen.findByText('Bruno Lima', { selector: 'p' })).toBeInTheDocument()
    expect(screen.queryByText(/matrícula/)).toBeNull()
  })

  it('Dados pessoais completos para o RH: CPF e telefone sem mascara', async () => {
    await abrir()
    expect(await screen.findByText('123.482.371-30')).toBeInTheDocument()
    expect(screen.getByText('(11) 98765-4410')).toBeInTheDocument()
    expect(screen.getByText('18 de junho')).toBeInTheDocument()
    expect(screen.getByText('bruno.lima@email.com')).toBeInTheDocument()
    expect(screen.getByText('45.678.912-3, SSP-SP')).toBeInTheDocument()
    expect(
      screen.getByText('Avenida Paulista, 1578, apto 42, Bela Vista, São Paulo/SP, CEP 01310-100'),
    ).toBeInTheDocument()
    expect(screen.queryByText(/\*\*\*/)).toBeNull()
  })

  it('Contrato: cargo, setor, gestora, tipo, jornada, admissao e nivel de acesso, sem Unidade', async () => {
    await abrir()
    expect(await screen.findByText('Marina Costa')).toBeInTheDocument()
    expect(screen.getByText('CLT')).toBeInTheDocument()
    expect(screen.getByText('44 h semanais')).toBeInTheDocument()
    const janela = screen.getByRole('dialog', { name: 'Ficha do colaborador' })
    expect(within(janela).getByText('14/03/2022')).toBeInTheDocument()
    expect(screen.getByText('Colaborador')).toBeInTheDocument()
    expect(screen.queryByText('Unidade')).toBeNull()
    expect(screen.queryByText(/Matriz/)).toBeNull()
  })

  it('gestor null mostra Sem gestor e a data de admissao nao muda de dia', async () => {
    comFicha({ gestor: null, dataAdmissao: '2026-10-01' })
    await abrir()
    expect(await screen.findByText('Sem gestor')).toBeInTheDocument()
    expect(screen.getByText('01/10/2026')).toBeInTheDocument()
  })

  it('beneficios como etiquetas, com dependentes, e sem beneficios', async () => {
    const { unmount } = await abrir()
    expect(await screen.findByText('Plano de saúde + 1 dependente')).toBeInTheDocument()
    unmount()
    comFicha({ beneficios: [] })
    await abrir()
    expect(await screen.findByText('Nenhum benefício ativo.')).toBeInTheDocument()
  })

  it('os quatro numeros vem de outra rota e falham sozinhos', async () => {
    const { unmount } = await abrir()
    const quadro = await screen.findByRole('region', { name: 'Números do colaborador' })
    expect(await within(quadro).findByText('+6h20')).toBeInTheDocument()
    expect(within(quadro).getByText('18 dias')).toBeInTheDocument()
    expect(within(quadro).getByText('0')).toBeInTheDocument()
    expect(within(quadro).getByText('6 de novembro')).toBeInTheDocument()
    unmount()
    capturar('obterIndicadoresDoFuncionario', problema(500))
    await abrir()
    const outro = await screen.findByRole('region', { name: 'Números do colaborador' })
    expect(await within(outro).findByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument()
    expect(within(outro).getAllByText('—')).toHaveLength(4)
    expect(screen.getByText('123.482.371-30')).toBeInTheDocument()
  })

  it('a ficha sem dados pessoais (administrador do seed) mostra traco em tudo e nao quebra', async () => {
    comFicha(SEED)
    await abrir()
    expect(await screen.findByText('Nenhum benefício ativo.')).toBeInTheDocument()
    expect(screen.getAllByText('—').length).toBeGreaterThanOrEqual(8)
    expect(screen.getByText('Sem gestor')).toBeInTheDocument()
  })

  it('carregando mostra o esqueleto e o erro da ficha mostra a mensagem com Tentar de novo', async () => {
    const ficha = capturar('obterFichaDoFuncionario', problema(500))
    const { user } = await abrir()
    expect(
      await screen.findByText('Erro no servidor. Tente novamente em instantes.'),
    ).toBeInTheDocument()
    const antes = ficha.quantas()
    await user.click(screen.getByRole('button', { name: 'Tentar de novo' }))
    await waitFor(() => expect(ficha.quantas()).toBe(antes + 1))
  })

  it('so existe a aba Dados nesta parte e ?aba desconhecida vale Dados', async () => {
    await abrir('/rh/colaboradores/12?aba=xyz')
    const abas = await screen.findByRole('tablist', { name: 'Partes da ficha' })
    expect(
      within(abas)
        .getAllByRole('tab')
        .map((t) => t.textContent),
    ).toContain('Dados')
    expect(within(abas).getByRole('tab', { name: 'Dados' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
  })

  it('desligamento agendado mostra a faixa com o ultimo dia e Cancelar desligamento', async () => {
    comFicha({ desligamento: { ultimoDiaDeTrabalho: '2026-11-13', situacao: 'AGENDADO' } })
    await abrir()
    expect(
      await screen.findByText(
        'Desligamento agendado: o último dia de trabalho é 13/11/2026. A pessoa continua com acesso até o dia seguinte.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Cancelar desligamento' })).toBeInTheDocument()
  })

  it('Inativar so para quem nao esta inativo; Readmitir so para inativo, no lugar; Editar dados nos dois', async () => {
    comFicha({ situacao: 'INATIVO', ativo: false, situacaoHoje: null })
    await abrir()
    expect(await screen.findByRole('button', { name: 'Readmitir' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Inativar' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Editar dados' })).toBeInTheDocument()
  })

  it('menu "...": Reenviar convite so sem SENHA_DEFINIDA e nao inativo; SEM_CONVITE diz Enviar convite de acesso; sem e-mail fica desabilitado com a dica; sem itens o botao some', async () => {
    const { unmount } = await abrir()
    await screen.findByRole('button', { name: 'Editar dados' })
    expect(screen.queryByRole('button', { name: 'Mais ações' })).toBeNull()
    unmount()
    comFicha({ acesso: { situacao: 'SEM_CONVITE', ativo: true } })
    const outra = await abrir()
    await outra.user.click(await screen.findByRole('button', { name: 'Mais ações' }))
    expect(
      await screen.findByRole('menuitem', { name: 'Enviar convite de acesso' }),
    ).toBeInTheDocument()
    outra.unmount()
    comFicha({ acesso: { situacao: 'CONVITE_ENVIADO', ativo: true }, emailCorporativo: null })
    const terceira = await abrir()
    await terceira.user.click(await screen.findByRole('button', { name: 'Mais ações' }))
    const item = await screen.findByRole('menuitem', { name: 'Reenviar convite' })
    expect(item).toHaveAttribute('aria-disabled', 'true')
    expect(item).toHaveAccessibleDescription('Esta pessoa não tem e-mail corporativo cadastrado.')
  })

  it('Reenviar convite chama a rota e avisa', async () => {
    comFicha({ acesso: { situacao: 'CONVITE_EXPIRADO', ativo: true } })
    const convite = capturar('enviarConviteDeAcesso')
    const { user } = await abrir()
    await user.click(await screen.findByRole('button', { name: 'Mais ações' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Reenviar convite' }))
    expect(
      await screen.findByText('Convite enviado para bruno.lima@empresa.com.br.'),
    ).toBeInTheDocument()
    expect(convite.ultima().parametrosDoCaminho).toEqual({ funcionarioId: '12' })
    convite.conforme()
  })

  it('404 fecha o painel com aviso e id invalido tambem', async () => {
    capturar('obterFichaDoFuncionario', problema(404, { detail: 'O registro não foi encontrado.' }))
    const { roteador, unmount } = renderizarTela('/rh/colaboradores/12?situacao=INATIVO')
    expect(await screen.findByText('O registro não foi encontrado.')).toBeInTheDocument()
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/rh/colaboradores'))
    expect(roteador.state.location.search).toBe('?situacao=INATIVO')
    unmount()
    const invalido = renderizarTela('/rh/colaboradores/abc')
    await waitFor(() => expect(invalido.roteador.state.location.pathname).toBe('/rh/colaboradores'))
  })

  it('a trilha volta a lista com os filtros', async () => {
    const ok = await abrir('/rh/colaboradores/12?situacao=INATIVO')
    await screen.findByText('Bruno Lima', { selector: 'p' })
    const janela = screen.getByRole('dialog', { name: 'Ficha do colaborador' })
    await ok.user.click(within(janela).getByRole('link', { name: 'Colaboradores' }))
    await waitFor(() => expect(ok.roteador.state.location.pathname).toBe('/rh/colaboradores'))
    expect(ok.roteador.state.location.search).toBe('?situacao=INATIVO')
  })

  it('Gestor em /rh/colaboradores/12 ve o 403 da guarda', async () => {
    renderizarTela('/rh/colaboradores/12', { perfil: 'Gestor' })
    expect(
      await screen.findByText('Você não tem permissão para ver esta página.'),
    ).toBeInTheDocument()
  })
})

describe('Ficha: Editar dados', { timeout: 15000 }, () => {
  it('Editar dados troca o corpo pelo formulario preenchido e Cancelar volta sem gravar', async () => {
    const editar = capturar('editarFuncionario')
    const { user } = await abrir()
    await user.click(await screen.findByRole('button', { name: 'Editar dados' }))
    expect(await screen.findByLabelText('Nome completo')).toHaveValue('Bruno Lima')
    expect(screen.getByLabelText('CPF')).toHaveValue('123.482.371-30')
    expect(screen.getByLabelText('CEP')).toHaveValue('01310-100')
    expect(screen.queryByText('Depois de salvar')).toBeNull()
    expect(screen.queryByLabelText('Matrícula')).toBeNull()
    for (const nome of ['Vale-refeição', 'Plano de saúde', 'Vale-transporte', 'Seguro de vida']) {
      expect(screen.getByRole('checkbox', { name: new RegExp(nome) })).toHaveAttribute(
        'aria-checked',
        'true',
      )
    }
    await user.click(screen.getByRole('button', { name: 'Cancelar' }))
    expect(await screen.findByText('123.482.371-30')).toBeInTheDocument()
    expect(editar.quantas()).toBe(0)
  })

  it('avisa ao trocar o e-mail corporativo e ao trocar o setor', async () => {
    const { user } = await abrir()
    await user.click(await screen.findByRole('button', { name: 'Editar dados' }))
    const email = await screen.findByLabelText('E-mail corporativo')
    await user.clear(email)
    await user.type(email, 'bruno@empresa.com.br')
    expect(
      screen.getByText(
        'Trocar o e-mail corporativo troca o e-mail de login: a pessoa precisa usar o novo.',
      ),
    ).toBeInTheDocument()
    await waitFor(() =>
      expect(screen.getByRole('combobox', { name: 'Setor' })).not.toHaveTextContent('Carregando'),
    )
    await escolherOpcao(user, 'Setor', 'Comercial')
    expect(screen.getByText('O gestor da pessoa passa a ser o do novo setor.')).toBeInTheDocument()
  })

  it('Nivel de acesso fica desabilitado na propria pessoa logada, com a dica', async () => {
    comFicha({ id: 1, nome: 'Ana Souza' })
    const { user } = await abrir('/rh/colaboradores/1')
    await user.click(await screen.findByRole('button', { name: 'Editar dados' }))
    expect(
      await screen.findByText('Você não pode alterar o seu próprio nível de acesso.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: 'Nível de acesso' })).toHaveAttribute(
      'data-disabled',
    )
  })

  it('envia a substituicao completa, a requisicao e conforme e o sucesso volta a leitura com Dados salvos.', async () => {
    const editar = capturar('editarFuncionario')
    const { user } = await abrir()
    await user.click(await screen.findByRole('button', { name: 'Editar dados' }))
    await waitFor(() =>
      expect(screen.getByRole('combobox', { name: 'Cargo' })).not.toHaveTextContent('Carregando'),
    )
    await user.clear(screen.getByLabelText('Complemento'))
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }))
    await waitFor(() => expect(editar.quantas()).toBe(1))
    const corpo = editar.ultima().corpo!
    expect(corpo.cpf).toBe('12348237130')
    expect(corpo.endereco).toEqual({
      cep: '01310100',
      logradouro: 'Avenida Paulista',
      numero: '1578',
      complemento: null,
      bairro: 'Bela Vista',
      cidade: 'São Paulo',
      uf: 'SP',
    })
    expect(corpo.beneficioIds).toEqual([1, 2, 4, 5])
    editar.conforme()
    expect(await screen.findByText('Dados salvos.')).toBeInTheDocument()
  })

  it('409 de CPF marca o campo e oferece Abrir a ficha; 422 do proprio perfil marca Nivel de acesso', async () => {
    capturar('editarFuncionario', problema(409, exemploDoContrato('editarFuncionario', 409, 'cpf')))
    const { user, unmount } = await abrir()
    await user.click(await screen.findByRole('button', { name: 'Editar dados' }))
    await waitFor(() =>
      expect(screen.getByRole('combobox', { name: 'Cargo' })).not.toHaveTextContent('Carregando'),
    )
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }))
    await waitFor(() =>
      expect(screen.getByLabelText('CPF')).toHaveAttribute('aria-invalid', 'true'),
    )
    expect(screen.getByRole('link', { name: /^Abrir a ficha de / })).toBeInTheDocument()
    unmount()
    capturar(
      'editarFuncionario',
      problema(422, {
        codigo: 'NAO_PODE_ALTERAR_O_PROPRIO_PERFIL',
        detail: 'Você não pode alterar o seu próprio nível de acesso.',
      }),
    )
    const outra = await abrir()
    await outra.user.click(await screen.findByRole('button', { name: 'Editar dados' }))
    await waitFor(() =>
      expect(screen.getByRole('combobox', { name: 'Cargo' })).not.toHaveTextContent('Carregando'),
    )
    await outra.user.click(screen.getByRole('button', { name: 'Salvar alterações' }))
    expect(
      await screen.findByText('Você não pode alterar o seu próprio nível de acesso.'),
    ).toBeInTheDocument()
  })

  it('a ficha sem dados pessoais abre o formulario com os obrigatorios vazios e exige o preenchimento', async () => {
    comFicha(SEED)
    const editar = capturar('editarFuncionario')
    const { user } = await abrir()
    await user.click(await screen.findByRole('button', { name: 'Editar dados' }))
    expect(await screen.findByLabelText('CPF')).toHaveValue('')
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }))
    expect(await screen.findByText('CPF inválido.')).toBeInTheDocument()
    expect(screen.getByText('Escolha o cargo.')).toBeInTheDocument()
    expect(editar.quantas()).toBe(0)
  })
})

describe('Ficha: Inativar e cancelar desligamento', { timeout: 15000 }, () => {
  it('Inativar abre o dialogo com o ultimo dia (hoje), a explicacao e a trava; o corpo e so ultimoDiaDeTrabalho', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] })
    vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))
    const inativar = capturar('inativarFuncionario', {
      corpo: {
        ...FICHA,
        desligamento: { ultimoDiaDeTrabalho: '2026-10-07', situacao: 'AGENDADO' },
      },
    })
    const { user } = await abrir()
    await user.click(await screen.findByRole('button', { name: 'Inativar' }))
    const dialogo = await screen.findByRole('alertdialog', { name: 'Inativar Bruno Lima' })
    expect(within(dialogo).getByLabelText('Último dia de trabalho')).toHaveValue('2026-10-07')
    expect(within(dialogo).getByText(/A pessoa perde o acesso ao portal/)).toBeInTheDocument()
    const botao = within(dialogo).getByRole('button', { name: 'Inativar' })
    expect(botao).toBeDisabled()
    await user.type(within(dialogo).getByLabelText('Digite "desativar" para confirmar'), 'desativa')
    expect(botao).toBeDisabled()
    await user.type(within(dialogo).getByLabelText('Digite "desativar" para confirmar'), 'r ')
    expect(botao).toBeEnabled()
    await user.click(botao)
    await waitFor(() => expect(inativar.quantas()).toBe(1))
    expect(inativar.ultima().corpo).toEqual({ ultimoDiaDeTrabalho: '2026-10-07' })
    inativar.conforme()
    expect(await screen.findByText('Desligamento de Bruno Lima registrado.')).toBeInTheDocument()
  })

  it('Gestor acrescenta a frase dos pedidos; data antes da admissao nao chama a API', async () => {
    comFicha({ perfil: 'Gestor' })
    const inativar = capturar('inativarFuncionario')
    const { user } = await abrir()
    await user.click(await screen.findByRole('button', { name: 'Inativar' }))
    const dialogo = await screen.findByRole('alertdialog')
    expect(
      within(dialogo).getByText(/os pedidos que esperam por ela passam para o RH/),
    ).toBeInTheDocument()
    fireEvent.change(within(dialogo).getByLabelText('Último dia de trabalho'), {
      target: { value: '2020-01-01' },
    })
    await user.type(
      within(dialogo).getByLabelText('Digite "desativar" para confirmar'),
      'desativar',
    )
    await user.click(within(dialogo).getByRole('button', { name: 'Inativar' }))
    expect(await within(dialogo).findByText(/não pode ser anterior à admissão/)).toBeInTheDocument()
    expect(inativar.quantas()).toBe(0)
  })

  it('409 DESLIGAMENTO_JA_REGISTRADO mostra o detail dentro do dialogo', async () => {
    capturar(
      'inativarFuncionario',
      problema(409, {
        codigo: 'DESLIGAMENTO_JA_REGISTRADO',
        detail: 'Este colaborador já está inativo ou já tem um desligamento registrado.',
      }),
    )
    const { user } = await abrir()
    await user.click(await screen.findByRole('button', { name: 'Inativar' }))
    const dialogo = await screen.findByRole('alertdialog')
    await user.type(
      within(dialogo).getByLabelText('Digite "desativar" para confirmar'),
      'desativar',
    )
    await user.click(within(dialogo).getByRole('button', { name: 'Inativar' }))
    expect(
      await within(dialogo).findByText(
        'Este colaborador já está inativo ou já tem um desligamento registrado.',
      ),
    ).toBeInTheDocument()
  })

  it('Cancelar desligamento pede confirmacao simples e chama a rota; Escape fecha so o dialogo de cima', async () => {
    comFicha({ desligamento: { ultimoDiaDeTrabalho: '2026-11-13', situacao: 'AGENDADO' } })
    const cancelar = capturar('cancelarDesligamentoAgendado')
    const { user } = await abrir()
    await user.click(await screen.findByRole('button', { name: 'Cancelar desligamento' }))
    const dialogo = await screen.findByRole('alertdialog', {
      name: 'Cancelar o desligamento agendado de Bruno Lima?',
    })
    expect(within(dialogo).getByRole('button', { name: 'Manter agendado' })).toBeInTheDocument()
    await user.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('alertdialog')).toBeNull())
    expect(screen.getByRole('dialog', { name: 'Ficha do colaborador' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Cancelar desligamento' }))
    const outra = await screen.findByRole('alertdialog')
    await user.click(within(outra).getByRole('button', { name: 'Cancelar desligamento' }))
    await waitFor(() => expect(cancelar.quantas()).toBe(1))
    cancelar.conforme()
  })
})

describe('Ficha: Readmitir', { timeout: 15000 }, () => {
  const INATIVO: Partial<Ficha> = {
    situacao: 'INATIVO',
    ativo: false,
    situacaoHoje: null,
    desligamento: { ultimoDiaDeTrabalho: '2026-09-30', situacao: 'EFETIVADO' },
    acesso: { situacao: 'SENHA_DEFINIDA', ativo: false },
  }

  async function abrirReadmitir(parcial: Partial<Ficha> = {}) {
    comFicha({ ...INATIVO, ...parcial })
    const tela = await abrir()
    await tela.user.click(await screen.findByRole('button', { name: 'Readmitir' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Readmitir Bruno Lima' })
    await waitFor(() =>
      expect(within(dialogo).getByRole('combobox', { name: 'Cargo' })).not.toHaveTextContent(
        'Carregando',
      ),
    )
    return { ...tela, dialogo }
  }

  it('abre o dialogo com a explicacao e os campos ja preenchidos, sem nivel nem beneficios', async () => {
    const { dialogo } = await abrirReadmitir()
    expect(
      within(dialogo).getByText(
        /A pessoa volta com o mesmo cadastro, o mesmo CPF e a mesma matrícula/,
      ),
    ).toBeInTheDocument()
    expect(within(dialogo).getByLabelText('Data de admissão')).toHaveAttribute('min', '2026-09-30')
    expect(within(dialogo).getByRole('combobox', { name: 'Tipo de contrato' })).toHaveTextContent(
      'CLT',
    )
    expect(within(dialogo).queryByRole('combobox', { name: 'Nível de acesso' })).toBeNull()
    expect(within(dialogo).queryByText('Vale-refeição')).toBeNull()
    expect(within(dialogo).queryByRole('checkbox', { name: /Enviar convite/ })).toBeNull()
  })

  it('a caixa de convite so aparece quando a senha nao foi definida (padrao desmarcada)', async () => {
    const { dialogo } = await abrirReadmitir({
      acesso: { situacao: 'CONVITE_EXPIRADO', ativo: false },
    })
    expect(
      within(dialogo).getByRole('checkbox', { name: /Enviar convite de acesso ao portal/ }),
    ).toHaveAttribute('aria-checked', 'false')
  })

  it('a caixa do formulario de admissao liga (ficha 5) e a readmissao manda formularioAdmissaoId: 11; 422 FORMULARIO_ENCERRADO mostra o detail', async () => {
    const readmitir = capturar(
      'readmitirFuncionario',
      problema(422, {
        title: 'Operação recusada.',
        detail: 'O formulário de admissão escolhido já foi encerrado.',
        codigo: 'FORMULARIO_ENCERRADO',
      }),
    )
    const { user, dialogo } = await abrirReadmitir()
    const onboarding = await within(dialogo).findByRole('checkbox', {
      name: /Incluir no treinamento Onboarding 2026/,
    })
    await waitFor(() => expect(onboarding).not.toHaveAttribute('aria-disabled', 'true'))
    const caixa = within(dialogo).getByRole('checkbox', { name: /Enviar o formulário de admissão/ })
    await waitFor(() => expect(caixa).not.toHaveAttribute('aria-disabled', 'true'))
    await user.click(caixa)
    fireEvent.change(within(dialogo).getByLabelText('Data de admissão'), {
      target: { value: '2026-10-13' },
    })
    await user.click(within(dialogo).getByRole('button', { name: 'Readmitir' }))
    await waitFor(() => expect(readmitir.quantas()).toBe(1))
    expect(readmitir.ultima().corpo).toMatchObject({ formularioAdmissaoId: 11 })
    readmitir.conforme()
    expect(
      await within(dialogo).findByText('O formulário de admissão escolhido já foi encerrado.'),
    ).toBeInTheDocument()
  })

  it('a caixa do onboarding liga (ficha 6) e a readmissao manda incluirNoOnboarding: true; 422 semOnboarding mostra o detail', async () => {
    const readmitir = capturar(
      'readmitirFuncionario',
      problema(
        422,
        exemploDoContrato<Record<string, unknown>>('readmitirFuncionario', 422, 'semOnboarding'),
      ),
    )
    const { user, dialogo } = await abrirReadmitir()
    const caixa = await within(dialogo).findByRole('checkbox', {
      name: /Incluir no treinamento Onboarding 2026/,
    })
    await waitFor(() => expect(caixa).not.toHaveAttribute('aria-disabled', 'true'))
    await user.click(caixa)
    fireEvent.change(within(dialogo).getByLabelText('Data de admissão'), {
      target: { value: '2026-10-13' },
    })
    await user.click(within(dialogo).getByRole('button', { name: 'Readmitir' }))
    await waitFor(() => expect(readmitir.quantas()).toBe(1))
    expect(readmitir.ultima().corpo).toMatchObject({ incluirNoOnboarding: true })
    readmitir.conforme()
    const detail = exemploDoContrato<{ detail: string }>(
      'readmitirFuncionario',
      422,
      'semOnboarding',
    ).detail
    expect(await within(dialogo).findByText(detail)).toBeInTheDocument()
  })

  it('envia o corpo, a requisicao e conforme e o sucesso mostra o aviso do acesso desativado', async () => {
    const readmitir = capturar('readmitirFuncionario', {
      corpo: { ...FICHA, nome: 'Bruno Lima', acesso: { situacao: 'SENHA_DEFINIDA', ativo: false } },
    })
    const { user, dialogo } = await abrirReadmitir()
    fireEvent.change(within(dialogo).getByLabelText('Data de admissão'), {
      target: { value: '2026-10-13' },
    })
    await user.click(within(dialogo).getByRole('button', { name: 'Readmitir' }))
    await waitFor(() => expect(readmitir.quantas()).toBe(1))
    expect(readmitir.ultima().corpo).toEqual({
      dataAdmissao: '2026-10-13',
      cargoId: 8,
      setorId: 2,
      tipoContrato: 'CLT',
      incluirNoOnboarding: false,
      formularioAdmissaoId: null,
      enviarConvite: false,
    })
    readmitir.conforme()
    expect(await screen.findByText('Bruno Lima foi readmitido.')).toBeInTheDocument()
    expect(
      await screen.findByText(
        'O acesso ao portal continua desativado. Libere em Cadastros, Usuários do sistema (Ativar).',
      ),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Usuários do sistema' })).toHaveAttribute(
      'href',
      '/rh/cadastros/usuarios',
    )
  })

  it('data antes do vinculo anterior nao chama a API; 422 GESTOR_DO_SETOR_INATIVO marca Setor', async () => {
    const readmitir = capturar(
      'readmitirFuncionario',
      problema(422, exemploDoContrato('readmitirFuncionario', 422, 'gestorInativo')),
    )
    const { user, dialogo } = await abrirReadmitir()
    fireEvent.change(within(dialogo).getByLabelText('Data de admissão'), {
      target: { value: '2026-09-01' },
    })
    await user.click(within(dialogo).getByRole('button', { name: 'Readmitir' }))
    expect(
      await within(dialogo).findByText(/não pode ser anterior a 30\/09\/2026/),
    ).toBeInTheDocument()
    expect(readmitir.quantas()).toBe(0)
    fireEvent.change(within(dialogo).getByLabelText('Data de admissão'), {
      target: { value: '2026-10-13' },
    })
    await user.click(within(dialogo).getByRole('button', { name: 'Readmitir' }))
    expect(
      await within(dialogo).findByText(/O gestor do setor escolhido está inativo/),
    ).toBeInTheDocument()
  })
})
