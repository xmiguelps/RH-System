using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Formularios;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Regras;

namespace RHSystem.Api.Formularios;

/// <summary>
/// Os parâmetros de <c>GET /api/rh/formularios</c>. <c>Tipo</c>, <c>Situacao</c>, <c>OrdenarPor</c> e <c>Direcao</c> chegam como texto: o caso de uso os lê pelo nome do contrato (<c>ENTREVISTA</c>, <c>criadoEm</c>, <c>ASC</c>) e devolve todos os erros juntos num 400 só.
/// </summary>
public sealed record FiltrosDaListaDeFormularios(int Pagina = 1, int TamanhoPagina = 20, string? Busca = null, string? Tipo = null, string? Situacao = null, string? OrdenarPor = null, string? Direcao = null);

/// <summary>
/// Os formulários pelo RH (<c>/api/rh/formularios</c>). Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/50-fatia-5-formularios.yaml</c>. Esta parte: criar, obter e editar os detalhes; as demais rotas entram nas tarefas seguintes
/// da fatia.
/// </summary>
public static class FormulariosRhEndpoints
{
    private const string Etiqueta = "Formulários";

    public static IServiceCollection AdicionarCasosDeUsoDosFormulariosDoRh(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarFormulariosRhCasoDeUso>();
        servicos.AddScoped<ExcluirFormularioCasoDeUso>();
        servicos.AddScoped<ObterFormularioRhCasoDeUso>();
        servicos.AddScoped<CriarFormularioCasoDeUso>();
        servicos.AddScoped<EditarFormularioCasoDeUso>();
        servicos.AddScoped<AdicionarPerguntaCasoDeUso>();
        servicos.AddScoped<EditarPerguntaCasoDeUso>();
        servicos.AddScoped<ExcluirPerguntaCasoDeUso>();
        servicos.AddScoped<SalvarPublicacaoCasoDeUso>();
        servicos.AddScoped<VerificarFormularioCasoDeUso>();
        servicos.AddScoped<PublicarFormularioCasoDeUso>();
        servicos.AddScoped<EncerrarFormularioCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapFormulariosRh(this IEndpointRouteBuilder rotas)
    {
        var rh = rotas.GrupoRh().WithTags(Etiqueta);

        rh.MapGet("/formularios", Listar)
            .WithName("listarFormulariosRh")
            .Produces<FormularioRhLista>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        rh.MapDelete("/formularios/{formularioId:int}", Excluir)
            .WithName("excluirFormulario")
            .Produces(StatusCodes.Status204NoContent)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status500InternalServerError);

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

        rh.MapGet("/formularios/{formularioId:int}/verificacao", Verificar)
            .WithName("verificarFormulario")
            .Produces<FormularioVerificacao>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        rh.MapPut("/formularios/{formularioId:int}/publicacao", SalvarPublicacao)
            .WithName("salvarPublicacaoFormulario")
            .Produces<FormularioDetalheRh>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        rh.MapPost("/formularios/{formularioId:int}/publicar", Publicar)
            .WithName("publicarFormulario")
            .Produces<FormularioDetalheRh>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        rh.MapPost("/formularios/{formularioId:int}/encerrar", Encerrar)
            .WithName("encerrarFormulario")
            .Produces<FormularioDetalheRh>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<FormularioRhLista> Listar([AsParameters] FiltrosDaListaDeFormularios f, ListarFormulariosRhCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, f.Busca, f.Tipo, f.Situacao, f.OrdenarPor, f.Direcao, ct);

    private static async Task<IResult> Excluir(int formularioId, bool? confirmar, ExcluirFormularioCasoDeUso casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(formularioId, confirmar ?? false, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Criar(FormularioDetalhesComando corpo, CriarFormularioCasoDeUso casoDeUso, CancellationToken ct) => Results.Json(await casoDeUso.ExecutarAsync(corpo, ct), statusCode: StatusCodes.Status201Created);

    private static async Task<FormularioDetalheRh> Obter(int formularioId, ObterFormularioRhCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, ct);

    private static async Task<FormularioDetalheRh> Editar(int formularioId, FormularioDetalhesComando corpo, EditarFormularioCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, corpo, ct);

    private static async Task<IResult> AdicionarPergunta(int formularioId, PerguntaComando corpo, AdicionarPerguntaCasoDeUso casoDeUso, CancellationToken ct) =>
        Results.Json(await casoDeUso.ExecutarAsync(formularioId, corpo, ct), statusCode: StatusCodes.Status201Created);

    private static async Task<PerguntaFormulario> EditarPergunta(int formularioId, int perguntaId, PerguntaComando corpo, EditarPerguntaCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, perguntaId, corpo, ct);

    private static async Task<FormularioVerificacao> Verificar(int formularioId, VerificarFormularioCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, ct);

    private static async Task<FormularioDetalheRh> SalvarPublicacao(int formularioId, FormularioPublicacaoComando corpo, SalvarPublicacaoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, corpo, ct);

    private static async Task<FormularioDetalheRh> Publicar(int formularioId, FormularioPublicacaoComando corpo, PublicarFormularioCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, corpo, ct);

    private static async Task<FormularioDetalheRh> Encerrar(int formularioId, bool? confirmar, EncerrarFormularioCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, confirmar ?? false, ct);

    private static async Task<IResult> ExcluirPergunta(int formularioId, int perguntaId, ExcluirPerguntaCasoDeUso casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(formularioId, perguntaId, ct);
        return Results.NoContent();
    }
}
