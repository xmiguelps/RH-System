import type { Esquema } from '../../shared/api/tipos'

type Treinamento = Esquema<'TreinamentoRh'>
type Comando = Esquema<'TreinamentoComando'>

/** O estado do assistente: o comando inteiro (o PUT substitui tudo) mais as pessoas com nome. */
export type Rascunho = Omit<Comando, 'funcionarioIds'> & {
  pessoas: { id: number; nome: string }[]
}

export const RASCUNHO_NOVO: Rascunho = {
  titulo: '',
  descricao: '',
  comecaEm: '',
  obrigatorio: true,
  onboarding: false,
  emiteCertificado: true,
  publico: 'TODOS',
  setorIds: [],
  pessoas: [],
  incluiAdmitidos: true,
}

export function rascunhoDe(t: Treinamento | undefined): Rascunho {
  if (!t) return RASCUNHO_NOVO
  return {
    titulo: t.titulo,
    descricao: t.descricao ?? '',
    comecaEm: t.comecaEm ?? '',
    obrigatorio: t.obrigatorio,
    onboarding: t.onboarding,
    emiteCertificado: t.emiteCertificado,
    publico: t.publico ?? 'TODOS',
    setorIds: t.setores.map((s) => s.id),
    pessoas: t.pessoas.map((p) => ({ id: p.id, nome: p.nome })),
    incluiAdmitidos: t.incluiAdmitidos,
  }
}

/** O comando inteiro, sempre (campo omitido no PUT volta ao padrão). */
export function comandoDe(r: Rascunho): Comando {
  const publico = r.publico ?? 'TODOS'
  return {
    titulo: r.titulo.trim(),
    descricao: r.descricao?.trim() || null,
    comecaEm: r.comecaEm || null,
    obrigatorio: r.obrigatorio,
    onboarding: r.onboarding,
    emiteCertificado: r.emiteCertificado,
    publico,
    setorIds: publico === 'SETORES' ? r.setorIds : [],
    funcionarioIds: publico === 'PESSOAS' ? r.pessoas.map((p) => p.id) : [],
    incluiAdmitidos: r.obrigatorio ? r.incluiAdmitidos : false,
  }
}

/** Depois de publicado só mudam título, descrição, incluir admitidos e (agendado) "Começa em". */
export function travasDoTreinamento(t: Treinamento | undefined): {
  encerrado: boolean
  publicado: boolean
  comecaEmLivre: boolean
} {
  const encerrado = t?.situacao === 'ENCERRADO'
  const publicado = !!t && t.situacao !== 'RASCUNHO'
  return {
    encerrado,
    publicado,
    comecaEmLivre: !t || t.situacao === 'RASCUNHO' || t.situacao === 'AGENDADO',
  }
}

export const DICA_DO_PUBLICADO =
  'Depois de publicado, só dá para mudar o título, a descrição e a opção de incluir quem for admitido.'

export const DICA_DOS_MODULOS =
  'Este treinamento já foi publicado. Não dá mais para acrescentar, reordenar nem excluir módulos; só corrigir o conteúdo deles.'
