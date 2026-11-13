using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Painel;

namespace RHSystem.Api.Painel;

/// <summary>A tela inicial do funcionário e do gestor, só o que é da gestão (<c>/api/eu/painel</c>, qualquer perfil, sempre os dados da própria pessoa). Os outros cartões vêm das rotas das fatias deles.</summary>
public static class PainelFuncionarioEndpoints
{
    private const string Etiqueta = "Painéis";

    public static IServiceCollection AdicionarCasosDeUsoDoPainelDoFuncionario(this IServiceCollection servicos)
    {
        servicos.AddScoped<ObterPainelDoFuncionarioCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapPainelDoFuncionario(this IEndpointRouteBuilder rotas)
    {
        rotas.GrupoEu().WithTags(Etiqueta).MapGet("/painel", Obter)
            .WithName("obterPainelDoFuncionario")
            .Produces<PainelFuncionarioDto>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<PainelFuncionarioDto> Obter(ObterPainelDoFuncionarioCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(ct);
}
