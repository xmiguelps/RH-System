using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace RHSystem.Api.Configuracao;

public static class LimiteDeTentativasExtensions
{
    public const string PoliticaDeLogin = "login";

    /// <summary>As três rotas públicas de senha (<c>esqueci</c>, <c>validar-link</c>, <c>definir</c>): o limite vale por IP E por rota (cada uma tem o balde dela).</summary>
    public const string PoliticaDeSenha = "senha";

    /// <summary>
    /// Limite de tentativas de login: <c>Autenticacao:LimiteLoginPorMinuto</c> (padrão 5) por IP, em janela de 1 minuto. Acima disso a
    /// resposta é 429 com o <c>ProblemDetails</c> do contrato e o cabeçalho <c>Retry-After</c> (o limitador do ASP.NET rejeita, por padrão,
    /// com 503 e sem corpo). O IP vem de <c>RemoteIpAddress</c>, que o <c>UseForwardedHeaders</c> ajusta quando o proxy reverso está habilitado.
    /// </summary>
    public static IServiceCollection AdicionarLimiteDeTentativas(this IServiceCollection servicos, IConfiguration configuracao)
    {
        var limite = configuracao.GetValue<int?>("Autenticacao:LimiteLoginPorMinuto") ?? 5;
        var limiteDeSenha = configuracao.GetValue<int?>("Autenticacao:LimiteSenhaPorMinuto") ?? 5;

        servicos.AddRateLimiter(opcoes =>
        {
            opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opcoes.AddPolicy(PoliticaDeLogin, contexto =>
                RateLimitPartition.GetFixedWindowLimiter(
                    contexto.Connection.RemoteIpAddress?.ToString() ?? "origem-desconhecida",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limite,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

            opcoes.AddPolicy(PoliticaDeSenha, contexto =>
                RateLimitPartition.GetFixedWindowLimiter(
                    $"{contexto.Connection.RemoteIpAddress?.ToString() ?? "origem-desconhecida"}|{contexto.Request.Path}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limiteDeSenha,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

            opcoes.OnRejected = async (contexto, ct) =>
            {
                var espera = contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                    : 60;
                contexto.HttpContext.Response.Headers.RetryAfter = espera.ToString(CultureInfo.InvariantCulture);
                await Problemas.EscreverAsync(contexto.HttpContext, StatusCodes.Status429TooManyRequests, "Muitas tentativas.", "Muitas tentativas. Aguarde um minuto e tente de novo.");
            };
        });

        return servicos;
    }
}
