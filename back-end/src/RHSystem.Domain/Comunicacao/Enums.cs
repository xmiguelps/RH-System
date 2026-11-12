namespace RHSystem.Domain.Comunicacao;

/// <summary>O tipo do aviso (<c>AVISOS.TIPO_AVISO</c>, texto livre no banco): <c>SIMPLES</c> (só uma mensagem curta) ou <c>DETALHADO</c> (título e conteúdo em markdown).</summary>
public enum TipoDeAviso
{
    Simples,
    Detalhado,
}

/// <summary>A situação do aviso, <b>calculada</b> das colunas e do relógio (nunca gravada): rascunho, agendado, ativo ou expirado. Encerrar é expirar agora; excluir é apagar.</summary>
public enum SituacaoDoAviso
{
    Rascunho,
    Agendado,
    Ativo,
    Expirado,
}

/// <summary>O tipo do holerite (<c>HOLERITES.TIPO_HOLERITE</c>, <c>CHK_HOLERITES_TIPO</c>): <c>MENSAL</c>, <c>DECIMO_TERCEIRO</c> ou <c>FERIAS</c>.</summary>
public enum TipoDeHolerite
{
    Mensal,
    DecimoTerceiro,
    Ferias,
}

/// <summary>O tipo da advertência (<c>ADVERTENCIAS.TIPO_ADVERTENCIA</c>, texto livre no banco; <c>x-provisorio</c> no contrato): <c>VERBAL</c> ou <c>ESCRITA</c>.</summary>
public enum TipoDeAdvertencia
{
    Verbal,
    Escrita,
}

/// <summary>A situação da advertência (<c>CHK_ADVERTENCIAS_STATUS</c>): nasce <c>PENDENTE</c>; a ciência a torna <c>APLICADA</c>; a contestação (só de pendente) a torna <c>CONTESTADA</c>; a resposta a mantém (<c>APLICADA</c>) ou a cancela (<c>CANCELADA</c>, final).</summary>
public enum SituacaoDaAdvertencia
{
    Pendente,
    Aplicada,
    Contestada,
    Cancelada,
}

/// <summary>O desfecho da contestação, <b>deduzido</b> (não há coluna): <c>CANCELADA</c> é <c>ACATADA</c>; <c>APLICADA</c> com texto de contestação é <c>MANTIDA</c>.</summary>
public enum DesfechoDaContestacao
{
    Mantida,
    Acatada,
}

/// <summary>A situação do holerite, calculada da data de publicação e do relógio: <c>PUBLICADO</c> (a pessoa já vê) ou <c>AGENDADO</c> (a data é futura).</summary>
public enum SituacaoDoHolerite
{
    Publicado,
    Agendado,
}
