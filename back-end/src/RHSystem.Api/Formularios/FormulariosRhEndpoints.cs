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
        servicos.AddScoped<AdicionarPerguntaCasoDeUso>();
        servicos.AddScoped<EditarPerguntaCasoDeUso>();
        servicos.AddScoped<ExcluirPerguntaCasoDeUso>();
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

        rh.MapPost("/formularios/{formularioId:int}/perguntas", AdicionarPergunta)
            .WithName("adicionarPerguntaFormulario")
            .Produces<PerguntaFormulario>(StatusCodes.Status201Created)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        rh.MapPut("/formularios/{formularioId:int}/perguntas/{perguntaId:int}", EditarPergunta)
            .WithName("editarPerguntaFormulario")
            .Produces<PerguntaFormulario>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        rh.MapDelete("/formularios/{formularioId:int}/perguntas/{perguntaId:int}", ExcluirPergunta)
            .WithName("excluirPerguntaFormulario")
            .Produces(StatusCodes.Status204NoContent)
            .ProduzProblemas(
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

    private static async Task<IResult> AdicionarPergunta(int formularioId, PerguntaComando corpo, AdicionarPerguntaCasoDeUso casoDeUso, CancellationToken ct) =>
        Results.Json(await casoDeUso.ExecutarAsync(formularioId, corpo, ct), statusCode: StatusCodes.Status201Created);

    private static async Task<PerguntaFormulario> EditarPergunta(int formularioId, int perguntaId, PerguntaComando corpo, EditarPerguntaCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, perguntaId, corpo, ct);

    private static async Task<IResult> ExcluirPergunta(int formularioId, int perguntaId, ExcluirPerguntaCasoDeUso casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(formularioId, perguntaId, ct);
        return Results.NoContent();
    }
}
