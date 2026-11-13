using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Ferias;

namespace RHSystem.Api.Ferias;

/// <summary>Os parâmetros da lista de férias do RH e SÓ eles (<c>Situacao</c> chega como texto: o caso de uso o lê pelo nome do contrato e responde 400 com o erro por campo).</summary>
public sealed record FiltrosDaListaDeFeriasDoRh(int Pagina = 1, int TamanhoPagina = 20, string? Busca = null, string? Situacao = null, int? SetorId = null);

/// <summary>
/// As férias de TODA a empresa para o RH (<c>/api/rh/ferias</c>, só o perfil RH): a fila e as duas decisões da etapa dele (confirmar e recusar). Os códigos de resposta são os de <c>contrato/fontes/70-fatia-7-gestao.yaml</c>. As rotas com <c>{feriasId}</c> usam <c>:int</c>.
/// </summary>
public static class FeriasRhEndpoints
{
    private const string Etiqueta = "Férias";

    public static IServiceCollection AdicionarCasosDeUsoDasFeriasParaORh(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarFeriasParaORhCasoDeUso>();
        servicos.AddScoped<DecidirFeriasComoRhCasoDeUso>();
        servicos.AddScoped<ObterFeriasDaFichaDoFuncionarioCasoDeUso>();
        servicos.AddScoped<ConferirRegistroDeFeriasCasoDeUso>();
        servicos.AddScoped<RegistrarFeriasPeloRhCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapFeriasRh(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoRh().WithTags(Etiqueta);

        grupo.MapGet("/ferias", Listar)
            .WithName("listarFeriasParaORh")
            .Produces<RespostaPaginada<FeriasPedidoAnalise>>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/ferias/{feriasId:int}/confirmar", Confirmar)
            .WithName("confirmarFeriasComoRh")
            .Produces<FeriasPedidoAnalise>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/ferias/{feriasId:int}/recusar", Recusar)
            .WithName("recusarFeriasComoRh")
            .Produces<FeriasPedidoAnalise>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        // O prefixo /api/rh/funcionarios/{funcionarioId}/ é de várias fatias: as rotas são registradas com o literal "ferias" e o :int.
        grupo.MapGet("/funcionarios/{funcionarioId:int}/ferias", ObterDaFicha)
            .WithName("obterFeriasDaFichaDoFuncionario")
            .Produces<FeriasFicha>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/funcionarios/{funcionarioId:int}/ferias", Registrar)
            .WithName("registrarFeriasPeloRh")
            .Produces<FeriasPedido>(StatusCodes.Status201Created)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/funcionarios/{funcionarioId:int}/ferias/conferencia", ConferirRegistro)
            .WithName("conferirRegistroDeFerias")
            .Produces<FeriasConferencia>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<FeriasFicha> ObterDaFicha(int funcionarioId, ObterFeriasDaFichaDoFuncionarioCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(funcionarioId, ct);

    private static async Task<IResult> Registrar(int funcionarioId, FeriasRegistrarComando corpo, RegistrarFeriasPeloRhCasoDeUso casoDeUso, CancellationToken ct)
    {
        var pedido = await casoDeUso.ExecutarAsync(funcionarioId, corpo, ct);
        return Results.Created($"/api/rh/funcionarios/{funcionarioId}/ferias", pedido);
    }

    private static async Task<FeriasConferencia> ConferirRegistro(int funcionarioId, FeriasConferenciaComando corpo, ConferirRegistroDeFeriasCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(funcionarioId, corpo, ct);

    private static async Task<RespostaPaginada<FeriasPedidoAnalise>> Listar([AsParameters] FiltrosDaListaDeFeriasDoRh f, ListarFeriasParaORhCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, f.Busca, f.Situacao, f.SetorId, ct);

    private static async Task<FeriasPedidoAnalise> Confirmar(int feriasId, DecidirFeriasComoRhCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ConfirmarAsync(feriasId, ct);

    private static async Task<FeriasPedidoAnalise> Recusar(int feriasId, FeriasRecusarComando corpo, DecidirFeriasComoRhCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.RecusarAsync(feriasId, corpo, ct);
}
