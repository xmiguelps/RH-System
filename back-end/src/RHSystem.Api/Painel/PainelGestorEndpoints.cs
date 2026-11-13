using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Painel;

namespace RHSystem.Api.Painel;

/// <summary>O painel da equipe do gestor (<c>/api/gestor/painel</c>, só o perfil Gestor, só a equipe dele). O quadro "Hoje" e o calendário de férias vêm das rotas da equipe e das férias.</summary>
public static class PainelGestorEndpoints
{
    private const string Etiqueta = "Painéis";

    public static IServiceCollection AdicionarCasosDeUsoDoPainelDoGestor(this IServiceCollection servicos)
    {
        servicos.AddScoped<ObterPainelDaEquipeCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapPainelDoGestor(this IEndpointRouteBuilder rotas)
    {
        rotas.GrupoGestor().WithTags(Etiqueta).MapGet("/painel", Obter)
            .WithName("obterPainelDaEquipe")
            .Produces<PainelGestorDto>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<PainelGestorDto> Obter(ObterPainelDaEquipeCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(ct);
}
