using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Ponto;

namespace RHSystem.Api.Ponto;

/// <summary>
/// Os parâmetros das listas de pedidos de ajuste da PRÓPRIA pessoa e SÓ eles (sem <c>busca</c> nem <c>funcionarioId</c>, que o contrato não declara aqui). <c>Status</c>, <c>Etapa</c>, <c>OrdenarPor</c> e <c>Direcao</c> chegam como texto: o caso de uso os lê
/// pelo nome do contrato (<c>PENDENTE</c>, <c>GESTOR</c>, <c>DIA_DO_PONTO</c>, <c>DESC</c>) e devolve os erros por campo num 400.
/// </summary>
public sealed record FiltrosDaListaDeMeusPedidos(int Pagina = 1, int TamanhoPagina = 20, string? Status = null, string? Etapa = null, string? OrdenarPor = null, string? Direcao = null);

/// <summary>Os pedidos de ajuste de ponto da PRÓPRIA pessoa logada (<c>/api/eu/ponto/pedidos-de-ajuste</c>, qualquer perfil): lista, criar e ver um. O pedido de outra pessoa é 404.</summary>
public static class PontoPedidosEuEndpoints
{
    private const string Etiqueta = "Pedidos de ajuste de ponto";

    public static IServiceCollection AdicionarCasosDeUsoDePedidosProprios(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarMeusPedidosCasoDeUso>();
        servicos.AddScoped<ObterMeuPedidoCasoDeUso>();
        servicos.AddScoped<CriarPedidoDeAjusteCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapPontoPedidosEu(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoEu().WithTags(Etiqueta);

        grupo.MapGet("/ponto/pedidos-de-ajuste", Listar)
            .WithName("listarMeusPedidosDeAjuste")
            .Produces<RespostaPaginada<PontoPedidoAjuste>>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/ponto/pedidos-de-ajuste", Criar)
            .WithName("criarPedidoDeAjuste")
            .Produces<PontoPedidoAjuste>(StatusCodes.Status201Created)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status409Conflict,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        grupo.MapGet("/ponto/pedidos-de-ajuste/{pedidoId:int}", Obter)
            .WithName("obterMeuPedidoDeAjuste")
            .Produces<PontoPedidoAjuste>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<RespostaPaginada<PontoPedidoAjuste>> Listar([AsParameters] FiltrosDaListaDeMeusPedidos f, ListarMeusPedidosCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, f.Status, f.Etapa, f.OrdenarPor, f.Direcao, ct);

    private static async Task<IResult> Criar(PontoPedidoAjusteCriar corpo, CriarPedidoDeAjusteCasoDeUso casoDeUso, CancellationToken ct)
    {
        var pedido = await casoDeUso.ExecutarAsync(corpo, ct);
        return Results.Created($"/api/eu/ponto/pedidos-de-ajuste/{pedido.Id}", pedido);
    }

    private static async Task<PontoPedidoAjuste> Obter(int pedidoId, ObterMeuPedidoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(pedidoId, ct);
}
