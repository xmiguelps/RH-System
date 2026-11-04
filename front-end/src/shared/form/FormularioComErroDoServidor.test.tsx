import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Controller } from 'react-hook-form'
import { describe, expect, it, vi } from 'vitest'
import { z } from 'zod'
import '../zod'
import { ErroDeApi } from '../api/erroDeApi'
import { mascararCpf } from '../formato/documentos'
import { Botao } from '../ui/Botao'
import { CaixaDeSelecao } from '../ui/CaixaDeSelecao'
import { Campo } from '../ui/Campo'
import { Entrada } from '../ui/Entrada'
import { Selecao } from '../ui/Selecao'
import { campoCpf, campoTexto } from './esquemas'
import { FormularioComErroDoServidor } from './FormularioComErroDoServidor'

const esquema = z.object({
  nome: campoTexto('Informe o nome.'),
  cpf: campoCpf(),
  cargo: campoTexto('Escolha o cargo.'),
  convite: z.boolean(),
})

const VALORES = { nome: '', cpf: '', cargo: '', convite: false }

function montar(aoEnviar: (valores: z.output<typeof esquema>) => unknown, aoSucesso?: () => void) {
  return render(
    <FormularioComErroDoServidor
      esquema={esquema}
      valoresIniciais={VALORES}
      aoEnviar={aoEnviar}
      aoSucesso={aoSucesso}
    >
      {({ register, control, formState }) => (
        <>
          <Campo rotulo="Nome" erro={formState.errors.nome?.message}>
            <Entrada {...register('nome')} />
          </Campo>
          <Campo rotulo="CPF" erro={formState.errors.cpf?.message}>
            <Entrada {...register('cpf')} mascara={mascararCpf} />
          </Campo>
          <Controller
            control={control}
            name="cargo"
            render={({ field, fieldState }) => (
              <Campo rotulo="Cargo" erro={fieldState.error?.message}>
                <Selecao
                  rotulo="Cargo"
                  opcoes={[
                    { valor: 'analista', rotulo: 'Analista' },
                    { valor: 'gerente', rotulo: 'Gerente' },
                  ]}
                  valor={field.value || null}
                  aoMudar={(v) => field.onChange(v ?? '')}
                  invalido={!!fieldState.error}
                />
              </Campo>
            )}
          />
          <Controller
            control={control}
            name="convite"
            render={({ field }) => (
              <CaixaDeSelecao
                marcada={field.value}
                aoMudar={field.onChange}
                rotulo="Enviar convite de acesso"
              />
            )}
          />
          <Botao type="submit" disabled={formState.isSubmitting}>
            Salvar
          </Botao>
        </>
      )}
    </FormularioComErroDoServidor>,
  )
}

async function preencher() {
  const usuario = userEvent.setup()
  await usuario.type(screen.getByLabelText('Nome'), 'Bruno Lima')
  await usuario.type(screen.getByLabelText('CPF'), '84639271069')
  await usuario.click(screen.getByRole('combobox', { name: 'Cargo' }))
  await usuario.click(await screen.findByRole('option', { name: 'Analista' }))
  await usuario.click(screen.getByText('Enviar convite de acesso'))
  return usuario
}

describe('FormularioComErroDoServidor', () => {
  it('valida no cliente, mostra a mensagem de cada campo e nao chama aoEnviar', async () => {
    const usuario = userEvent.setup()
    const aoEnviar = vi.fn()
    montar(aoEnviar)
    await usuario.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(await screen.findByText('Informe o nome.')).toBeInTheDocument()
    expect(screen.getByText('Escolha o cargo.')).toBeInTheDocument()
    expect(screen.getByLabelText('Nome')).toHaveAttribute('aria-invalid', 'true')
    expect(aoEnviar).not.toHaveBeenCalled()
  })

  it('envia o z.output: cpf so com digitos, cargo, convite', async () => {
    const aoEnviar = vi.fn()
    montar(aoEnviar)
    const usuario = await preencher()
    await usuario.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(aoEnviar).toHaveBeenCalledTimes(1))
    expect(aoEnviar.mock.calls[0]?.[0]).toEqual({
      nome: 'Bruno Lima',
      cpf: '84639271069',
      cargo: 'analista',
      convite: true,
    })
  })

  it('mascara de cpf do Entrada funciona junto do register', async () => {
    const usuario = userEvent.setup()
    montar(() => {})
    await usuario.type(screen.getByLabelText('CPF'), '84639271069')
    expect(screen.getByLabelText('CPF')).toHaveValue('846.392.710-69')
  })

  it('400 do servidor marca o campo com aria-invalid e a mensagem do contrato', async () => {
    montar(() => {
      throw new ErroDeApi(400, 'Confira os campos indicados.', {
        errors: { cpf: ['CPF já cadastrado.'] },
      })
    })
    const usuario = await preencher()
    await usuario.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(await screen.findByText('CPF já cadastrado.')).toBeInTheDocument()
    expect(screen.getByLabelText('CPF')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.queryByRole('alert', { name: /geral/ })).not.toBeInTheDocument()
  })

  it('400 com campo desconhecido, 422 e falha de rede aparecem no aviso geral', async () => {
    const erros = [
      new ErroDeApi(400, 'x', { errors: { nivelDeAcesso: ['Valor inválido.'] } }),
      new ErroDeApi(422, 'Regra.', { detail: 'Período de férias bloqueado.' }),
      ErroDeApi.semConexao('x'),
    ]
    const textos = [
      'Valor inválido.',
      'Período de férias bloqueado.',
      'Não foi possível conectar. Tente novamente.',
    ]
    let vez = 0
    montar(() => {
      throw erros[vez++]
    })
    const usuario = await preencher()
    for (const texto of textos) {
      await usuario.click(screen.getByRole('button', { name: 'Salvar' }))
      await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(texto))
    }
  })

  it('clique duplo em Salvar chama aoEnviar uma vez (Review Focus)', async () => {
    let liberar: () => void = () => {}
    const aoEnviar = vi.fn(
      () =>
        new Promise<void>((resolver) => {
          liberar = resolver
        }),
    )
    const { container } = montar(aoEnviar)
    await preencher()
    const formulario = container.querySelector('form')!
    fireEvent.submit(formulario)
    fireEvent.submit(formulario)
    await waitFor(() => expect(aoEnviar).toHaveBeenCalledTimes(1))
    await new Promise((resolver) => setTimeout(resolver, 30))
    expect(aoEnviar).toHaveBeenCalledTimes(1)
    liberar()
  })

  it('aoSucesso e chamado so quando aoEnviar nao lanca', async () => {
    const aoSucesso = vi.fn()
    let falhar = true
    montar(() => {
      if (falhar) throw new ErroDeApi(500, 'x')
    }, aoSucesso)
    const usuario = await preencher()
    await usuario.click(screen.getByRole('button', { name: 'Salvar' }))
    await screen.findByRole('alert')
    expect(aoSucesso).not.toHaveBeenCalled()
    falhar = false
    await usuario.click(screen.getByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(aoSucesso).toHaveBeenCalledTimes(1))
  })
})
