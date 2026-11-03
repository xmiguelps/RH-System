namespace RHSystem.Api.Autorizacao;

/// <summary>
/// As quatro famílias de rota do contrato (<c>CONVENCOES.md</c>, seção 2). Toda rota nova de negócio nasce em um destes grupos, que já trazem a
/// política do perfil; <c>/api/auth/*</c> e <c>/health</c> ficam por rota (<c>AllowAnonymous</c> nas públicas).
/// </summary>
public static class FamiliasDeRota
{
    /// <summary><c>/api/rh</c>: só o perfil RH.</summary>
    public static RouteGroupBuilder GrupoRh(this IEndpointRouteBuilder app) => app.MapGroup("/api/rh").RequireAuthorization(Politicas.Rh);

    /// <summary><c>/api/gestor</c>: só o perfil Gestor (sempre sobre a equipe dele).</summary>
    public static RouteGroupBuilder GrupoGestor(this IEndpointRouteBuilder app) => app.MapGroup("/api/gestor").RequireAuthorization(Politicas.Gestor);

    /// <summary><c>/api/eu</c>: os dados da própria pessoa logada, para qualquer perfil.</summary>
    public static RouteGroupBuilder GrupoEu(this IEndpointRouteBuilder app) => app.MapGroup("/api/eu").RequireAuthorization(Politicas.Eu);

    /// <summary><c>/api/publico</c>: sem login.</summary>
    public static RouteGroupBuilder GrupoPublico(this IEndpointRouteBuilder app) => app.MapGroup("/api/publico").AllowAnonymous();
}
