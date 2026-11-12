namespace RHSystem.Application.Formularios.Acompanhar;

/// <summary>
/// A leitura do resultado e da exportação. <b>Nunca devolve de uma resposta anônima nada além do que o resultado soma</b>: a resposta, a alternativa e o avaliado, sem id, sem data e sem autor (e sem ordem de gravação: quem usa soma e ordena por texto).
/// </summary>
public interface IConsultaDeResultado
{
    /// <summary>As respostas do formulário (as de pessoas, de candidatos e as anônimas), só o que o resultado soma. <paramref name="avaliadoId"/> restringe às de um avaliado.</summary>
    Task<IReadOnlyList<RespostaParaResultado>> RespostasAsync(int formularioId, int? avaliadoId, CancellationToken ct);

    /// <summary>O total que o resultado mostra: as participações (anônimo) ou os envios distintos de pessoas e candidatos (com nome), só os do avaliado se ele foi dado.</summary>
    Task<int> TotalAsync(int formularioId, bool anonimo, int? avaliadoId, CancellationToken ct);

    /// <summary>Quantas participações cada avaliado tem num formulário anônimo (para saber quem chegou ao mínimo).</summary>
    Task<IReadOnlyDictionary<int, int>> ParticipacoesPorAvaliadoAsync(int formularioId, CancellationToken ct);

    /// <summary>As linhas do CSV de um formulário COM NOME ou por link: uma por resposta, com a data no instante UTC (o caso de uso põe o fuso).</summary>
    Task<IReadOnlyList<LinhaDeExportacaoDeResposta>> LinhasParaExportarAsync(int formularioId, CancellationToken ct);
}
