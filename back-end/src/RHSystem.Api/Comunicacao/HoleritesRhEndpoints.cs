using Microsoft.AspNetCore.Mvc;
using RHSystem.Api.Autorizacao;
using RHSystem.Api.Comum;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Comunicacao.Holerites;

namespace RHSystem.Api.Comunicacao;

/// <summary>
/// Os holerites pelo RH (<c>/api/rh</c>): o envio individual e a nova versão (<c>multipart/form-data</c>, lido de <c>HttpRequest</c> como nos documentos para o 415 e o 400 certos). Os <c>operationId</c> e os códigos de resposta são os de
/// <c>contrato/fontes/40-fatia-4-comunicacao-e-documentos.yaml</c>; o teto da requisição (6 MiB) deixa o erro de arquivo grande ser o 400 em <c>arquivo</c> do contrato, e não o 413.
/// </summary>
public static class HoleritesRhEndpoints
{
    private const string Etiqueta = "Holerites";

    public static IServiceCollection AdicionarCasosDeUsoDeHoleritesDoRh(this IServiceCollection servicos)
    {
        servicos.AddScoped<PublicadorDeHolerite>();
        servicos.AddScoped<EnviarHoleriteCasoDeUso>();
        servicos.AddScoped<SubstituirHoleriteCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapHoleritesRh(this IEndpointRouteBuilder rotas)
    {
        var rh = rotas.GrupoRh().WithTags(Etiqueta);

        rh.MapPost("/funcionarios/{funcionarioId:int}/holerites", Enviar)
            .WithName(FormulariosDoContrato.EnviarHolerite)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(LimitesDeHolerite.TetoDaRequisicaoBytes), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = LimitesDeHolerite.TetoDaRequisicaoBytes })
            .Produces<HoleriteItemRh>(StatusCodes.Status201Created)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        rh.MapPost("/holerites/{holeriteId:int}/versoes", Substituir)
            .WithName(FormulariosDoContrato.SubstituirHolerite)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(LimitesDeHolerite.TetoDaRequisicaoBytes), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = LimitesDeHolerite.TetoDaRequisicaoBytes })
            .Produces<HoleriteItemRh>(StatusCodes.Status201Created)
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

    private static async Task<IResult> Enviar(int funcionarioId, HttpRequest requisicao, EnviarHoleriteCasoDeUso casoDeUso, CancellationToken ct)
    {
        var formulario = await LeituraDeFormulario.LerAsync(requisicao, ct);
        var novo = new NovoHolerite(Texto(formulario, "mes"), Texto(formulario, "ano"), Texto(formulario, "tipo"), Texto(formulario, "publicarEm"), Texto(formulario, "noDiaDoPagamento"), LeituraDeFormulario.Arquivo(formulario), formulario.Files.Count);
        return Results.Json(await casoDeUso.ExecutarAsync(funcionarioId, novo, ct), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> Substituir(int holeriteId, HttpRequest requisicao, SubstituirHoleriteCasoDeUso casoDeUso, CancellationToken ct)
    {
        var formulario = await LeituraDeFormulario.LerAsync(requisicao, ct);
        var novo = new NovaVersaoDeHolerite(Texto(formulario, "publicarEm"), Texto(formulario, "noDiaDoPagamento"), LeituraDeFormulario.Arquivo(formulario), formulario.Files.Count);
        return Results.Json(await casoDeUso.ExecutarAsync(holeriteId, novo, ct), statusCode: StatusCodes.Status201Created);
    }

    private static string? Texto(IFormCollection formulario, string campo) => formulario.TryGetValue(campo, out var valor) ? valor.ToString() : null;
}
