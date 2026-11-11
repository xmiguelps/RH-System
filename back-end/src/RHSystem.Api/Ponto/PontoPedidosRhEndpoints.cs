using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Ponto;

namespace RHSystem.Api.Ponto;

/// <summary>
/// Os pedidos de ajuste de ponto para o RH (<c>/api/rh/ponto/pedidos-de-ajuste</c>, só o perfil RH): a fila da empresa toda, ver um e a decisão FINAL (aprovar aplica as batidas, tudo ou nada). Os <c>operationId</c> e os códigos de resposta são os de
/// <c>contrato/fontes/30-fatia-3-ponto.yaml</c>.
/// </summary>
public static class PontoPedidosRhEndpoints
{
    private const string Etiqueta = "Pedidos de ajuste de ponto";

    public static IServiceCollection AdicionarCasosDeUsoDePedidosParaRh(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarPedidosParaRhCasoDeUso>();
        servicos.AddScoped<ObterPedidoParaRhCasoDeUso>();
        servicos.AddScoped<DecidirComoRhCasoDeUso>();
        servicos.AddScoped<CorrigirMarcacoesDoDiaCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapPontoPedidosRh(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoRh().WithTags(Etiqueta);

        grupo.MapGet("/ponto/pedidos-de-ajuste", Listar)
            .WithName("listarPedidosDeAjusteParaRh")
            .Produces<PontoPedidoAjusteFilaPagina>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapGet("/ponto/pedidos-de-ajuste/{pedidoId:int}", Obter)
            .WithName("obterPedidoDeAjusteParaRh")
            .Produces<PontoPedidoAjuste>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/ponto/pedidos-de-ajuste/{pedidoId:int}/aprovar", Aprovar)
            .WithName("aprovarPedidoDeAjusteComoRh")
            .Produces<PontoPedidoAjuste>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        grupo.MapPost("/ponto/pedidos-de-ajuste/{pedidoId:int}/recusar", Recusar)
            .WithName("recusarPedidoDeAjusteComoRh")
            .Produces<PontoPedidoAjuste>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<PontoPedidoAjusteFilaPagina> Listar([AsParameters] FiltrosDaFilaDePedidos f, ListarPedidosParaRhCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, f.Status, f.Etapa, f.FuncionarioId, f.OrdenarPor, f.Direcao, ct);

    private static async Task<PontoPedidoAjuste> Obter(int pedidoId, ObterPedidoParaRhCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(pedidoId, ct);

    private static async Task<PontoPedidoAjuste> Aprovar(int pedidoId, PontoPedidoDecisao corpo, DecidirComoRhCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.AprovarAsync(pedidoId, corpo, ct);

    private static async Task<PontoPedidoAjuste> Recusar(int pedidoId, PontoPedidoDecisao corpo, DecidirComoRhCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.RecusarAsync(pedidoId, corpo, ct);
}
