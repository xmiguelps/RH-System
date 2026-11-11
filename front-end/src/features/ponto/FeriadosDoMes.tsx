import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Controller } from 'react-hook-form'
import { Link } from 'react-router'
import { z } from 'zod'
import { $api } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { invalidarRota } from '../../shared/api/invalidar'
import type { Esquema } from '../../shared/api/tipos'
import { erroNoCampo } from '../../shared/form/erroNoCampo'
import { campoData, campoTexto } from '../../shared/form/esquemas'
import { FormularioComErroDoServidor } from '../../shared/form/FormularioComErroDoServidor'
import { formatarDiaMesDaData } from '../../shared/formato/datas'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { Campo } from '../../shared/ui/Campo'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'
import { Cartao } from '../../shared/ui/Cartao'
import { Entrada } from '../../shared/ui/Entrada'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { useNotificacao } from '../../shared/ui/useNotificacao'

type Feriado = Esquema<'Feriado'>

const esquema = z.object({
  nome: campoTexto('Informe o nome do feriado.').max(100, 'Até 100 caracteres.'),
  data: campoData(),
  repeteTodoAno: z.boolean(),
  ativo: z.boolean(),
})

function rotuloDoTipo(f: Feriado): string {
  if (f.tipo === 'NACIONAL') return 'Nacional, importado automaticamente'
  if (f.tipo === 'ESTADUAL') return 'Estadual, importado automaticamente'
  return 'Municipal'
}

function CartaoDoFeriado({
  editando,
  chave,
  aoTerminar,
}: {
  editando: Feriado | null
  /** Muda a cada término: o formulário volta vazio depois de salvar. */
  chave: string
  aoTerminar: () => void
}) {
  const queryClient = useQueryClient()
  const notificacao = useNotificacao()
  const criar = $api.useMutation('post', '/api/rh/ponto/feriados')
  const editar = $api.useMutation('put', '/api/rh/ponto/feriados/{feriadoId}')
  const [semEmpresa, setSemEmpresa] = useState(false)
  return (
    <Cartao>
      <h3 className="mb-2 text-t3 font-bold text-texto">
        {editando ? 'Editar feriado' : 'Adicionar feriado municipal'}
      </h3>
      <p className="mb-4 text-corpo text-texto-suave">
        Os feriados nacionais e estaduais entram sozinhos. Aqui você cadastra os da sua cidade.
      </p>
      {semEmpresa && (
        <p className="mb-3 text-pequeno">
          <Link to="/rh/cadastros/empresa" className="font-semibold text-primaria">
            Ir para Cadastros, Empresa
          </Link>
        </p>
      )}
      <FormularioComErroDoServidor
        key={chave}
        id="form-feriado"
        esquema={esquema}
        valoresIniciais={{
          nome: editando?.nome ?? '',
          data: editando?.data ?? '',
          repeteTodoAno: editando?.repeteTodoAno ?? false,
          ativo: true,
        }}
        aoEnviar={async (v) => {
          setSemEmpresa(false)
          try {
            if (editando) {
              await editar.mutateAsync({
                params: { path: { feriadoId: editando.id } },
                body: {
                  nome: v.nome,
                  data: v.data,
                  repeteTodoAno: v.repeteTodoAno,
                  ativo: v.ativo,
                },
              })
            } else {
              await criar.mutateAsync({
                body: { nome: v.nome, data: v.data, repeteTodoAno: v.repeteTodoAno },
              })
            }
          } catch (erro) {
            if (erro instanceof ErroDeApi && erro.codigo === 'EMPRESA_NAO_CONFIGURADA') {
              setSemEmpresa(true)
            }
            throw erroNoCampo(erro, { FERIADO_DUPLICADO: 'nome' })
          }
          notificacao.sucesso(editando ? 'Feriado atualizado.' : 'Feriado salvo.')
          await invalidarRota(queryClient, '/api/rh/ponto/feriados')
          aoTerminar()
        }}
      >
        {({ register, control, formState }) => (
          <div className="flex flex-col gap-4">
            <Campo rotulo="Nome do feriado" erro={formState.errors.nome?.message}>
              <Entrada {...register('nome')} placeholder="Ex.: aniversário da cidade" />
            </Campo>
            <Campo rotulo="Data" erro={formState.errors.data?.message}>
              <Entrada type="date" lang="pt-BR" {...register('data')} />
            </Campo>
            <Controller
              control={control}
              name="repeteTodoAno"
              render={({ field }) => (
                <CaixaDeSelecao
                  rotulo="Repetir todos os anos"
                  marcada={field.value}
                  aoMudar={field.onChange}
                />
              )}
            />
            {editando && (
              <Controller
                control={control}
                name="ativo"
                render={({ field }) => (
                  <CaixaDeSelecao
                    rotulo="Desativar este feriado"
                    descricao="Some da lista e deixa de valer."
                    marcada={!field.value}
                    aoMudar={(marcada) => field.onChange(!marcada)}
                  />
                )}
              />
            )}
            <div className="flex gap-3">
              {editando && (
                <Botao variante="secundario" onClick={aoTerminar}>
                  Cancelar
                </Botao>
              )}
              <Botao type="submit" carregando={criar.isPending || editar.isPending}>
                Salvar feriado
              </Botao>
            </div>
          </div>
        )}
      </FormularioComErroDoServidor>
    </Cartao>
  )
}

/** "Feriados deste mês" e o cartão de adicionar ou editar (tela 12). */
export function FeriadosDoMes({ ano, mes }: { ano: number; mes: number }) {
  const [editando, setEditando] = useState<Feriado | null>(null)
  const [versao, setVersao] = useState(0)
  const consulta = $api.useQuery('get', '/api/rh/ponto/feriados', {
    params: { query: { ano, mes } },
  })
  const itens = consulta.data?.itens
  return (
    <div className="grid grid-cols-2 items-start gap-6">
      <section aria-labelledby="titulo-feriados" className="flex flex-col gap-3">
        <h2 id="titulo-feriados" className="text-t3 font-bold text-texto">
          Feriados deste mês
        </h2>
        {consulta.error ? (
          <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
        ) : !itens ? (
          <Esqueleto className="h-24 w-full" />
        ) : itens.length === 0 ? (
          <p className="text-corpo text-texto-suave">Nenhum feriado neste mês.</p>
        ) : (
          <ul aria-label="Feriados deste mês" className="flex flex-col divide-y divide-divisor">
            {itens.map((f) => (
              <li key={f.id} className="flex items-center justify-between gap-3 py-3">
                <span className="flex items-center gap-4">
                  <span className="text-t3 font-bold text-primaria">
                    {formatarDiaMesDaData(f.data)}
                  </span>
                  <span className="flex flex-col">
                    <span className="font-semibold text-texto">{f.nome}</span>
                    <span className="text-pequeno text-texto-suave">{rotuloDoTipo(f)}</span>
                  </span>
                </span>
                {f.podeEditar && (
                  <Botao
                    variante="secundario"
                    tamanho="pequeno"
                    onClick={() => setEditando(f)}
                    aria-label={`Editar o feriado ${f.nome}`}
                  >
                    Editar
                  </Botao>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>
      <CartaoDoFeriado
        editando={editando}
        chave={`${editando?.id ?? 'novo'}-${versao}`}
        aoTerminar={() => {
          setEditando(null)
          setVersao((v) => v + 1)
        }}
      />
    </div>
  )
}
