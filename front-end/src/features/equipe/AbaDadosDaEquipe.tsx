import type { ReactNode } from 'react'
import { $api } from '../../shared/api/api'
import { formatarData } from '../../shared/formato/datas'
import { Cartao } from '../../shared/ui/Cartao'
import {
  itemDeAdvertencias,
  itemDeBancoDeHoras,
  itemDeFerias,
  itemDeTreinamentosPendentes,
} from '../colaboradores/indicadores'
import { QuadroDeIndicadores } from '../colaboradores/QuadroDeIndicadores'
import { rotuloDoTipoDeContrato } from '../colaboradores/rotulos'
import type { PropsDaAbaDaEquipe } from './abasDaFichaDaEquipe'

function Dado({ rotulo, valor }: { rotulo: string; valor: ReactNode }) {
  return (
    <div className="flex flex-col gap-1">
      <dt className="text-tabela text-texto-suave">{rotulo}</dt>
      <dd className="text-corpo text-texto">{valor}</dd>
    </div>
  )
}

/** A ficha do gestor tem schema próprio: não esconde campos da do RH, só não os tem. */
export function AbaDadosDaEquipe({ ficha }: PropsDaAbaDaEquipe) {
  const indicadores = $api.useQuery('get', '/api/gestor/equipe/{funcionarioId}/indicadores', {
    params: { path: { funcionarioId: ficha.id } },
  })
  const numeros = indicadores.data
  return (
    <div className="flex flex-col gap-5">
      <QuadroDeIndicadores
        rotulo="Números da pessoa"
        carregando={indicadores.isLoading}
        erro={indicadores.error ?? undefined}
        aoTentarDeNovo={() => void indicadores.refetch()}
        itens={[
          itemDeBancoDeHoras(numeros?.bancoDeHorasMinutos ?? null),
          itemDeFerias(numeros?.feriasDisponiveisDias ?? null),
          itemDeTreinamentosPendentes(numeros?.treinamentosPendentes ?? null),
          itemDeAdvertencias(numeros?.advertenciasQuantidade ?? null),
        ]}
      />
      <Cartao>
        <h3 className="mb-4 text-t3 font-bold text-texto">Contato e contrato</h3>
        <dl className="grid grid-cols-3 gap-x-6 gap-y-4">
          <Dado rotulo="E-mail corporativo" valor={ficha.emailCorporativo ?? '—'} />
          <Dado rotulo="Telefone" valor={ficha.telefoneMascarado ?? '—'} />
          <Dado rotulo="Cargo" valor={ficha.cargo ?? '—'} />
          <Dado rotulo="Admissão" valor={formatarData(ficha.dataAdmissao)} />
          <Dado rotulo="Contrato" valor={rotuloDoTipoDeContrato(ficha.tipoContrato)} />
          <Dado
            rotulo="Jornada"
            valor={
              ficha.jornadaSemanalHoras === null ? '—' : `${ficha.jornadaSemanalHoras} h semanais`
            }
          />
        </dl>
      </Cartao>
      <p className="text-pequeno text-texto-suave">
        Só o RH altera dados cadastrais. CPF, endereço, holerites e documentos ficam visíveis apenas
        para o RH.
      </p>
    </div>
  )
}
