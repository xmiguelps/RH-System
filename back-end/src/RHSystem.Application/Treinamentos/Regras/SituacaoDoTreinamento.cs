namespace RHSystem.Application.Treinamentos.Regras;

/// <summary>A situação do treinamento (<c>TreinamentoSituacao</c> do contrato): não existe coluna, sai das datas de hoje (em -03:00).</summary>
public enum SituacaoDoTreinamento
{
    Rascunho,
    Agendado,
    EmAndamento,
    Encerrado,
}

/// <summary>
/// A função ÚNICA da situação: <c>ENCERRADO_EM</c> preenchido = encerrado (vale mais que a data); sem publicar = rascunho; data de início depois de hoje = agendado; senão em andamento (a data de hoje já libera; publicado sem data, que só existe em dado antigo, já está liberado). Liberar e encerrar não têm agendador:
/// é só esta conta.
/// </summary>
public static class RegraDaSituacaoDoTreinamento
{
    public static SituacaoDoTreinamento De(bool publicado, DateOnly? comecaEm, DateTime? encerradoEmUtc, DateOnly hoje)
    {
        if (encerradoEmUtc is not null)
        {
            return SituacaoDoTreinamento.Encerrado;
        }

        if (!publicado)
        {
            return SituacaoDoTreinamento.Rascunho;
        }

        return comecaEm is { } dia && dia > hoje ? SituacaoDoTreinamento.Agendado : SituacaoDoTreinamento.EmAndamento;
    }

    /// <summary>Publicado e com a data de início já chegada (ou sem data): é o que a pessoa pode ver.</summary>
    public static bool Liberado(bool publicado, DateOnly? comecaEm, DateOnly hoje) => publicado && (comecaEm is null || comecaEm <= hoje);
}
