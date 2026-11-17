import { useState } from 'react'
import { Outlet } from 'react-router'
import { $api } from '../../shared/api/api'
import type { Esquema } from '../../shared/api/tipos'
import { formatarData, formatarDataDoInstante } from '../../shared/formato/datas'
import { plural } from '../../shared/formato/texto'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Botao } from '../../shared/ui/Botao'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Selo } from '../../shared/ui/Selo'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { ModalAtualizarTrajeto, ModalIncluirDependente } from './ModaisDeBeneficio'
import { IconeDoBeneficio } from './IconeDoBeneficio'
import { SITUACAO_DO_PEDIDO_DE_BENEFICIO } from './rotulosDosBeneficios'

type Cartao = Esquema<'BeneficioAdesaoCartao'>
type Disponivel = Esquema<'BeneficioAdesaoDisponivel'>
type Pedido = Esquema<'BeneficioAdesaoPedidoResumo'>

function CartaoDoBeneficio({
  cartao,
  aoIncluirDependente,
  aoAtualizarTrajeto,
}: {
  cartao: Cartao
  aoIncluirDependente: () => void
  aoAtualizarTrajeto: () => void
}) {
  const aprovados = cartao.dependentes.filter((d) => d.situacao === 'APROVADO').length
  return (
    <li
      aria-label={cartao.beneficio.nome}
      className="flex flex-col gap-3 rounded-cartao border border-borda bg-superficie p-5"
    >
      <div className="flex items-center justify-between gap-2">
        <IconeDoBeneficio nome={cartao.beneficio.nome} />
        <Selo tom="sucesso">Ativo</Selo>
      </div>
      <p className="text-corpo font-bold text-texto">{cartao.beneficio.nome}</p>
      {cartao.beneficio.descricao && (
        <p className="text-tabela text-texto-suave">{cartao.beneficio.descricao}</p>
      )}
      {cartao.aceitaDependentes && (
        <p className="text-tabela text-texto">
          Quem está no plano: Você
          {aprovados > 0 ? ` e ${aprovados} ${plural(aprovados, 'dependente', 'dependentes')}` : ''}
        </p>
      )}
      {/* só as linhas ou os meios: a origem é o endereço de casa, e a tela pode estar num projetor */}
      {cartao.aceitaTrajeto && cartao.trajetoAtual && (
        <p className="text-tabela text-texto">Trajeto: {cartao.trajetoAtual.linhasMeios}</p>
      )}
      {(cartao.aceitaDependentes || cartao.aceitaTrajeto) && (
        <div className="flex flex-wrap gap-2">
          {cartao.aceitaDependentes && (
            <Botao variante="secundario" tamanho="pequeno" onClick={aoIncluirDependente}>
              Incluir dependente
            </Botao>
          )}
          {cartao.aceitaTrajeto && (
            <Botao variante="secundario" tamanho="pequeno" onClick={aoAtualizarTrajeto}>
              Atualizar trajeto
            </Botao>
          )}
        </div>
      )}
    </li>
  )
}

function CartaoDisponivel({ item }: { item: Disponivel }) {
  return (
    <li
      aria-label={item.beneficio.nome}
      className="flex flex-col gap-3 rounded-cartao border border-dashed border-tracejado bg-superficie p-5"
    >
      <IconeDoBeneficio nome={item.beneficio.nome} />
      <p className="text-corpo font-bold text-texto">{item.beneficio.nome}</p>
      {item.beneficio.descricao && (
        <p className="text-tabela text-texto-suave">{item.beneficio.descricao}</p>
      )}
      {item.ultimoPedido && (
        <p className="text-pequeno text-texto-suave">
          {item.ultimoPedido.situacao === 'RECUSADO'
            ? `Seu último pedido foi recusado${item.ultimoPedido.motivoRecusa ? `: ${item.ultimoPedido.motivoRecusa}` : '.'}`
            : 'Seu último pedido foi cancelado.'}
        </p>
      )}
      <div>
        <BotaoLink to={`aderir/${item.beneficio.id}`} tamanho="pequeno">
          Quero aderir
        </BotaoLink>
      </div>
    </li>
  )
}

