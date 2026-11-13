using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Formularios.Acompanhar;
using RHSystem.Application.Formularios.Lembretes;

namespace RHSystem.Api.Formularios;

/// <summary>Os parâmetros de <c>GET /api/rh/formularios/{id}/respondentes</c>. <c>Situacao</c> chega como texto (o caso de uso o lê pelo nome do contrato e devolve todos os erros juntos num 400 só).</summary>
public sealed record FiltrosDosRespondentes(int Pagina = 1, int TamanhoPagina = 20, string? Busca = null, string? Situacao = null, int? SetorId = null);

/// <summary>Os parâmetros de <c>GET /api/rh/formularios/{id}/candidatos</c> (formulário por link): a página e a busca no nome e no e-mail.</summary>
public sealed record FiltrosDosCandidatos(int Pagina = 1, int TamanhoPagina = 20, string? Busca = null);

/// <summary>Os parâmetros de <c>GET /api/rh/formularios/{id}/respostas/funcionarios/{funcionarioId}</c>: o avaliado (nas avaliações) e o instante do envio (na Sugestão).</summary>
public sealed record FiltrosDasRespostasDeFuncionario(int? AvaliadoId = null, DateTime? RespondidoEmUtc = null);

/// <summary>O <c>avaliadoId</c> de <c>GET /api/rh/formularios/{id}/resultado</c> (obrigatório nas avaliações, proibido nos outros tipos: o caso de uso confere).</summary>
public sealed record FiltrosDoResultado(int? AvaliadoId = null);

/// <summary>
/// Quem respondeu, ver respostas, resultado e exportação, só o RH (<c>/api/rh/formularios/{id}/...</c>). Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/50-fatia-5-formularios.yaml</c>.
/// </summary>
public static class FormulariosRhAcompanhamentoEndpoints
{
    private const string Etiqueta = "Formulários";

    public static IServiceCollection AdicionarCasosDeUsoDoAcompanhamentoDosFormularios(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarRespondentesDoFormularioCasoDeUso>();
        servicos.AddScoped<ObterRespostasDeFuncionarioCasoDeUso>();
        servicos.AddScoped<BaixarArquivoDeRespostaCasoDeUso>();
        servicos.AddScoped<ListarCandidatosDoFormularioCasoDeUso>();
        servicos.AddScoped<ListarAvaliadosDoFormularioCasoDeUso>();
        servicos.AddScoped<ObterRespostasDeCandidatoCasoDeUso>();
        servicos.AddScoped<BaixarCurriculoDeCandidatoCasoDeUso>();
        servicos.AddScoped<ObterResultadoDoFormularioCasoDeUso>();
        servicos.AddScoped<ExportarRespostasDoFormularioCasoDeUso>();
        servicos.AddScoped<LembrarQuemFaltaFormularioCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapFormulariosRhAcompanhamento(this IEndpointRouteBuilder rotas)
    {
        var rh = rotas.GrupoRh().WithTags(Etiqueta);

        rh.MapGet("/formularios/{formularioId:int}/respondentes", Respondentes)
            .WithName("listarRespondentesFormulario")
            .Produces<FormularioRespondentesRh>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        rh.MapGet("/formularios/{formularioId:int}/respostas/funcionarios/{funcionarioId:int}", RespostasDeFuncionario)
            .WithName("obterRespostasDeFuncionario")
            .Produces<RespostaFormularioFuncionario>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        rh.MapGet("/formularios/{formularioId:int}/arquivos/{respostaId:int}/download", BaixarArquivo)
            .WithName("baixarArquivoDeResposta")
            .Produces<LinkTemporario>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        rh.MapGet("/formularios/{formularioId:int}/candidatos", Candidatos)
            .WithName("listarCandidatosFormulario")
            .Produces<CandidatoLista>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        rh.MapGet("/formularios/{formularioId:int}/respostas/candidatos/{candidatoId:int}", RespostasDeCandidato)
            .WithName("obterRespostasDeCandidato")
            .Produces<RespostaFormularioCandidato>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        rh.MapGet("/formularios/{formularioId:int}/candidatos/{candidatoId:int}/curriculo/download", BaixarCurriculo)
            .WithName("baixarCurriculoDeCandidato")
            .Produces<LinkTemporario>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        rh.MapGet("/formularios/{formularioId:int}/avaliados", Avaliados)
            .WithName("listarAvaliadosFormulario")
            .Produces<FormularioAvaliadosLista>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        rh.MapGet("/formularios/{formularioId:int}/resultado", Resultado)
            .WithName("obterResultadoFormulario")
            .Produces<FormularioResultado>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        rh.MapGet("/formularios/{formularioId:int}/exportacao", Exportar)
            .WithName("exportarRespostasFormulario")
            .Produces<string>(StatusCodes.Status200OK, "text/csv")
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        rh.MapPost("/formularios/{formularioId:int}/lembretes", Lembrar)
            .WithName("lembrarQuemFaltaFormulario")
            .Produces<FormularioLembreteResultado>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<FormularioRespondentesRh> Respondentes(int formularioId, [AsParameters] FiltrosDosRespondentes f, ListarRespondentesDoFormularioCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(formularioId, f.Pagina, f.TamanhoPagina, f.Busca, f.Situacao, f.SetorId, ct);

    private static async Task<RespostaFormularioFuncionario> RespostasDeFuncionario(int formularioId, int funcionarioId, [AsParameters] FiltrosDasRespostasDeFuncionario f, ObterRespostasDeFuncionarioCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(formularioId, funcionarioId, f.AvaliadoId, f.RespondidoEmUtc, ct);

    private static async Task<CandidatoLista> Candidatos(int formularioId, [AsParameters] FiltrosDosCandidatos f, ListarCandidatosDoFormularioCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(formularioId, f.Pagina, f.TamanhoPagina, f.Busca, ct);

    private static async Task<RespostaFormularioCandidato> RespostasDeCandidato(int formularioId, int candidatoId, ObterRespostasDeCandidatoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, candidatoId, ct);

    private static async Task<LinkTemporario> BaixarCurriculo(int formularioId, int candidatoId, BaixarCurriculoDeCandidatoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, candidatoId, ct);

    private static async Task<FormularioAvaliadosLista> Avaliados(int formularioId, ListarAvaliadosDoFormularioCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, ct);

    private static async Task<FormularioResultado> Resultado(int formularioId, [AsParameters] FiltrosDoResultado f, ObterResultadoDoFormularioCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, f.AvaliadoId, ct);

    private static async Task<IResult> Exportar(int formularioId, ExportarRespostasDoFormularioCasoDeUso casoDeUso, HttpContext http, CancellationToken ct)
    {
        var arquivo = await casoDeUso.ExecutarAsync(formularioId, ct);
        http.Response.Headers.ContentDisposition = $"attachment; filename=\"{arquivo.NomeDoArquivo}\"";
        http.Response.Headers.CacheControl = "no-store"; // respostas de formulário são dado pessoal: nenhum cache guarda o arquivo
        return Results.Bytes(arquivo.Conteudo, "text/csv; charset=utf-8");
    }

    private static async Task<FormularioLembreteResultado> Lembrar(int formularioId, FormularioLembreteComando corpo, LembrarQuemFaltaFormularioCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, corpo, ct);

    private static async Task<LinkTemporario> BaixarArquivo(int formularioId, int respostaId, BaixarArquivoDeRespostaCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, respostaId, ct);
}
