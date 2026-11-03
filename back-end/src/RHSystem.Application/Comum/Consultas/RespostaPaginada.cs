namespace RHSystem.Application.Comum.Consultas;

/// <summary>
/// O corpo de toda lista paginada do contrato (<c>itens</c> mais <c>pagina</c>, <c>tamanhoPagina</c>, <c>total</c> e <c>totalPaginas</c>). Uma resposta com
/// campo extra herda: <c>record FuncionarioListaRespostaRh(..., Totais Totais) : RespostaPaginada&lt;Item&gt;(...)</c>.
/// </summary>
public record RespostaPaginada<T>(IReadOnlyList<T> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas)
{
    /// <summary><c>TotalPaginas</c> é o teto de <c>Total / TamanhoPagina</c>, e 0 quando não há itens.</summary>
    public static RespostaPaginada<T> De(IReadOnlyList<T> itens, int total, ParametrosDeLista p) =>
        new(itens, p.Pagina, p.TamanhoPagina, total, (int)(((long)total + p.TamanhoPagina - 1) / p.TamanhoPagina));
}
