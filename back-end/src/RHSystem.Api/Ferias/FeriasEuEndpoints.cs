using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Ferias;

namespace RHSystem.Api.Ferias;

/// <summary>
/// As férias da PRÓPRIA pessoa logada (<c>/api/eu/ferias</c>, qualquer perfil): o saldo e o histórico, a conferência das regras sem gravar e o pedido. A pessoa nunca decide: o pedido de outra pessoa é 404. As rotas de <c>/api/eu/ferias/{feriasId}/sugestao/...</c> entram com a resposta à sugestão.
/// </summary>
public static class FeriasEuEndpoints
{
    private const string Etiqueta = "Férias";

    public static IServiceCollection AdicionarCasosDeUsoDasMinhasFerias(this IServiceCollection servicos)
    {
        servicos.AddScoped<ConferidorDeFerias>();
        servicos.AddScoped<ObterMinhasFeriasCasoDeUso>();
        servicos.AddScoped<ConferirPedidoDeFeriasCasoDeUso>();
        servicos.AddScoped<PedirFeriasCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapFeriasEu(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoEu().WithTags(Etiqueta);

        grupo.MapGet("/ferias", Obter)
            .WithName("obterMinhasFerias")
            .Produces<FeriasEuResposta>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/ferias", Pedir)
            .WithName("pedirFerias")
            .Produces<FeriasPedido>(StatusCodes.Status201Created)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/ferias/conferencia", Conferir)
            .WithName("conferirPedidoDeFerias")
            .Produces<FeriasConferencia>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<FeriasEuResposta> Obter(ObterMinhasFeriasCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(ct);

    private static async Task<IResult> Pedir(FeriasPedirComando corpo, PedirFeriasCasoDeUso casoDeUso, CancellationToken ct)
    {
        var pedido = await casoDeUso.ExecutarAsync(corpo, ct);
        return Results.Created("/api/eu/ferias", pedido);
    }

    private static async Task<FeriasConferencia> Conferir(FeriasConferenciaComando corpo, ConferirPedidoDeFeriasCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(corpo, ct);
}
