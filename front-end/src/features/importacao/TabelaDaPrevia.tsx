import { useState } from 'react'
import type { Esquema } from '../../shared/api/tipos'
import { cn } from '../../shared/ui/estilo'
import { Interruptor } from '../../shared/ui/Interruptor'
import { Paginacao } from '../../shared/ui/Paginacao'
import { Selo } from '../../shared/ui/Selo'
import { Tabela, type ColunaDaTabela } from '../../shared/ui/Tabela'
import {
  COLUNAS_DO_MODELO,
  LINHAS_POR_PAGINA_DA_PREVIA,
  ORDEM_DAS_COLUNAS,
  type ColunaDaImportacao,
} from './rotulosDaImportacao'

type Linha = Esquema<'ImportacaoLinha'>

function colunasComErro(linha: Linha): ReadonlySet<ColunaDaImportacao> {
  return new Set(linha.erros.map((e) => e.coluna))
}

const COLUNAS: readonly ColunaDaTabela<Linha>[] = [
  { chave: 'linha', titulo: 'Linha', celula: (l) => l.numeroLinha },
  {
    chave: 'situacao',
    titulo: 'Situação',
    celula: (l) =>
      l.situacao === 'OK' ? <Selo tom="sucesso">OK</Selo> : <Selo tom="erro">Com erro</Selo>,
  },
  ...ORDEM_DAS_COLUNAS.map((coluna): ColunaDaTabela<Linha> => ({
    chave: coluna,
    titulo: COLUNAS_DO_MODELO[coluna].tituloCurto,
    celula: (l) => {
      const valor = l.valores[COLUNAS_DO_MODELO[coluna].chave]
      const comErro = colunasComErro(l).has(coluna)
      return (
        <span
          data-erro={comErro || undefined}
          className={cn(
            'inline-block whitespace-nowrap rounded-campo px-1.5 py-0.5',
            comErro && 'border border-erro-texto',
            valor === null && 'text-texto-suave',
          )}
        >
          {valor ?? '(vazio)'}
        </span>
      )
    },
  })),
]

/** A prévia que a API leu, paginada no cliente (até 500 linhas); a tela não edita célula. */
export function TabelaDaPrevia({ previa }: { previa: Esquema<'ImportacaoPrevia'> }) {
  const [soComErro, setSoComErro] = useState(previa.linhasComErro > 0)
  const [pagina, setPagina] = useState(1)
  const visiveis = soComErro
    ? previa.linhas.filter((l) => l.situacao === 'COM_ERRO')
    : previa.linhas
  const totalPaginas = Math.max(1, Math.ceil(visiveis.length / LINHAS_POR_PAGINA_DA_PREVIA))
  const atual = Math.min(pagina, totalPaginas)
  const daPagina = visiveis.slice(
    (atual - 1) * LINHAS_POR_PAGINA_DA_PREVIA,
    atual * LINHAS_POR_PAGINA_DA_PREVIA,
  )

  return (
    <div className="flex flex-col gap-3">
      {previa.linhasComErro > 0 && (
        <Interruptor
          rotulo="Mostrar só as linhas com erro"
          ligado={soComErro}
          aoMudar={(ligado) => {
            setSoComErro(ligado)
            setPagina(1)
          }}
        />
      )}
      <Tabela
        rotulo="Prévia da planilha"
        colunas={COLUNAS}
        linhas={daPagina}
        chaveDaLinha={(l) => l.numeroLinha}
        detalhe={(l) =>
          l.erros.length === 0 ? null : (
            <ul aria-label={`Erros da linha ${l.numeroLinha}`} className="flex flex-col gap-1">
              {l.erros.map((e, i) => (
                <li key={`${e.coluna}-${i}`} className="text-erro-texto">
                  {COLUNAS_DO_MODELO[e.coluna].titulo}: {e.mensagem}
                </li>
              ))}
            </ul>
          )
        }
        rodape={
          visiveis.length > LINHAS_POR_PAGINA_DA_PREVIA ? (
            <Paginacao
              dados={{
                pagina: atual,
                tamanhoPagina: LINHAS_POR_PAGINA_DA_PREVIA,
                total: visiveis.length,
                totalPaginas,
              }}
              aoMudar={setPagina}
              resumo={(de, ate, total) => `Linhas ${de} a ${ate} de ${total}`}
            />
          ) : undefined
        }
      />
    </div>
  )
}
