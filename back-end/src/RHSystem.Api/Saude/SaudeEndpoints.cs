using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RHSystem.Api.Saude;

/// <summary>O corpo do 200 de <c>GET /health</c> (<c>SaudeResposta</c> do contrato).</summary>
public sealed record SaudeResposta(string Status);

public static class SaudeEndpoints
{
    public static IServiceCollection AdicionarSaude(this IServiceCollection servicos)
    {
        servicos.AddHealthChecks().AddCheck<BancoHealthCheck>("banco");
        return servicos;
    }

    /// <summary>
    /// <c>GET /health</c> (<c>verificarSaude</c>): 200 <c>{"status":"ok"}</c> com o banco acessível, 503 sem corpo se ele cair (o contrato
    /// não define o corpo do 503). É um <c>MapGet</c> e não o <c>MapHealthChecks</c>, que não entra no documento OpenAPI. Sem login.
    /// </summary>
    public static IEndpointRouteBuilder MapSaude(this IEndpointRouteBuilder rotas)
    {
        rotas.MapGet("/health", async (HealthCheckService saude, CancellationToken ct) =>
            {
                var relatorio = await saude.CheckHealthAsync(ct);
                return relatorio.Status == HealthStatus.Healthy
                    ? Results.Ok(new SaudeResposta("ok"))
                    : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            })
            .WithName("verificarSaude")
            .WithTags("Operação")
            .AllowAnonymous()
            .Produces<SaudeResposta>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        return rotas;
    }
}
