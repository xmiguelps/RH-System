import { useState } from 'react'
import { Outlet } from 'react-router'
import { $api } from '../../shared/api/api'
import { formatarMesAno, nomeDoMes } from '../../shared/formato/datas'
import { formatarMinutosComoHoras, formatarNumero } from '../../shared/formato/numeros'
import { Botao } from '../../shared/ui/Botao'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { BlocoDoBancoDeHoras, MeusAtestados, MeusPedidosDeAjuste } from './BlocosDoMeuPonto'
import { CartaoDoPontoHoje } from './CartaoDoPontoHoje'
import { ModalEnviarAtestado } from './ModalEnviarAtestado'
import { ModalPedirAjuste } from './ModalPedirAjuste'
import { NumerosDoPonto } from './PecasDoPonto'
import { saldoComSinal, tomDoSaldo } from './rotulosDoPonto'
import { TabelaDoMeuEspelho } from './TabelaDoEspelho'

type Modal = { tipo: 'ajuste'; dia?: string } | { tipo: 'atestado' } | null

function subtitulo(
  ano: number,
  mes: number,
  jornada: {
    cargaSemanalMinutos: number | null
    horarioEntrada: string | null
    horarioSaida: string | null
  },
): string {
  const partes = [`${formatarMesAno(ano, mes)}.`]
  const horas = jornada.cargaSemanalMinutos === null ? null : jornada.cargaSemanalMinutos / 60
  const horario =
    jornada.horarioEntrada && jornada.horarioSaida
      ? `das ${jornada.horarioEntrada} às ${jornada.horarioSaida}`
      : null
  if (horas !== null && horario) partes.push(`Jornada de ${horas} horas semanais, ${horario}.`)
  else if (horas !== null) partes.push(`Jornada de ${horas} horas semanais.`)
  else if (horario) partes.push(`Jornada ${horario}.`)
  return partes.join(' ')
}

/** Meu ponto (telas 33 e 47; a mesma tela para os três perfis, só o menu muda). */
export function PaginaMeuPonto() {
  const [modal, setModal] = useState<Modal>(null)
  const espelho = $api.useQuery('get', '/api/eu/ponto/espelho')
  const dados = espelho.data
  const resumo = dados?.resumo

  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo="Meu ponto"
        subtitulo={dados ? subtitulo(dados.ano, dados.mes, dados.jornada) : undefined}
        acoes={
          <>
            <Botao variante="secundario" onClick={() => setModal({ tipo: 'atestado' })}>
              Enviar atestado
            </Botao>
            <Botao variante="secundario" onClick={() => setModal({ tipo: 'ajuste' })}>
              Pedir ajuste
            </Botao>
          </>
        }
      />
      <CartaoDoPontoHoje variante="reduzido" />
      <NumerosDoPonto
        rotulo="Números do mês"
        carregando={!dados}
        itens={[
          {
            rotulo: 'Banco de horas',
            valor: saldoComSinal(resumo?.saldoBancoHorasMinutos ?? null),
            tom: tomDoSaldo(resumo?.saldoBancoHorasMinutos ?? null),
          },
          {
            rotulo: 'Horas trabalhadas no mês',
            valor: resumo ? formatarMinutosComoHoras(resumo.horasTrabalhadasNoMesMinutos) : '—',
          },
          {
            rotulo: 'Carga horária por dia',
            valor:
              dados?.jornada.cargaDiariaMinutos != null
                ? formatarMinutosComoHoras(dados.jornada.cargaDiariaMinutos)
                : '—',
          },
          {
            rotulo: 'Pedidos em análise',
            valor: resumo ? formatarNumero(resumo.pedidosEmAnalise) : '—',
            tom: resumo && resumo.pedidosEmAnalise > 0 ? 'alerta' : 'neutro',
          },
        ]}
      />
      <section aria-labelledby="titulo-registros" className="flex flex-col gap-3">
        <h2 id="titulo-registros" className="text-t3 font-bold text-texto">
          {dados ? `Registros de ${nomeDoMes(dados.mes)}` : 'Registros do mês'}
        </h2>
        <TabelaDoMeuEspelho
          rotulo="Registros do mês"
          dias={dados?.dias}
          carregando={espelho.isLoading}
          erro={espelho.error ?? undefined}
          aoTentarDeNovo={() => void espelho.refetch()}
          aoPedirAjuste={(dia) => setModal({ tipo: 'ajuste', dia: dia.data })}
        />
      </section>
      <div className="grid grid-cols-[2fr_1fr] items-start gap-6">
        <div className="flex flex-col gap-6">
          <MeusPedidosDeAjuste />
          <MeusAtestados />
        </div>
        <BlocoDoBancoDeHoras />
      </div>
      {modal?.tipo === 'ajuste' && (
        <ModalPedirAjuste dia={modal.dia} aoFechar={() => setModal(null)} />
      )}
      {modal?.tipo === 'atestado' && <ModalEnviarAtestado aoFechar={() => setModal(null)} />}
      <Outlet />
    </div>
  )
}
