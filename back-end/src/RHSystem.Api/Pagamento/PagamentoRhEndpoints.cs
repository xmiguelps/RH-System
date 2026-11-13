using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Pagamento;

namespace RHSystem.Api.Pagamento;

/// <summary>
/// O pagamento para o RH (<c>/api/rh/pagamento</c>, só o perfil RH): a lista de exceções, o calendário do ano e o cadastro (PUT) e a remoção (DELETE) da exceção de um mês. Os códigos de resposta são os de <c>contrato/fontes/70-fatia-7-gestao.yaml</c>. As rotas com <c>{ano}/{mes}</c> usam <c>:int</c>; o contrato do DELETE
/// não tem 400, então ano ou mês fora da faixa lá é 404.
/// </summary>
public static class PagamentoRhEndpoints
{
    private const string Etiqueta = "Pagamento";

    public static IServiceCollection AdicionarCasosDeUsoDoPagamentoParaORh(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarExcecoesDePagamentoCasoDeUso>();
        servicos.AddScoped<ObterCalendarioDePagamentoCasoDeUso>();
        servicos.AddScoped<DefinirExcecaoDePagamentoCasoDeUso>();
        servicos.AddScoped<RemoverExcecaoDePagamentoCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapPagamentoRh(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoRh().WithTags(Etiqueta);

        grupo.MapGet("/pagamento/excecoes", Listar)
            .WithName("listarExcecoesDePagamento")
            .Produces<PagamentoExcecaoListaDto>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapGet("/pagamento/calendario", Calendario)
            .WithName("obterCalendarioDePagamento")
            .Produces<PagamentoCalendarioDto>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapPut("/pagamento/excecoes/{ano:int}/{mes:int}", Definir)
            .WithName("definirExcecaoDePagamento")
            .Produces<PagamentoExcecaoDto>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapDelete("/pagamento/excecoes/{ano:int}/{mes:int}", Remover)
            .WithName("removerExcecaoDePagamento")
            .Produces(StatusCodes.Status204NoContent)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<PagamentoExcecaoListaDto> Listar(int? ano, ListarExcecoesDePagamentoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(ano, ct);

    private static async Task<PagamentoCalendarioDto> Calendario(int? ano, ObterCalendarioDePagamentoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(ano, ct);

    private static async Task<PagamentoExcecaoDto> Definir(int ano, int mes, PagamentoExcecaoComando corpo, DefinirExcecaoDePagamentoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(ano, mes, corpo, ct);

    private static async Task<IResult> Remover(int ano, int mes, RemoverExcecaoDePagamentoCasoDeUso casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(ano, mes, ct);
        return Results.NoContent();
    }
}
