using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Ponto;

namespace RHSystem.Api.Ponto;

/// <summary>O dia consultado do ponto da equipe (<c>data</c>): opcional, padrão hoje; futuro é 400.</summary>
public sealed record FiltroDoPontoDaEquipe(DateOnly? Data = null);

/// <summary>
/// O ponto da equipe do gestor (<c>/api/gestor</c>, só o perfil Gestor, sempre sobre a equipe dele). O que está fora da equipe responde 404. Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/30-fatia-3-ponto.yaml</c>.
/// Registrar por <c>operationId</c>, nunca por prefixo: sob <c>/api/gestor/equipe/{funcionarioId}/</c> o contrato também tem rotas de férias, treinamentos e advertências, que são de outras fatias.
/// </summary>
public static class PontoGestorEndpoints
{
    private const string Etiqueta = "Ponto";

    public static IEndpointRouteBuilder MapPontoGestor(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoGestor().WithTags(Etiqueta);

        grupo.MapGet("/ponto/equipe", ObterPontoDaEquipe)
            .WithName("obterPontoDaEquipe")
            .Produces<PontoEquipeDia>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapGet("/equipe/{funcionarioId:int}/ponto", ObterEspelho)
            .WithName("obterEspelhoDePontoDaPessoaDaEquipe")
            .Produces<PontoEspelho>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<PontoEquipeDia> ObterPontoDaEquipe([AsParameters] FiltroDoPontoDaEquipe filtro, ObterPontoDaEquipeCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(filtro.Data, ct);

    private static async Task<PontoEspelho> ObterEspelho(int funcionarioId, [AsParameters] FiltrosDoEspelho filtros, ObterEspelhoDaEquipeCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(funcionarioId, filtros.Ano, filtros.Mes, ct);
}
