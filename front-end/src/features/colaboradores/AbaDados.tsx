import type { ReactNode } from 'react'
import { $api } from '../../shared/api/api'
import { formatarData, formatarDiaEMes } from '../../shared/formato/datas'
import { formatarCpf, formatarTelefone } from '../../shared/formato/documentos'
import { plural } from '../../shared/formato/texto'
import { Cartao } from '../../shared/ui/Cartao'
import { Selo } from '../../shared/ui/Selo'
import type { PropsDaAba } from './abasDaFicha'
import { formatarEndereco } from './endereco'
import { FormularioDeEdicao } from './FormularioDeEdicao'
import {
  itemDeAdvertencias,
  itemDeBancoDeHoras,
  itemDeFerias,
  itemDeProximoPagamento,
} from './indicadores'
import { QuadroDeIndicadores } from './QuadroDeIndicadores'
import { rotuloDoNivelDeAcesso, rotuloDoTipoDeContrato } from './rotulos'

function Dado({ rotulo, valor }: { rotulo: string; valor: ReactNode }) {
  return (
    <div className="flex flex-col gap-1">
      <dt className="text-tabela text-texto-suave">{rotulo}</dt>
      <dd className="text-corpo text-texto">{valor}</dd>
    </div>
  )
}

function rotuloDoBeneficio(nome: string, dependentes: number): string {
  if (dependentes <= 0) return nome
  return `${nome} + ${dependentes} ${plural(dependentes, 'dependente', 'dependentes')}`
}

export function AbaDados({ ficha, editando, aoEncerrarEdicao }: PropsDaAba) {
  const indicadores = $api.useQuery('get', '/api/rh/funcionarios/{funcionarioId}/indicadores', {
    params: { path: { funcionarioId: ficha.id } },
  })

  if (editando) return <FormularioDeEdicao ficha={ficha} aoEncerrar={aoEncerrarEdicao} />

  const numeros = indicadores.data
  const rg = [ficha.rg, ficha.rgOrgaoEmissor].filter(Boolean).join(', ')

  return (
    <div className="flex flex-col gap-5">
      <Cartao>
        <h3 className="mb-4 text-t3 font-bold text-texto">Dados pessoais</h3>
        <dl className="grid grid-cols-3 gap-x-6 gap-y-4">
          <Dado rotulo="Nome completo" valor={ficha.nome} />
          <Dado rotulo="E-mail corporativo" valor={ficha.emailCorporativo ?? '—'} />
          <Dado rotulo="Telefone" valor={formatarTelefone(ficha.telefone)} />
          <Dado rotulo="CPF" valor={formatarCpf(ficha.cpf)} />
          <Dado rotulo="Aniversário" valor={formatarDiaEMes(ficha.dataNascimento)} />
          <Dado rotulo="E-mail pessoal" valor={ficha.emailPessoal ?? '—'} />
          <Dado rotulo="RG" valor={rg || '—'} />
          <div className="col-span-2">
            <Dado rotulo="Endereço" valor={formatarEndereco(ficha.endereco)} />
          </div>
        </dl>
      </Cartao>
      <Cartao>
        <h3 className="mb-4 text-t3 font-bold text-texto">Contrato</h3>
        <dl className="grid grid-cols-3 gap-x-6 gap-y-4">
          <Dado rotulo="Cargo" valor={ficha.cargo?.nome ?? '—'} />
          <Dado rotulo="Setor" valor={ficha.setor?.nome ?? '—'} />
          <Dado rotulo="Gestor(a)" valor={ficha.gestor?.nome ?? 'Sem gestor'} />
          <Dado rotulo="Tipo de contrato" valor={rotuloDoTipoDeContrato(ficha.tipoContrato)} />
          <Dado
            rotulo="Jornada"
            valor={
              ficha.jornadaSemanalHoras === null ? '—' : `${ficha.jornadaSemanalHoras} h semanais`
            }
          />
          <Dado rotulo="Admissão" valor={formatarData(ficha.dataAdmissao)} />
          <Dado rotulo="Nível de acesso" valor={rotuloDoNivelDeAcesso(ficha.perfil)} />
        </dl>
      </Cartao>
      <Cartao>
        <h3 className="mb-4 text-t3 font-bold text-texto">Benefícios</h3>
        {ficha.beneficios.length === 0 ? (
          <p className="text-corpo text-texto-suave">Nenhum benefício ativo.</p>
        ) : (
          <ul className="flex flex-wrap gap-2">
            {ficha.beneficios.map((b) => (
              <li key={b.beneficioId}>
                <Selo tom="sucesso">{rotuloDoBeneficio(b.nome, b.quantidadeDependentes)}</Selo>
              </li>
            ))}
          </ul>
        )}
      </Cartao>
      <QuadroDeIndicadores
        rotulo="Números do colaborador"
        carregando={indicadores.isLoading}
        erro={indicadores.error ?? undefined}
        aoTentarDeNovo={() => void indicadores.refetch()}
        itens={[
          itemDeBancoDeHoras(numeros?.bancoDeHorasMinutos ?? null),
          itemDeFerias(numeros?.feriasDisponiveisDias ?? null),
          itemDeAdvertencias(numeros?.advertenciasQuantidade ?? null),
          itemDeProximoPagamento(numeros?.proximoPagamento ?? null),
        ]}
      />
    </div>
  )
}
