import type { Esquema } from '../../shared/api/tipos'
import { formatarMinutosComoHoras } from '../../shared/formato/numeros'
import { Avatar } from '../../shared/ui/Avatar'
import { BotaoLink } from '../../shared/ui/BotaoLink'
import { cn } from '../../shared/ui/estilo'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Selo } from '../../shared/ui/Selo'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import { textoDeDias } from '../ferias/rotulosDasFerias'
import { textoDoPontoHoje } from './rotulosDaEquipe'

type Pessoa = Esquema<'PainelPessoaDaEquipe'>

const SELO_DA_AVALIACAO: Record<
  NonNullable<Pessoa['avaliacaoSituacao']>,
  { texto: string; tom: 'sucesso' | 'alerta' | 'neutro' }
> = {
  RESPONDIDA: { texto: 'Respondida', tom: 'sucesso' },
  PENDENTE: { texto: 'Pendente', tom: 'alerta' },
  NAO_SE_APLICA: { texto: 'Não se aplica', tom: 'neutro' },
}

/** "Pessoas da equipe" (tela 51): a ordem é a da API (alfabética). A coluna da avaliação só existe com ciclo aberto. */
export function TabelaDaEquipe({
  pessoas,
  cicloDaAvaliacao,
  carregando,
}: {
  pessoas: readonly Pessoa[] | undefined
  cicloDaAvaliacao: string | null
  carregando: boolean
}) {
  const colunas: ColunaDaTabela<Pessoa>[] = [
    {
      chave: 'pessoa',
      titulo: 'Pessoa',
      celula: (p) => (
        <span className="flex items-center gap-3">
          <Avatar nome={p.funcionario.nome} />
          <span className="flex flex-col">
            <span className="font-semibold text-texto">{p.funcionario.nome}</span>
            {p.funcionario.cargo && (
              <span className="text-pequeno text-texto-suave">{p.funcionario.cargo}</span>
            )}
          </span>
        </span>
      ),
    },
    {
      chave: 'ponto',
      titulo: 'Ponto hoje',
      celula: (p) => (
        <span
          className={cn(
            p.pontoHoje.situacaoHoje === 'SEM_REGISTRO' && 'font-semibold text-alerta-forte',
          )}
        >
          {textoDoPontoHoje(p.pontoHoje)}
        </span>
      ),
    },
    {
      chave: 'banco',
      titulo: 'Banco de horas',
      celula: (p) => formatarMinutosComoHoras(p.bancoDeHorasMinutos, { sinal: true }),
    },
    {
      chave: 'ferias',
      titulo: 'Férias disponíveis',
      celula: (p) => textoDeDias(p.feriasDisponiveisDias),
    },
    ...(cicloDaAvaliacao
      ? [
          {
            chave: 'avaliacao',
            titulo: `Avaliação ${cicloDaAvaliacao}`,
            celula: (p: Pessoa) => {
              const selo = p.avaliacaoSituacao ? SELO_DA_AVALIACAO[p.avaliacaoSituacao] : null
              return selo ? <Selo tom={selo.tom}>{selo.texto}</Selo> : '—'
            },
          } satisfies ColunaDaTabela<Pessoa>,
        ]
      : []),
    {
      chave: 'advertencias',
      titulo: 'Advertências',
      celula: (p) =>
        p.advertencias > 0 ? (
          <span className="font-semibold text-erro-texto">{p.advertencias}</span>
        ) : (
          'Nenhuma'
        ),
    },
    {
      chave: 'acoes',
      titulo: 'Ações',
      alinhamento: 'direita',
      celula: (p) => (
        <BotaoLink
          to={String(p.funcionario.id)}
          variante="secundario"
          tamanho="pequeno"
          aria-label={`Abrir a ficha de ${p.funcionario.nome}`}
        >
          Abrir ficha
        </BotaoLink>
      ),
    },
  ]
  return (
    <Tabela
      rotulo="Pessoas da equipe"
      colunas={colunas}
      linhas={pessoas}
      chaveDaLinha={(p) => p.funcionario.id}
      carregando={carregando}
      vazio={<EstadoVazio titulo="Seu setor ainda não tem pessoas ativas." />}
    />
  )
}
