using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Formularios.Equipe;
using RHSystem.Application.Formularios.Lembretes;

namespace RHSystem.Api.Formularios;

/// <summary>
/// Os formulários da equipe do gestor (<c>/api/gestor/formularios</c>, só o perfil Gestor): os pedidos do RH abertos na equipe, quem da equipe respondeu (só quem, nunca o conteúdo) e o lembrete à equipe. Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/50-fatia-5-formularios.yaml</c>.
/// </summary>
public static class FormulariosGestorEndpoints
{
    private const string Etiqueta = "Formulários";

    public static IServiceCollection AdicionarCasosDeUsoDosFormulariosDoGestor(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarFormulariosDaEquipeCasoDeUso>();
        servicos.AddScoped<ListarRespondentesDaEquipeCasoDeUso>();
        servicos.AddScoped<LembrarQuemFaltaNaEquipeCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapFormulariosDoGestor(this IEndpointRouteBuilder rotas)
    {
        var gestor = rotas.GrupoGestor().WithTags(Etiqueta);

        gestor.MapGet("/formularios", Listar)
            .WithName("listarFormulariosDaEquipe")
            .Produces<FormularioEquipeLista>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        gestor.MapGet("/formularios/{formularioId:int}/respondentes", Respondentes)
            .WithName("listarRespondentesDaEquipe")
            .Produces<FormularioEquipeRespondentes>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        gestor.MapPost("/formularios/{formularioId:int}/lembretes", Lembrar)
            .WithName("lembrarQuemFaltaNaEquipe")
            .Produces<FormularioLembreteResultado>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<FormularioEquipeLista> Listar(ListarFormulariosDaEquipeCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(ct);

    private static async Task<FormularioEquipeRespondentes> Respondentes(int formularioId, ListarRespondentesDaEquipeCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, ct);

    private static async Task<FormularioLembreteResultado> Lembrar(int formularioId, LembrarQuemFaltaNaEquipeCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, ct);
}
