import { useState } from 'react'
import { Outlet, useSearchParams } from 'react-router'
import { $api, clienteHttp } from '../../shared/api/api'
import type { Esquema } from '../../shared/api/tipos'
import { baixarExportacao } from '../../shared/arquivos/baixar'
import { useBaixarPorLink } from '../../shared/arquivos/useBaixarPorLink'
import { formatarMesAno, hojeEmSaoPaulo } from '../../shared/formato/datas'
import { formatarMinutosComoHoras, formatarNumero } from '../../shared/formato/numeros'
import { inteiroDaUrl } from '../../shared/lista/valoresDeLista'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Bolinha } from '../../shared/ui/Bolinha'
import { Botao } from '../../shared/ui/Botao'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { Campo } from '../../shared/ui/Campo'
import { ComboDeBusca } from '../../shared/ui/ComboDeBusca'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Selecao } from '../../shared/ui/Selecao'
import { useNotificacao } from '../../shared/ui/useNotificacao'
import { mensagemDeErro } from '../../shared/api/mensagemDeErro'
import { FeriadosDoMes } from './FeriadosDoMes'
import { ModalCorrigirBatidas } from './ModalCorrigirBatidas'
import { ModalPedidosDeAjusteRh } from './ModalPedidosDeAjusteRh'
import { NumerosDoPonto } from './PecasDoPonto'
import { FILA_DO_RH, saldoComSinal, tomDoSaldo } from './rotulosDoPonto'
import { TabelaDoEspelhoRh } from './TabelaDoEspelhoRh'
import { useBuscaDeColaborador } from './useBuscaDeColaborador'

type Dia = Esquema<'PontoEspelhoDia'>

/** Os últimos 12 meses, do atual para trás ('2026-10'). */
function ultimosMeses(hoje: string, incluir?: string): { valor: string; rotulo: string }[] {
  let ano = Number(hoje.slice(0, 4))
  let mes = Number(hoje.slice(5, 7))
  const meses: { valor: string; rotulo: string }[] = []
  for (let i = 0; i < 12; i++) {
    meses.push({
      valor: `${ano}-${String(mes).padStart(2, '0')}`,
      rotulo: formatarMesAno(ano, mes),
    })
    mes -= 1
    if (mes === 0) {
      mes = 12
      ano -= 1
    }
  }
  if (incluir && !meses.some((m) => m.valor === incluir)) {
    meses.push({
      valor: incluir,
      rotulo: formatarMesAno(Number(incluir.slice(0, 4)), Number(incluir.slice(5, 7))),
    })
  }
  return meses
}

