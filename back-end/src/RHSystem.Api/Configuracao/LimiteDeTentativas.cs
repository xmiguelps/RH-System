using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace RHSystem.Api.Configuracao;

public static class LimiteDeTentativasExtensions
{
    public const string PoliticaDeLogin = "login";

    /// <summary>As três rotas públicas de senha (<c>esqueci</c>, <c>validar-link</c>, <c>definir</c>): o limite vale por IP E por rota (cada uma tem o balde dela).</summary>
    public const string PoliticaDeSenha = "senha";

    /// <summary>A página pública do formulário do candidato: <c>Formularios:LimiteConsultaPorMinuto</c> (padrão 30) por IP, em janela de 1 minuto (a segunda barreira contra quem varre códigos de link).</summary>
    public const string PoliticaDeFormularioConsulta = "formulario-consulta";

    /// <summary>A conferência pública do certificado: <c>Certificados:LimiteConferenciaPorMinuto</c> (padrão 10) por IP, em janela de 1 minuto. Roda antes de tudo (inclusive do 404): é o que impede varrer códigos.</summary>
    public const string PoliticaDeCertificadoConferencia = "certificado-conferencia";

    /// <summary>O envio da candidatura: <c>Formularios:LimiteEnvioPorHora</c> (padrão 10) por IP, em janela de 1 hora (a única proteção do link público contra currículo em massa; sem captcha, por decisão provisória do contrato).</summary>
    public const string PoliticaDeFormularioEnvio = "formulario-envio";

    /// <summary>
    /// Limite de tentativas de login: <c>Autenticacao:LimiteLoginPorMinuto</c> (padrão 5) por IP, em janela de 1 minuto. Acima disso a
    /// resposta é 429 com o <c>ProblemDetails</c> do contrato e o cabeçalho <c>Retry-After</c> (o limitador do ASP.NET rejeita, por padrão,
    /// com 503 e sem corpo). O IP vem de <c>RemoteIpAddress</c>, que o <c>UseForwardedHeaders</c> ajusta quando o proxy reverso está habilitado.
    /// </summary>
    public static IServiceCollection AdicionarLimiteDeTentativas(this IServiceCollection servicos, IConfiguration configuracao)
    {
        var limite = configuracao.GetValue<int?>("Autenticacao:LimiteLoginPorMinuto") ?? 5;
        var limiteDeSenha = configuracao.GetValue<int?>("Autenticacao:LimiteSenhaPorMinuto") ?? 5;
        var limiteDeConsulta = configuracao.GetValue<int?>("Formularios:LimiteConsultaPorMinuto") ?? 30;
        var limiteDeEnvio = configuracao.GetValue<int?>("Formularios:LimiteEnvioPorHora") ?? 10;
        var limiteDeConferencia = configuracao.GetValue<int?>("Certificados:LimiteConferenciaPorMinuto") ?? 10;

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

            opcoes.AddPolicy(PoliticaDeFormularioConsulta, contexto =>
                RateLimitPartition.GetFixedWindowLimiter(
                    contexto.Connection.RemoteIpAddress?.ToString() ?? "origem-desconhecida",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limiteDeConsulta,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

            opcoes.AddPolicy(PoliticaDeCertificadoConferencia, contexto =>
                RateLimitPartition.GetFixedWindowLimiter(
                    contexto.Connection.RemoteIpAddress?.ToString() ?? "origem-desconhecida",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limiteDeConferencia,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

            opcoes.AddPolicy(PoliticaDeFormularioEnvio, contexto =>
                RateLimitPartition.GetFixedWindowLimiter(
                    contexto.Connection.RemoteIpAddress?.ToString() ?? "origem-desconhecida",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limiteDeEnvio,
                        Window = TimeSpan.FromHours(1),
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
