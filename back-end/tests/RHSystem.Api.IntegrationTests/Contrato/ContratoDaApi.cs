using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>Uma operação do contrato: o método (<c>GET</c>), o caminho (<c>/api/rh/funcionarios/{funcionarioId}</c>), o <c>operationId</c> e o <c>x-perfis</c>.</summary>
internal sealed record OperacaoDoContrato(string Metodo, string Caminho, string OperationId, IReadOnlyList<string> Perfis);

/// <summary>
/// O contrato visto pelos testes que o cruzam com a API: as operações com o <c>x-perfis</c> de cada uma, as rotas que a API TEM de verdade
/// (do <see cref="EndpointDataSource"/>) e os schemas das respostas. As fatias seguintes só chamam isto: não reescrevem a leitura.
/// </summary>
internal static partial class ContratoDaApi
{
    private static readonly string[] Metodos = ["get", "put", "post", "delete", "patch", "options", "head", "trace"];
    private static readonly Lazy<IReadOnlyList<OperacaoDoContrato>> Lidas = new(LerOperacoes);

    public static IReadOnlyList<OperacaoDoContrato> Operacoes => Lidas.Value;

    /// <summary>
    /// As rotas reais da API: só <c>/api</c> e <c>/health</c>, sem segmento <c>__teste</c> (rotas dos testes), sem restrição de rota
    /// (<c>{id:int}</c> vira <c>{id}</c>). O documento OpenAPI e a UI da documentação não entram.
    /// </summary>
    public static IReadOnlyList<(string Metodo, string Caminho)> RotasImplementadas(IServiceProvider servicos)
    {
        var rotas = new SortedSet<(string Metodo, string Caminho)>();
        foreach (var endpoint in servicos.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>())
        {
            var padrao = "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/');
            var segmentos = padrao.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (!(padrao == "/health" || padrao.StartsWith("/api/", StringComparison.Ordinal)) || segmentos.Contains("__teste"))
            {
                continue;
            }

            var caminho = RestricaoDeRota().Replace(padrao, "{$1}");
            foreach (var metodo in endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])
            {
                rotas.Add((metodo.ToUpperInvariant(), caminho));
            }
        }

        return [.. rotas];
    }

    /// <summary>
    /// O <c>operationId</c> de cada rota implementada, casada com as operações do contrato por método e caminho (os nomes dos parâmetros de caminho não
    /// entram no casamento: quem os compara é o teste de divergência). Rota sem operação no contrato fica de fora: a matriz de perfis a acusa.
    /// </summary>
    public static IReadOnlySet<string> OperationIdsImplementados(IServiceProvider servicos) =>
        RotasImplementadas(servicos)
            .Select(r => Operacoes.FirstOrDefault(o => o.Metodo == r.Metodo && Normalizar(o.Caminho) == Normalizar(r.Caminho))?.OperationId)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Os schemas (resolvidos) das respostas 2xx em JSON da operação.</summary>
    public static IEnumerable<JsonNode> EsquemasDeRespostaDeSucesso(string operationId)
    {
        var respostas = Operacao(operationId)["responses"] as JsonObject ?? [];
        foreach (var (_, resposta) in respostas.Where(r => r.Key.StartsWith('2')))
        {
            var resolvida = ResolvedorDeEsquema.Resolver(resposta!, CarregadorDeContrato.Contrato());
            foreach (var (tipoDeMidia, conteudo) in resolvida["content"] as JsonObject ?? [])
            {
                if (tipoDeMidia.Contains("json", StringComparison.Ordinal) && conteudo?["schema"] is { } schema)
                {
                    yield return schema;
                }
            }
        }
    }

    /// <summary>A resposta que o contrato declara para o status (ou <c>default</c>), com os <c>$ref</c> resolvidos; <c>null</c> se não declara.</summary>
    public static JsonObject? RespostaDeclarada(string operationId, int status)
    {
        var respostas = Operacao(operationId)["responses"] as JsonObject;
        var declarada = respostas?[status.ToString(System.Globalization.CultureInfo.InvariantCulture)] ?? respostas?["default"];
        return declarada is null ? null : (JsonObject)ResolvedorDeEsquema.Resolver(declarada, CarregadorDeContrato.Contrato());
    }

    private static JsonObject Operacao(string operationId)
    {
        var operacao = Operacoes.FirstOrDefault(o => o.OperationId == operationId)
            ?? throw new InvalidOperationException($"O contrato não tem a operação {operationId}.");
        return CarregadorDeContrato.Contrato()["paths"]![operacao.Caminho]![operacao.Metodo.ToLowerInvariant()]!.AsObject();
    }

    private static string Normalizar(string caminho) => NomeDeParametro().Replace(caminho, "{}");

    private static List<OperacaoDoContrato> LerOperacoes()
    {
        var lidas = new List<OperacaoDoContrato>();
        foreach (var (caminho, item) in CarregadorDeContrato.Contrato()["paths"]!.AsObject())
        {
            foreach (var metodo in Metodos)
            {
                if (item?[metodo] is not JsonObject operacao)
                {
                    continue;
                }

                var id = operacao["operationId"]?.ToString() ?? throw new InvalidOperationException($"{metodo.ToUpperInvariant()} {caminho} não tem operationId no contrato.");
                var perfis = (operacao["x-perfis"] as JsonArray)?.Select(p => p!.ToString()).ToList() ?? [];
                lidas.Add(new OperacaoDoContrato(metodo.ToUpperInvariant(), caminho, id, perfis));
            }
        }

        return lidas;
    }

    [GeneratedRegex(@"\{([^}:]+)(?::[^}]*)?\}")]
    private static partial Regex RestricaoDeRota();

    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex NomeDeParametro();
}
