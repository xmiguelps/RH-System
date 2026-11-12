using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Formularios;
using RHSystem.Application.Formularios.Construtor;

namespace RHSystem.Api.Formularios;

/// <summary>
/// Os formulários pelo RH (<c>/api/rh/formularios</c>). Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/50-fatia-5-formularios.yaml</c>. Esta parte: criar, obter e editar os detalhes; as demais rotas entram nas tarefas seguintes
/// da fatia.
/// </summary>
public static class FormulariosRhEndpoints
{
    private const string Etiqueta = "Formulários";

    public static IServiceCollection AdicionarCasosDeUsoDosFormulariosDoRh(this IServiceCollection servicos)
    {
        servicos.AddScoped<ObterFormularioRhCasoDeUso>();
        servicos.AddScoped<CriarFormularioCasoDeUso>();
        servicos.AddScoped<EditarFormularioCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapFormulariosRh(this IEndpointRouteBuilder rotas)
    {
        var rh = rotas.GrupoRh().WithTags(Etiqueta);

        rh.MapPost("/formularios", Criar)
            .WithName("criarFormulario")
            .Produces<FormularioDetalheRh>(StatusCodes.Status201Created)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        rh.MapGet("/formularios/{formularioId:int}", Obter)
            .WithName("obterFormularioRh")
            .Produces<FormularioDetalheRh>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        rh.MapPut("/formularios/{formularioId:int}", Editar)
            .WithName("editarFormulario")
            .Produces<FormularioDetalheRh>(StatusCodes.Status200OK)
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

    private static async Task<IResult> Criar(FormularioDetalhesComando corpo, CriarFormularioCasoDeUso casoDeUso, CancellationToken ct) => Results.Json(await casoDeUso.ExecutarAsync(corpo, ct), statusCode: StatusCodes.Status201Created);

    private static async Task<FormularioDetalheRh> Obter(int formularioId, ObterFormularioRhCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, ct);

    private static async Task<FormularioDetalheRh> Editar(int formularioId, FormularioDetalhesComando corpo, EditarFormularioCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, corpo, ct);
}
