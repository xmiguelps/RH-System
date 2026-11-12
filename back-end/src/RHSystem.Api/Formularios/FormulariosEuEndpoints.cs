using Microsoft.AspNetCore.Mvc;
using RHSystem.Api.Autorizacao;
using RHSystem.Api.Comum;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Documentos;
using RHSystem.Application.Formularios.Responder;

namespace RHSystem.Api.Formularios;

/// <summary>Os parâmetros de <c>GET /api/eu/formularios</c>. <c>Tipo</c> e <c>Situacao</c> chegam como texto: o caso de uso os lê pelo nome do contrato e devolve todos os erros juntos num 400 só.</summary>
public sealed record FiltrosDaListaDeMeusFormularios(int Pagina = 1, int TamanhoPagina = 20, string? Busca = null, string? Tipo = null, string? Situacao = null);

/// <summary>O <c>avaliadoId</c> de <c>GET /api/eu/formularios/{id}/minhas-respostas</c> (obrigatório nas avaliações, proibido nos outros tipos: o caso de uso confere).</summary>
public sealed record FiltrosDasMinhasRespostas(int? AvaliadoId = null);

/// <summary>
/// Os formulários de quem está logado (<c>/api/eu/formularios</c>, qualquer perfil: nenhuma rota tem 403). Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/50-fatia-5-formularios.yaml</c>. Esta parte: a lista de cartões, abrir o formulário, responder (<c>multipart/form-data</c>, lido de <c>HttpRequest</c> como nos documentos para o 415 e o 400 certos) e ver o que a pessoa respondeu.
/// </summary>
public static class FormulariosEuEndpoints
{
    private const string Etiqueta = "Formulários";

    public static IServiceCollection AdicionarCasosDeUsoDosFormulariosEu(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarMeusFormulariosCasoDeUso>();
        servicos.AddScoped<ObterFormularioParaResponderCasoDeUso>();
        servicos.AddScoped<ResponderFormularioCasoDeUso>();
        servicos.AddScoped<ObterMinhasRespostasCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapFormulariosEu(this IEndpointRouteBuilder rotas)
    {
        var eu = rotas.GrupoEu().WithTags(Etiqueta);

        eu.MapGet("/formularios", Listar)
            .WithName("listarMeusFormularios")
            .Produces<FormularioMeusLista>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);

        eu.MapGet("/formularios/{formularioId:int}", Obter)
            .WithName("obterFormularioParaResponder")
            .Produces<FormularioParaResponder>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        eu.MapPost("/formularios/{formularioId:int}/respostas", Responder)
            .WithName(FormulariosDoContrato.ResponderFormulario)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(ValidacaoDeArquivoDeFormulario.TetoDaRequisicaoBytes), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = ValidacaoDeArquivoDeFormulario.TetoDaRequisicaoBytes })
            .Produces<RespostaFormularioRecibo>(StatusCodes.Status201Created)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        eu.MapGet("/formularios/{formularioId:int}/minhas-respostas", MinhasRespostas)
            .WithName("obterMinhasRespostas")
            .Produces<RespostaFormularioMinhas>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<FormularioMeusLista> Listar([AsParameters] FiltrosDaListaDeMeusFormularios f, ListarMeusFormulariosCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, f.Busca, f.Tipo, f.Situacao, ct);

    private static async Task<FormularioParaResponder> Obter(int formularioId, ObterFormularioParaResponderCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, ct);

    private static async Task<IResult> Responder(int formularioId, HttpRequest requisicao, ResponderFormularioCasoDeUso casoDeUso, CancellationToken ct)
    {
        var formulario = await LeituraDeFormulario.LerAsync(requisicao, ct);
        var arquivos = formulario.Files.GetFiles("arquivos").Select(f => new ArquivoRecebido(f.FileName, f.Length, f.OpenReadStream)).ToList();
        var novo = new NovaRespostaDeFormulario(Texto(formulario, "avaliadoId"), Texto(formulario, "respostas"), arquivos, formulario.Files.Count);
        return Results.Json(await casoDeUso.ExecutarAsync(formularioId, novo, ct), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<RespostaFormularioMinhas> MinhasRespostas(int formularioId, [AsParameters] FiltrosDasMinhasRespostas f, ObterMinhasRespostasCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, f.AvaliadoId, ct);

    private static string? Texto(IFormCollection formulario, string campo) => formulario.TryGetValue(campo, out var valor) ? valor.ToString() : null;
}