/** Tela 12: espelho de qualquer pessoa, feriados, exportar para a folha; o modal 13 por `?pedido`. */
export function PaginaPontoEFeriados() {
  const notificacao = useNotificacao()
  const [busca, setBusca] = useSearchParams()
  const hoje = hojeEmSaoPaulo()
  const funcionarioId = inteiroDaUrl(busca.get('funcionarioId') ?? undefined)
  const anoDaUrl = inteiroDaUrl(busca.get('ano') ?? undefined)
  const mesDaUrl = inteiroDaUrl(busca.get('mes') ?? undefined)
  const ano = anoDaUrl ?? Number(hoje.slice(0, 4))
  const mes = mesDaUrl && mesDaUrl >= 1 && mesDaUrl <= 12 ? mesDaUrl : Number(hoje.slice(5, 7))
  const periodo = `${ano}-${String(mes).padStart(2, '0')}`
  const pedidoNaUrl = inteiroDaUrl(busca.get('pedido') ?? undefined)
  const modalAberto = pedidoNaUrl !== undefined || busca.get('pedidos') === 'abertos'
  const [editando, setEditando] = useState<Dia | null>(null)
  const [exportando, setExportando] = useState(false)
  const { baixar, baixando } = useBaixarPorLink()

  const espelho = $api.useQuery(
    'get',
    '/api/rh/funcionarios/{funcionarioId}/ponto',
    { params: { path: { funcionarioId: funcionarioId ?? 0 }, query: { ano, mes } } },
    { enabled: funcionarioId !== undefined },
  )
  const fila = $api.useQuery('get', '/api/rh/ponto/pedidos-de-ajuste', {
    params: { query: FILA_DO_RH },
  })
  const atestados = $api.useQuery('get', '/api/rh/ponto/atestados', {
    params: { query: { situacao: 'ENVIADO', tamanhoPagina: 1 } },
  })
  const pessoa = espelho.data?.funcionario
  const colaboradores = useBuscaDeColaborador(
    pessoa ? { id: pessoa.id, nome: pessoa.nome } : undefined,
  )

  function mudar(valores: Record<string, string | null>) {
    setBusca((p) => {
      const novo = new URLSearchParams(p)
      for (const [chave, valor] of Object.entries(valores)) {
        if (valor === null) novo.delete(chave)
        else novo.set(chave, valor)
      }
      return novo
    })
  }

  async function exportar() {
    if (exportando) return
    setExportando(true)
    try {
      await baixarExportacao(
        clienteHttp.GET('/api/rh/ponto/folha/exportacao', {
          params: { query: { ano, mes } },
          parseAs: 'blob',
        }),
        `ponto-${periodo}.csv`,
      )
    } catch (erro) {
      notificacao.erro(mensagemDeErro(erro))
    } finally {
      setExportando(false)
    }
  }

  const resumo = espelho.data?.resumo
  const comRh = fila.data?.resumo.comRh
  const paraValidar = atestados.data?.total

  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo="Ponto e feriados"
        subtitulo="Espelho de ponto de cada colaborador e feriados da empresa"
        acoes={
          <>
            <BotaoLink
              variante="secundario"
              to={{ pathname: 'atestados', search: busca.toString() }}
            >
              Atestados
              {paraValidar !== undefined && paraValidar > 0 && <Bolinha valor={paraValidar} />}
            </BotaoLink>
            <Botao
              variante="secundario"
              onClick={() => mudar({ pedidos: 'abertos', pedido: null })}
            >
              Pedidos de ajuste
              {comRh !== undefined && comRh > 0 && <Bolinha valor={comRh} />}
            </Botao>
            <Botao onClick={() => void exportar()} carregando={exportando}>
              Exportar para a folha
            </Botao>
          </>
        }
      />
      <div className="grid grid-cols-[1fr_260px_auto] items-end gap-4">
        <Campo rotulo="Colaborador">
          <ComboDeBusca
            rotulo="Colaborador"
            placeholder="Busque pelo nome"
            opcoes={colaboradores.opcoes}
            valor={funcionarioId === undefined ? null : String(funcionarioId)}
            aoMudar={(v) => mudar({ funcionarioId: v })}
            aoDigitar={colaboradores.aoDigitar}
            carregando={colaboradores.carregando}
          />
        </Campo>
        <Campo rotulo="Período">
          <Selecao
            rotulo="Período"
            opcoes={ultimosMeses(hoje, periodo)}
            valor={periodo}
            aoMudar={(v) => {
              if (!v) return
              mudar({ ano: String(Number(v.slice(0, 4))), mes: String(Number(v.slice(5, 7))) })
            }}
          />
        </Campo>
        <Botao
          variante="secundario"
          disabled={funcionarioId === undefined}
          carregando={baixando}
          onClick={() => {
            if (funcionarioId === undefined) return
            void baixar(() =>
              clienteHttp.GET('/api/rh/funcionarios/{funcionarioId}/ponto/espelho/download', {
                params: { path: { funcionarioId }, query: { ano, mes } },
              }),
            )
          }}
        >
          Baixar PDF
        </Botao>
      </div>
      <section aria-labelledby="titulo-espelho" className="flex flex-col gap-4">
        <h2 id="titulo-espelho" className="text-t3 font-bold text-texto">
          Espelho de ponto
        </h2>
        {funcionarioId === undefined ? (
          <EstadoVazio titulo="Escolha um colaborador para ver o espelho." />
        ) : espelho.error ? (
          <AvisoDeErro erro={espelho.error} aoTentarDeNovo={() => void espelho.refetch()} />
        ) : (
          <>
            <NumerosDoPonto
              rotulo="Números do espelho"
              carregando={!resumo}
              itens={[
                {
                  rotulo: 'Horas trabalhadas no mês',
                  valor: resumo
                    ? formatarMinutosComoHoras(resumo.horasTrabalhadasNoMesMinutos)
                    : '—',
                },
                {
                  rotulo: 'Banco de horas',
                  valor: saldoComSinal(resumo?.saldoBancoHorasMinutos ?? null),
                  tom: tomDoSaldo(resumo?.saldoBancoHorasMinutos ?? null),
                },
                {
                  rotulo: 'Atrasos',
                  valor: resumo ? formatarNumero(resumo.atrasos) : '—',
                  tom: resumo && resumo.atrasos > 0 ? 'alerta' : 'neutro',
                },
                { rotulo: 'Faltas', valor: resumo ? formatarNumero(resumo.faltas) : '—' },
              ]}
            />
            <TabelaDoEspelhoRh
              dias={espelho.data?.dias}
              carregando={espelho.isLoading}
              aoEditar={setEditando}
              aoRevisarPedido={(id) => mudar({ pedido: String(id), pedidos: null })}
            />
          </>
        )}
      </section>
      <FeriadosDoMes ano={ano} mes={mes} />
      {editando && pessoa && (
        <ModalCorrigirBatidas
          funcionario={{ id: pessoa.id, nome: pessoa.nome }}
          dia={editando}
          aoFechar={() => setEditando(null)}
        />
      )}
      {modalAberto && (
        <ModalPedidosDeAjusteRh
          pedidoInicial={pedidoNaUrl ?? null}
          aoFechar={() => mudar({ pedido: null, pedidos: null })}
        />
      )}
      <Outlet />
    </div>
  )
}
