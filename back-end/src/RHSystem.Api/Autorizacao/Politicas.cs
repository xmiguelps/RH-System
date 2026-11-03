using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Api.Autorizacao;

/// <summary>Os nomes das políticas por perfil. Quem decide é o claim <c>perfil</c> do token (texto: <c>RH</c>, <c>Gestor</c> ou <c>Funcionario</c>).</summary>
public static class Politicas
{
    /// <summary>Só o perfil RH.</summary>
    public const string Rh = "Rh";

    /// <summary>Só o perfil Gestor.</summary>
    public const string Gestor = "Gestor";

    /// <summary>Qualquer um dos três perfis (os dados da própria pessoa logada).</summary>
    public const string Eu = "Eu";
}

public static class AutorizacaoExtensions
{
    private const string ClaimDoPerfil = "perfil";

    /// <summary>
    /// As políticas por perfil e a regra de reserva (<c>FallbackPolicy</c>): rota sem política e sem <c>AllowAnonymous</c> exige usuário
    /// autenticado, então uma rota nova que esqueça a família fica fechada, e não aberta. O 403 sai como <c>ProblemDetails</c> do contrato
    /// (<see cref="ManipuladorDeAutorizacao"/>). Registra também o <see cref="IAtorAtual"/>.
    /// </summary>
    public static IServiceCollection AdicionarAutorizacaoPorPerfil(this IServiceCollection servicos)
    {
        servicos.AddAuthorization(opcoes =>
        {
            // O valor do claim é comparado como texto exato: "rh" minúsculo, um número ou um perfil que não existe não passam.
            opcoes.AddPolicy(Politicas.Rh, p => p.RequireAuthenticatedUser().RequireClaim(ClaimDoPerfil, nameof(Perfil.RH)));
            opcoes.AddPolicy(Politicas.Gestor, p => p.RequireAuthenticatedUser().RequireClaim(ClaimDoPerfil, nameof(Perfil.Gestor)));
            opcoes.AddPolicy(
                Politicas.Eu,
                p => p.RequireAuthenticatedUser().RequireClaim(ClaimDoPerfil, nameof(Perfil.Funcionario), nameof(Perfil.Gestor), nameof(Perfil.RH)));
            opcoes.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAssertion(ExigeLoginSoEmRotaReal).Build();
        });

        servicos.Replace(ServiceDescriptor.Singleton<IAuthorizationMiddlewareResultHandler, ManipuladorDeAutorizacao>());
        servicos.AddHttpContextAccessor();
        servicos.AddScoped<IAtorAtual, AtorAtualHttp>();
        return servicos;
    }

    /// <summary>
    /// A regra de reserva: toda ROTA (um <see cref="RouteEndpoint"/>) sem política e sem <c>AllowAnonymous</c> exige login. O que não é rota fica de
    /// fora: a requisição que não casou com nada e as respostas de rejeição do próprio roteamento (404, 405 de método errado, 415 de tipo de mídia
    /// errado) não têm o que proteger e não carregam metadados. Medido: com <c>RequireAuthenticatedUser</c> puro, o anônimo recebia 401 nelas e
    /// os testes da fatia 0 (405, 415) e o 404 do contrato quebravam.
    /// </summary>
    private static bool ExigeLoginSoEmRotaReal(AuthorizationHandlerContext contexto) =>
        contexto.User.Identity?.IsAuthenticated == true
        || (contexto.Resource is HttpContext http && http.GetEndpoint() is not RouteEndpoint);
}
