namespace RHSystem.Application.Comum.Consultas;

/// <summary>
/// No JSON: <c>ASC</c> ou <c>DESC</c>. Na consulta (<c>?direcao=ASC</c>) este tipo NÃO entra no <c>[AsParameters]</c>: o binder do minimal API só entende o nome do C# (<c>Asc</c>) e recusaria o
/// <c>ASC</c> do contrato com um 400 de corpo (medido na fatia 1b). A rota recebe o texto e o caso de uso o lê com <c>EnumsDeConsulta.Ler&lt;DirecaoDeOrdenacao&gt;</c>.
/// </summary>
public enum DirecaoDeOrdenacao
{
    Asc,
    Desc,
}

/// <summary>
/// Os cinco parâmetros de lista do contrato (<c>pagina</c>, <c>tamanhoPagina</c>, <c>busca</c>, <c>ordenarPor</c>, <c>direcao</c>), para <c>[AsParameters]</c>
/// SÓ na rota cujo contrato declara os cinco. A que declara outros (a auditoria, sem <c>ordenarPor</c> e <c>direcao</c>) tem um objeto de consulta
/// próprio, só com o que o contrato declara, e monta <c>new ParametrosDeLista(pagina, tamanhoPagina, busca).Validado()</c> para a paginação.
/// </summary>
public sealed record ParametrosDeLista(
    int Pagina = 1,
    int TamanhoPagina = 20,
    string? Busca = null,
    string? OrdenarPor = null,
    DirecaoDeOrdenacao? Direcao = null)
{
    public const int TamanhoMaximoDaPagina = 100;
    public const int TamanhoMaximoDaBusca = 100;

    /// <summary>Quantos itens pular: <c>(Pagina - 1) * TamanhoPagina</c>, sem estourar o <c>int</c> para uma página gigante (que devolve a lista vazia).</summary>
    public int Pular => (int)Math.Min(int.MaxValue, ((long)Pagina - 1) * TamanhoPagina);

    /// <summary>
    /// Confere os limites do contrato e devolve a cópia pronta: a busca aparada, e <c>null</c> se só tinha espaços. <c>ordenarPor</c> e
    /// <c>direcao</c> quem confere é o <see cref="MapaDeOrdenacao{T}"/>, que sabe os valores aceitos da rota.
    /// </summary>
    /// <exception cref="ErroDeValidacaoException">Com <c>errors</c> por campo: <c>pagina</c>, <c>tamanhoPagina</c>, <c>busca</c>.</exception>
    public ParametrosDeLista Validado()
    {
        var erros = new ColetorDeErros();
        if (Pagina < 1)
        {
            erros.Adicionar("pagina", "A página começa em 1.");
        }

        if (TamanhoPagina is < 1 or > TamanhoMaximoDaPagina)
        {
            erros.Adicionar("tamanhoPagina", $"O tamanho da página vai de 1 a {TamanhoMaximoDaPagina}.");
        }

        var busca = string.IsNullOrWhiteSpace(Busca) ? null : Busca.Trim();
        if (busca is { Length: > TamanhoMaximoDaBusca })
        {
            erros.Adicionar("busca", $"A busca aceita até {TamanhoMaximoDaBusca} caracteres.");
        }

        erros.LancarSeHouver();
        return this with { Busca = busca };
    }
}
