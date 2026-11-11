using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Ponto;

namespace RHSystem.Api.Ponto;

/// <summary>
/// O ponto para o RH (<c>/api/rh</c>, só o perfil RH). Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/30-fatia-3-ponto.yaml</c>. Registrar por <c>operationId</c>, nunca por prefixo: sob
/// <c>/api/rh/funcionarios/{funcionarioId}/</c> o contrato também tem rotas de outras fatias.
/// </summary>
public static class PontoRhEndpoints
{
    private const string Etiqueta = "Ponto";

    public static IEndpointRouteBuilder MapPontoRh(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoRh().WithTags(Etiqueta);

        grupo.MapGet("/funcionarios/{funcionarioId:int}/ponto", ObterEspelho)
            .WithName("obterEspelhoDePontoDeFuncionario")
            .Produces<PontoEspelho>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<PontoEspelho> ObterEspelho(int funcionarioId, [AsParameters] FiltrosDoEspelho filtros, ObterEspelhoParaRhCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(funcionarioId, filtros.Ano, filtros.Mes, ct);
}
