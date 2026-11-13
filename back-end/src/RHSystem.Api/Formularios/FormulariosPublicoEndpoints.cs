using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using Microsoft.AspNetCore.Mvc;
using RHSystem.Api.Comum;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Documentos;
using RHSystem.Application.Formularios.Publico;

namespace RHSystem.Api.Formularios;

/// <summary>
/// O formulário do candidato (<c>/api/publico/formularios/{token}</c>, sem login): a página e, na tarefa seguinte, a candidatura. O limite por IP (30 consultas por minuto, 10 envios por hora) roda ANTES de qualquer coisa e o código do link nunca vai a log. Os <c>operationId</c> e os códigos de
/// resposta são os de <c>contrato/fontes/50-fatia-5-formularios.yaml</c>.
/// </summary>
public static class FormulariosPublicoEndpoints
{
    private const string Etiqueta = "Formulário público";

    public static IServiceCollection AdicionarCasosDeUsoDoFormularioPublico(this IServiceCollection servicos, IConfiguration configuracao)
    {
        servicos.AddSingleton(configuracao.GetSection(OpcoesDoFormularioPublico.Secao).Get<OpcoesDoFormularioPublico>() ?? new OpcoesDoFormularioPublico());
        servicos.AddScoped<ObterFormularioPublicoCasoDeUso>();
        servicos.AddScoped<ResponderFormularioPublicoCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapFormulariosPublico(this IEndpointRouteBuilder rotas)
    {
        var publico = rotas.GrupoPublico().WithTags(Etiqueta);

        publico.MapGet("/formularios/{token}", Pagina)
            .WithName("obterFormularioPublico")
            .RequireRateLimiting(LimiteDeTentativasExtensions.PoliticaDeFormularioConsulta)
            .Produces<FormularioPaginaPublica>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status429TooManyRequests, StatusCodes.Status500InternalServerError);

        publico.MapPost("/formularios/{token}/respostas", Responder)
            .WithName(FormulariosDoContrato.ResponderFormularioPublico)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(ValidacaoDeArquivoDeFormulario.TetoDaCandidaturaBytes), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = ValidacaoDeArquivoDeFormulario.TetoDaCandidaturaBytes })
            .RequireRateLimiting(LimiteDeTentativasExtensions.PoliticaDeFormularioEnvio)
            .Produces<CandidatoRecibo>(StatusCodes.Status201Created)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status429TooManyRequests, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<IResult> Responder(string token, HttpRequest requisicao, ResponderFormularioPublicoCasoDeUso casoDeUso, CancellationToken ct)
    {
        var formulario = await LeituraDeFormulario.LerAsync(requisicao, ct);
        var curriculo = formulario.Files.GetFile("curriculo") is { } enviado ? new ArquivoRecebido(enviado.FileName, enviado.Length, enviado.OpenReadStream) : null;
        var arquivos = formulario.Files.GetFiles("arquivos").Select(f => new ArquivoRecebido(f.FileName, f.Length, f.OpenReadStream)).ToList();
        var novo = new NovaCandidatura(Texto(formulario, "nome"), Texto(formulario, "email"), Texto(formulario, "telefone"), Texto(formulario, "aceitouAvisoDePrivacidade"), Texto(formulario, "respostas"), curriculo, arquivos, formulario.Files.Count);
        return Results.Json(await casoDeUso.ExecutarAsync(token, novo, ct), statusCode: StatusCodes.Status201Created);
    }

    private static string? Texto(IFormCollection formulario, string campo) => formulario.TryGetValue(campo, out var valor) ? valor.ToString() : null;

    private static async Task<FormularioPaginaPublica> Pagina(string token, ObterFormularioPublicoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(token, ct);
}
