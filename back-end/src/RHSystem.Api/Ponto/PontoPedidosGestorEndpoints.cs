using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Ponto;

namespace RHSystem.Api.Ponto;

/// <summary>Os parâmetros da fila de pedidos de ajuste do gestor e do RH e SÓ eles (com <c>funcionarioId</c>, sem <c>busca</c>). Os de enum chegam como texto e o caso de uso os lê pelo nome do contrato.</summary>
public sealed record FiltrosDaFilaDePedidos(int Pagina = 1, int TamanhoPagina = 20, string? Status = null, string? Etapa = null, int? FuncionarioId = null, string? OrdenarPor = null, string? Direcao = null);

/// <summary>
/// Os pedidos de ajuste de ponto da EQUIPE do gestor (<c>/api/gestor/ponto/pedidos-de-ajuste</c>, só o perfil Gestor): fila, ver um e decidir a etapa dele. O pedido do próprio gestor, de outro gestor e de outro setor é 404. Os <c>operationId</c> e os
/// códigos de resposta são os de <c>contrato/fontes/30-fatia-3-ponto.yaml</c>.
/// </summary>
public static class PontoPedidosGestorEndpoints
{
    private const string Etiqueta = "Pedidos de ajuste de ponto";

    public static IServiceCollection AdicionarCasosDeUsoDePedidosDaEquipe(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarPedidosDaEquipeCasoDeUso>();
        servicos.AddScoped<ObterPedidoDaEquipeCasoDeUso>();
        servicos.AddScoped<DecidirComoGestorCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapPontoPedidosGestor(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoGestor().WithTags(Etiqueta);

        grupo.MapGet("/ponto/pedidos-de-ajuste", Listar)
            .WithName("listarPedidosDeAjusteDaEquipe")
            .Produces<PontoPedidoAjusteFilaPagina>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapGet("/ponto/pedidos-de-ajuste/{pedidoId:int}", Obter)
            .WithName("obterPedidoDeAjusteDaEquipe")
            .Produces<PontoPedidoAjuste>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/ponto/pedidos-de-ajuste/{pedidoId:int}/aprovar", Aprovar)
            .WithName("aprovarPedidoDeAjusteComoGestor")
            .Produces<PontoPedidoAjuste>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        grupo.MapPost("/ponto/pedidos-de-ajuste/{pedidoId:int}/recusar", Recusar)
            .WithName("recusarPedidoDeAjusteComoGestor")
            .Produces<PontoPedidoAjuste>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<PontoPedidoAjusteFilaPagina> Listar([AsParameters] FiltrosDaFilaDePedidos f, ListarPedidosDaEquipeCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, f.Status, f.Etapa, f.FuncionarioId, f.OrdenarPor, f.Direcao, ct);

    private static async Task<PontoPedidoAjuste> Obter(int pedidoId, ObterPedidoDaEquipeCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(pedidoId, ct);

    private static async Task<PontoPedidoAjuste> Aprovar(int pedidoId, PontoPedidoDecisao corpo, DecidirComoGestorCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.AprovarAsync(pedidoId, corpo, ct);

    private static async Task<PontoPedidoAjuste> Recusar(int pedidoId, PontoPedidoDecisao corpo, DecidirComoGestorCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.RecusarAsync(pedidoId, corpo, ct);
}
