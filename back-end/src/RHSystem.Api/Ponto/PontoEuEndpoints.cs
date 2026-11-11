using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Ponto;

namespace RHSystem.Api.Ponto;

/// <summary>O mês do espelho (<c>ano</c> e <c>mes</c>): inteiros opcionais; o caso de uso confere a faixa (2000 a 2100 e 1 a 12) e devolve o 400 com o nome do campo.</summary>
public sealed record FiltrosDoEspelho(int? Ano = null, int? Mes = null);

/// <summary>
/// O ponto da PRÓPRIA pessoa logada (<c>/api/eu/ponto</c>, qualquer perfil): o cartão "Seu ponto hoje" e a batida. Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/30-fatia-3-ponto.yaml</c>. Registrar por
/// <c>operationId</c>, nunca por prefixo.
/// </summary>
public static class PontoEuEndpoints
{
    private const string Etiqueta = "Ponto";

    public static IServiceCollection AdicionarCasosDeUsoDePontoEu(this IServiceCollection servicos)
    {
        servicos.AddScoped<MontadorDoCartaoDeHoje>();
        servicos.AddScoped<ObterPontoHojeCasoDeUso>();
        servicos.AddScoped<RegistrarMarcacaoCasoDeUso>();
        servicos.AddScoped<MontadorDoEspelho>();
        servicos.AddScoped<MontadorDoPontoDaEquipe>();
        servicos.AddScoped<ObterMeuEspelhoCasoDeUso>();
        servicos.AddScoped<ObterEspelhoDaEquipeCasoDeUso>();
        servicos.AddScoped<ObterEspelhoParaRhCasoDeUso>();
        servicos.AddScoped<ObterPontoDaEquipeCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapPontoEu(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoEu().WithTags(Etiqueta);

        grupo.MapGet("/ponto/hoje", ObterHoje)
            .WithName("obterPontoHoje")
            .Produces<PontoHoje>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/ponto/marcacoes", Registrar)
            .WithName("registrarMarcacaoDePonto")
            .Produces<PontoHoje>(StatusCodes.Status201Created)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status409Conflict,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        grupo.MapGet("/ponto/espelho", ObterEspelho)
            .WithName("obterMeuEspelhoDePonto")
            .Produces<PontoEspelhoProprio>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<PontoEspelhoProprio> ObterEspelho([AsParameters] FiltrosDoEspelho filtros, ObterMeuEspelhoCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(filtros.Ano, filtros.Mes, ct);

    private static async Task<PontoHoje> ObterHoje(ObterPontoHojeCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(ct);

    private static async Task<IResult> Registrar(PontoMarcacaoRegistrar corpo, RegistrarMarcacaoCasoDeUso casoDeUso, CancellationToken ct) =>
        Results.Json(await casoDeUso.ExecutarAsync(corpo, ct), statusCode: StatusCodes.Status201Created);
}