/** Telas 44 (funcionário; o gestor e o RH usam a mesma): benefícios ativos, os que dá para pedir e os pedidos feitos. */
export function PaginaMeusBeneficios() {
  const consulta = $api.useQuery('get', '/api/eu/beneficios')
  const [incluindoDependenteEm, setIncluindoDependenteEm] = useState<Cartao | null>(null)
  const [atualizandoTrajetoDe, setAtualizandoTrajetoDe] = useState<Cartao | null>(null)
  const d = consulta.data

  const colunas: readonly ColunaDaTabela<Pedido>[] = [
    {
      chave: 'pedido',
      titulo: 'Pedido',
      celula: (p) => (
        <span className="flex flex-col gap-1">
          <span>{p.titulo}</span>
          {p.situacao === 'PENDENTE' && (
            <span className="text-pequeno text-texto-suave">
              Resposta até {formatarData(p.respostaPrevistaAte)}
            </span>
          )}
          {p.situacao === 'RECUSADO' && p.motivoRecusa && (
            <span className="text-pequeno text-texto-suave">Motivo: {p.motivoRecusa}</span>
          )}
        </span>
      ),
    },
    { chave: 'data', titulo: 'Data', celula: (p) => formatarDataDoInstante(p.pedidoEmUtc) },
    {
      chave: 'situacao',
      titulo: 'Situação',
      celula: (p) => (
        <Selo tom={SITUACAO_DO_PEDIDO_DE_BENEFICIO[p.situacao].tom}>
          {SITUACAO_DO_PEDIDO_DE_BENEFICIO[p.situacao].texto}
        </Selo>
      ),
    },
  ]

  return (
    <div className="flex flex-col gap-6">
      <CabecalhoDePagina
        titulo="Benefícios"
        subtitulo="O que a empresa oferece para você e como usar"
      />
      {consulta.error ? (
        <AvisoDeErro erro={consulta.error} aoTentarDeNovo={() => void consulta.refetch()} />
      ) : (
        <>
          <section aria-labelledby="titulo-seus-beneficios" className="flex flex-col gap-3">
            <h2 id="titulo-seus-beneficios" className="text-t3 font-bold text-texto">
              Seus benefícios
            </h2>
            {!d ? (
              <Esqueleto className="h-32 w-full" />
            ) : d.beneficios.length === 0 ? (
              <p className="text-corpo text-texto-suave">Você ainda não tem benefícios ativos.</p>
            ) : (
              <ul className="grid grid-cols-4 gap-4">
                {d.beneficios.map((cartao) => (
                  <CartaoDoBeneficio
                    key={cartao.adesaoId}
                    cartao={cartao}
                    aoIncluirDependente={() => setIncluindoDependenteEm(cartao)}
                    aoAtualizarTrajeto={() => setAtualizandoTrajetoDe(cartao)}
                  />
                ))}
              </ul>
            )}
          </section>
          {d && d.disponiveis.length > 0 && (
            <section aria-labelledby="titulo-disponiveis" className="flex flex-col gap-3">
              <h2 id="titulo-disponiveis" className="text-t3 font-bold text-texto">
                Disponíveis para você
              </h2>
              <p className="text-tabela text-texto-suave">
                Benefícios que você ainda não tem e pode pedir. O RH analisa e responde em até 5
                dias úteis; você vê a resposta em Seus pedidos.
              </p>
              <ul className="grid grid-cols-4 gap-4">
                {d.disponiveis.map((item) => (
                  <CartaoDisponivel key={item.beneficio.id} item={item} />
                ))}
              </ul>
            </section>
          )}
          <section aria-labelledby="titulo-pedidos" className="flex flex-col gap-3">
            <h2 id="titulo-pedidos" className="text-t3 font-bold text-texto">
              Seus pedidos
            </h2>
            <Tabela
              rotulo="Seus pedidos"
              colunas={colunas}
              linhas={d?.pedidos}
              chaveDaLinha={(p) => `${p.tipo}-${p.id}`}
              carregando={consulta.isLoading}
              vazio={<EstadoVazio titulo="Você ainda não fez pedidos." />}
            />
          </section>
          <p className="text-pequeno text-texto-suave">
            Dúvida sobre algum benefício? Fale com o RH
          </p>
        </>
      )}
      <ModalIncluirDependente
        cartao={incluindoDependenteEm}
        aoFechar={() => setIncluindoDependenteEm(null)}
      />
      <ModalAtualizarTrajeto
        cartao={atualizandoTrajetoDe}
        aoFechar={() => setAtualizandoTrajetoDe(null)}
      />
      <Outlet />
    </div>
  )
}
