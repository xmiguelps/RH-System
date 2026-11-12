using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Comunicacao.Aniversarios;

namespace RHSystem.Api.Comunicacao;

/// <summary>
/// Os aniversariantes (<c>/api/eu/aniversariantes</c>, qualquer perfil): a empresa inteira vê os aniversários de hoje até 14 dias à frente. Só a API: a tela é da fatia 7d. O <c>operationId</c> e os códigos de resposta são os de
/// <c>contrato/fontes/40-fatia-4-comunicacao-e-documentos.yaml</c>.
/// </summary>
public static class AniversariantesEndpoints
{
    public static IServiceCollection AdicionarCasosDeUsoDeAniversariantes(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarAniversariantesCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapAniversariantes(this IEndpointRouteBuilder rotas)
    {
        rotas.GrupoEu().WithTags("Aniversariantes").MapGet("/aniversariantes", Listar)
            .WithName("listarAniversariantes")
            .Produces<AniversariantesResposta>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);
        return rotas;
    }

    private static async Task<AniversariantesResposta> Listar(ListarAniversariantesCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(ct);
}
