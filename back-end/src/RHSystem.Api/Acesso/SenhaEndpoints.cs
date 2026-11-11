using System.Security.Claims;
using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Acesso;

namespace RHSystem.Api.Acesso;

/// <summary>
/// As quatro rotas de senha (<c>contrato/fontes/20-fatia-1-colaboradores.yaml</c>, "Senha e convite"): três públicas em <c>/api/auth/senha</c> e a troca de quem está logado em <c>/api/eu/senha</c>.
/// Os <c>operationId</c> e os códigos de resposta são os do contrato. As públicas respondem sempre sem revelar se o e-mail ou o link existe; o limite delas é por IP e por rota.
/// </summary>
public static class SenhaEndpoints
{
    private const string Etiqueta = "Senha e convite";

    public static IServiceCollection AdicionarCasosDeUsoDeAcesso(this IServiceCollection servicos)
    {
        servicos.AddScoped<SolicitarRedefinicaoDeSenhaCasoDeUso>();
        servicos.AddScoped<ValidarLinkDeSenhaCasoDeUso>();
        servicos.AddScoped<DefinirSenhaPeloLinkCasoDeUso>();
        servicos.AddScoped<TrocarMinhaSenhaCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapSenha(this IEndpointRouteBuilder rotas)
    {
        var publicas = rotas.MapGroup("/api/auth/senha").WithTags(Etiqueta);

        publicas.MapPost("/esqueci", Esqueci)
            .WithName("solicitarRedefinicaoDeSenha")
            .AllowAnonymous()
            .RequireRateLimiting(LimiteDeTentativasExtensions.PoliticaDeSenha)
            .Produces(StatusCodes.Status202Accepted)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status429TooManyRequests, StatusCodes.Status500InternalServerError);

        publicas.MapPost("/validar-link", ValidarLink)
            .WithName("validarLinkDeSenha")
            .AllowAnonymous()
            .RequireRateLimiting(LimiteDeTentativasExtensions.PoliticaDeSenha)
            .Produces<ConviteLinkInfo>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status429TooManyRequests, StatusCodes.Status500InternalServerError);

        publicas.MapPost("/definir", Definir)
            .WithName("definirSenhaPeloLink")
            .AllowAnonymous()
            .RequireRateLimiting(LimiteDeTentativasExtensions.PoliticaDeSenha)
            .Produces(StatusCodes.Status204NoContent)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status429TooManyRequests, StatusCodes.Status500InternalServerError);

        // A troca NÃO passa pelo limitador por IP: o limite dela é por pessoa e só conta os erros de senha atual (ILimiteDeSenhaAtual).
        rotas.GrupoEu().WithTags(Etiqueta).MapPost("/senha", Trocar)
            .WithName("trocarMinhaSenha")
            .Produces(StatusCodes.Status204NoContent)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status429TooManyRequests,
                StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<IResult> Esqueci(ConviteEsqueciSenhaComando comando, SolicitarRedefinicaoDeSenhaCasoDeUso casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(comando, ct);
        return Results.Accepted(); // sempre a mesma resposta, exista o e-mail ou não
    }

    private static async Task<IResult> ValidarLink(ConviteLinkValidarComando comando, ValidarLinkDeSenhaCasoDeUso casoDeUso, CancellationToken ct) =>
        Results.Ok(await casoDeUso.ExecutarAsync(comando, ct));

    private static async Task<IResult> Definir(ConviteDefinirSenhaComando comando, DefinirSenhaPeloLinkCasoDeUso casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(comando, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Trocar(SenhaTrocarComando comando, ClaimsPrincipal pessoa, TrocarMinhaSenhaCasoDeUso casoDeUso, CancellationToken ct)
    {
        // A claim rth é o hash do refresh token da sessão atual: é o que a troca mantém. Token emitido antes dela não tem a claim e a troca encerra todas as sessões.
        await casoDeUso.ExecutarAsync(comando, pessoa.FindFirstValue("rth"), ct);
        return Results.NoContent();
    }
}
