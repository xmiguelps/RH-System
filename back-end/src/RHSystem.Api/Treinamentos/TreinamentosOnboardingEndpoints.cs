using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Treinamentos.Onboarding;

namespace RHSystem.Api.Treinamentos;

/// <summary>
/// O onboarding dos treinamentos pelo RH: a lista dos que estão em vigor (<c>/api/rh/treinamentos/onboarding</c>, que liga a caixa "Incluir no treinamento" do cadastro) e a inclusão de uma pessoa já cadastrada (<c>/api/rh/funcionarios/{funcionarioId}/treinamentos/onboarding</c>). Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/60-fatia-6-tad.yaml</c>.
/// </summary>
public static class TreinamentosOnboardingEndpoints
{
    private const string Etiqueta = "Treinamentos";

    public static IServiceCollection AdicionarCasosDeUsoDoOnboardingDosTreinamentos(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarTreinamentosDeOnboardingCasoDeUso>();
        servicos.AddScoped<IncluirFuncionarioNoOnboardingCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapTreinamentosOnboarding(this IEndpointRouteBuilder rotas)
    {
        var rh = rotas.GrupoRh().WithTags(Etiqueta);

        rh.MapGet("/treinamentos/onboarding", Listar)
            .WithName("listarTreinamentosDeOnboarding")
            .Produces<TreinamentoOnboardingLista>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        rh.MapPost("/funcionarios/{funcionarioId:int}/treinamentos/onboarding", Incluir)
            .WithName("incluirFuncionarioNoOnboarding")
            .Produces<TreinamentoOnboardingInclusaoResposta>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<TreinamentoOnboardingLista> Listar(ListarTreinamentosDeOnboardingCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(ct);

    private static async Task<TreinamentoOnboardingInclusaoResposta> Incluir(int funcionarioId, IncluirFuncionarioNoOnboardingCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(funcionarioId, ct);
}
