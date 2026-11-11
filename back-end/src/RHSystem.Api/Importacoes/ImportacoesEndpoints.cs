using Microsoft.AspNetCore.Mvc;
using RHSystem.Api.Autorizacao;
using RHSystem.Api.Comum;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Importacoes;

namespace RHSystem.Api.Importacoes;

/// <summary>
/// A importação de colaboradores por planilha Excel (fatia 2), só para o RH (<c>GrupoRh</c>): baixar o modelo, enviar a planilha e receber a prévia, confirmar. Os <c>operationId</c> e os códigos de resposta são os de
/// <c>contrato/fontes/25-fatia-2-importacao.yaml</c>.
/// </summary>
public static class ImportacoesEndpoints
{
    private const string Etiqueta = "Importação";

    public static IServiceCollection AdicionarCasosDeUsoDeImportacao(this IServiceCollection servicos)
    {
        servicos.AddScoped<ModeloDeImportacaoCasoDeUso>();
        servicos.AddScoped<MontadorDaPrevia>();
        servicos.AddScoped<ValidarPlanilhaCasoDeUso>();
        servicos.AddScoped<ConfirmarImportacaoCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapImportacoes(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoRh().WithTags(Etiqueta);

        grupo.MapGet("/importacoes/modelo", BaixarModelo)
            .WithName("baixarModeloDeImportacao")
            .Produces(StatusCodes.Status200OK, contentType: ModeloDeImportacaoCasoDeUso.TipoDeConteudo)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        // multipart LIDO de HttpRequest (415 se o corpo não é multipart, 400 se está quebrado) e sem antiforgery: a autenticação é por cabeçalho, sem cookie. O teto do corpo é de 6 MiB; de 5 a 6 MiB o 422 é do contrato.
        grupo.MapPost("/importacoes", Validar)
            .WithName(FormulariosDoContrato.ValidarPlanilhaDeImportacao)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(LimitesDeImportacao.TetoDaRequisicaoBytes), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = LimitesDeImportacao.TetoDaRequisicaoBytes })
            .Produces<ImportacaoPrevia>(StatusCodes.Status201Created)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        grupo.MapPost("/importacoes/{importacaoId:int}/confirmar", Confirmar)
            .WithName("confirmarImportacao")
            .Produces<ImportacaoResultado>(StatusCodes.Status200OK)
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

    private static async Task<IResult> BaixarModelo(ModeloDeImportacaoCasoDeUso casoDeUso, CancellationToken ct) =>
        Results.File(await casoDeUso.GerarAsync(ct), ModeloDeImportacaoCasoDeUso.TipoDeConteudo, ModeloDeImportacaoCasoDeUso.NomeDoArquivo);

    private static async Task<ImportacaoResultado> Confirmar(int importacaoId, ImportacaoConfirmarComando comando, ConfirmarImportacaoCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(importacaoId, comando, ct);

    /// <summary>201 SEM <c>Location</c> (o contrato não a declara): a prévia não é um recurso que se busque depois, só se confirma pelo id.</summary>
    private static async Task<IResult> Validar(HttpRequest requisicao, ValidarPlanilhaCasoDeUso casoDeUso, CancellationToken ct)
    {
        var formulario = await LeituraDeFormulario.LerAsync(requisicao, ct);
        var previa = await casoDeUso.ExecutarAsync(LeituraDeFormulario.Arquivo(formulario), formulario.Files.Count, ct); // conta as partes de arquivo de QUALQUER campo
        return Results.Json(previa, statusCode: StatusCodes.Status201Created);
    }
}
