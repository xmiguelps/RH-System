using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Pagamento;

namespace RHSystem.Api.Pagamento;

/// <summary>O pagamento da PRÓPRIA pessoa logada (<c>/api/eu/pagamento</c>, qualquer perfil): o cartão "Próximo pagamento".</summary>
public static class PagamentoEuEndpoints
{
    private const string Etiqueta = "Pagamento";

    public static IServiceCollection AdicionarCasosDeUsoDoMeuPagamento(this IServiceCollection servicos)
    {
        servicos.AddScoped<ObterProximoPagamentoCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapPagamentoEu(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoEu().WithTags(Etiqueta);

        grupo.MapGet("/pagamento/proximo", Obter)
            .WithName("obterProximoPagamento")
            .Produces<PagamentoProximoDto>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<PagamentoProximoDto> Obter(ObterProximoPagamentoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(ct);
}
