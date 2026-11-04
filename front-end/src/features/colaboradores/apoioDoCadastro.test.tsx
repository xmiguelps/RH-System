import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import type { ReactNode } from 'react'
import { describe, expect, it } from 'vitest'
import '../../shared/zod'
import { lerCadastroExistente } from '../../shared/api/cadastroExistente'
import { criarClienteApi } from '../../shared/api/clienteApi'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { erroNoCampo } from '../../shared/form/erroNoCampo'
import { servidor } from '../../shared/mocks/servidor'
import { exemploDoContrato } from '../../shared/teste/exemploDoContrato'
import { capturar } from '../../test/capturar'
import {
  esquemaDoColaborador,
  montarCorpoDeCadastro,
  valoresIniciaisDoCadastro,
  type EntradaDoColaborador,
} from './esquemaDoColaborador'
import { useOpcoesDeApoio } from './useOpcoesDeApoio'

const CPF_INATIVO = exemploDoContrato<Record<string, unknown>>(
  'criarFuncionario',
  409,
  'cpfInativo',
)

describe('ErroDeApi.corpo', () => {
  it('ErroDeApi guarda o corpo inteiro do problem+json (cadastroExistente)', async () => {
    servidor.use(
      http.post('*/api/auth/entrar', () =>
        HttpResponse.json(CPF_INATIVO, {
          status: 409,
          headers: { 'content-type': 'application/problem+json' },
        }),
      ),
    )
    const erro = await criarClienteApi('https://api.exemplo.test')
      .entrar('a@b.c', 'x')
      .catch((e: unknown) => e)
    expect((erro as ErroDeApi).corpo?.cadastroExistente).toMatchObject({ nome: 'Henrique Alves' })
  })
})

describe('lerCadastroExistente', () => {
  it('le cadastroExistente do 409 e devolve null para qualquer outra coisa', () => {
    const com = new ErroDeApi(409, 'Conflito.', { corpo: CPF_INATIVO })
    expect(lerCadastroExistente(com)).toEqual({
      funcionarioId: 201,
      nome: 'Henrique Alves',
      ativo: false,
    })
    expect(lerCadastroExistente(new ErroDeApi(409, 'Conflito.'))).toBeNull()
    expect(lerCadastroExistente(new ErroDeApi(400, 'x', { corpo: CPF_INATIVO }))).toBeNull()
    expect(lerCadastroExistente(new Error('x'))).toBeNull()
    expect(
      lerCadastroExistente(
        new ErroDeApi(409, 'x', {
          corpo: { cadastroExistente: { funcionarioId: 1, nome: 'A', ativo: 'n' } },
        }),
      ),
    ).toBeNull()
  })
})

describe('erroNoCampo', () => {
  it('poe o detail no campo do codigo e mantem o resto do erro', () => {
    const original = new ErroDeApi(409, 'Conflito.', {
      codigo: 'CPF_JA_CADASTRADO',
      detail: 'Este CPF já está cadastrado.',
      corpo: CPF_INATIVO,
    })
    const novo = erroNoCampo(original, { CPF_JA_CADASTRADO: 'cpf' }) as ErroDeApi
    expect(novo.errors).toEqual({ cpf: ['Este CPF já está cadastrado.'] })
    expect(novo.status).toBe(409)
    expect(novo.corpo).toBe(CPF_INATIVO)
  })

  it('nao mexe em codigo fora do mapa nem em erro que ja traz errors', () => {
    const fora = new ErroDeApi(422, 'x', { codigo: 'OUTRO' })
    expect(erroNoCampo(fora, { CPF_JA_CADASTRADO: 'cpf' })).toBe(fora)
    const comErrors = new ErroDeApi(409, 'x', {
      codigo: 'CPF_JA_CADASTRADO',
      errors: { nome: ['a'] },
    })
    expect(erroNoCampo(comErrors, { CPF_JA_CADASTRADO: 'cpf' })).toBe(comErrors)
  })
})

describe('useOpcoesDeApoio', () => {
  it('pede as tres listas com tamanhoPagina=100 e monta as opcoes e o gestor do setor', async () => {
    const cargos = capturar('listarCargos')
    const setores = capturar('listarSetores')
    const beneficios = capturar('listarBeneficiosDoCatalogo')
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={qc}>{children}</QueryClientProvider>
    )
    const { result } = renderHook(() => useOpcoesDeApoio(), { wrapper })
    await waitFor(() => expect(result.current.carregando).toBe(false))
    for (const c of [cargos, setores, beneficios]) {
      expect(c.ultima().consulta.get('tamanhoPagina')).toBe('100')
      c.conforme()
    }
    expect(result.current.cargos).toContainEqual({ valor: '2', rotulo: 'Vendedor' })
    expect(result.current.gestorDoSetor('1')).toBe('Carlos Ribeiro')
    expect(result.current.gestorDoSetor('7')).toBeNull()
    expect(result.current.gestorDoSetor(null)).toBeUndefined()
    expect(result.current.beneficios.map((b) => b.id)).toEqual([1, 2, 3, 4, 5, 6, 7])
  })
})

const VALIDO: EntradaDoColaborador = {
  ...valoresIniciaisDoCadastro,
  nome: 'Rafael Torres',
  cpf: '846.392.710-69',
  dataNascimento: '1995-02-09',
  telefone: '(11) 99123-4567',
  emailCorporativo: 'rafael.torres@empresa.com.br',
  dataAdmissao: '2026-10-13',
  cargoId: '2',
  setorId: '1',
  beneficioIds: [1, 2],
}

function mensagem(campo: string, valores: Partial<EntradaDoColaborador>) {
  const r = esquemaDoColaborador.safeParse({ ...VALIDO, ...valores })
  return r.error?.issues.find((i) => i.path.join('.') === campo)?.message
}

describe('esquemaDoColaborador', () => {
  it('valida cada regra da nota com a mensagem certa', () => {
    expect(esquemaDoColaborador.safeParse(VALIDO).success).toBe(true)
    expect(mensagem('cpf', { cpf: '111.111.111-11' })).toBe('CPF inválido.')
    expect(mensagem('nome', { nome: '' })).toBe('Informe o nome.')
    expect(mensagem('dataNascimento', { dataNascimento: '2999-01-01' })).toBe(
      'A data de nascimento não pode ser no futuro.',
    )
    expect(mensagem('telefone', { telefone: '(11) 9876' })).toBe(
      'Telefone inválido. Use DDD e número.',
    )
    expect(mensagem('cargoId', { cargoId: '' })).toBe('Escolha o cargo.')
    expect(mensagem('setorId', { setorId: '' })).toBe('Escolha o setor.')
    expect(mensagem('emailCorporativo', { emailCorporativo: 'x' })).toBe(
      'Informe um e-mail válido.',
    )
  })

  it('montarCorpoDeCadastro envia so as chaves do contrato e converte os ids', () => {
    const corpo = montarCorpoDeCadastro(esquemaDoColaborador.parse(VALIDO))
    expect(corpo).toEqual({
      nome: 'Rafael Torres',
      cpf: '84639271069',
      dataNascimento: '1995-02-09',
      telefone: '11991234567',
      emailCorporativo: 'rafael.torres@empresa.com.br',
      dataAdmissao: '2026-10-13',
      tipoContrato: 'CLT',
      cargoId: 2,
      setorId: 1,
      perfil: 'Funcionario',
      beneficioIds: [1, 2],
      enviarConvite: true,
      incluirNoOnboarding: false,
      formularioAdmissaoId: null,
    })
  })
})
