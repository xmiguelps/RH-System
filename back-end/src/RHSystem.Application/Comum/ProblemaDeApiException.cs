namespace RHSystem.Application.Comum;

/// <summary>
/// Um erro com o status, o título, o detalhe e o <c>codigo</c> que o contrato descreve para aquela rota, mais campos extras do corpo (o <c>cadastroExistente</c> do 409 de CPF
/// repetido) e cabeçalhos da resposta (o <c>Retry-After</c> do 429). Serve onde as exceções simples (<see cref="ErroDeNegocioException"/> e as outras) não bastam: elas fixam o título
/// e não levam extras. A Api devolve <c>application/problem+json</c> com <c>title</c>, <c>status</c>, <c>detail</c>, <c>traceId</c>, <c>codigo</c> (se houver) e as
/// <paramref name="extensoes"/> em camelCase; extensão com o nome de <c>title</c>, <c>status</c>, <c>detail</c>, <c>traceId</c>, <c>codigo</c>, <c>type</c> ou <c>instance</c> é ignorada.
/// Os <paramref name="cabecalhos"/> vão para a resposta como estão. Nada aqui pode levar dado interno nem segredo: tudo vai ao cliente.
/// </summary>
public sealed class ProblemaDeApiException : Exception
{
    public ProblemaDeApiException(
        int status,
        string titulo,
        string detalhe,
        string? codigo = null,
        IReadOnlyDictionary<string, object?>? extensoes = null,
        IReadOnlyDictionary<string, string>? cabecalhos = null)
        : base(detalhe)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(status, 400);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(status, 599);
        ArgumentException.ThrowIfNullOrWhiteSpace(titulo);
        ArgumentException.ThrowIfNullOrWhiteSpace(detalhe);
        Status = status;
        Titulo = titulo;
        Detalhe = detalhe;
        Codigo = codigo;
        Extensoes = extensoes;
        Cabecalhos = cabecalhos;
    }

    public int Status { get; }

    public string Titulo { get; }

    public string Detalhe { get; }

    public string? Codigo { get; }

    public IReadOnlyDictionary<string, object?>? Extensoes { get; }

    public IReadOnlyDictionary<string, string>? Cabecalhos { get; }
}
