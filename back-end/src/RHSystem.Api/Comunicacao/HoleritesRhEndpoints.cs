using Microsoft.AspNetCore.Mvc;
using RHSystem.Api.Autorizacao;
using RHSystem.Api.Comum;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Comunicacao.Holerites;

namespace RHSystem.Api.Comunicacao;

/// <summary>Os parâmetros da lista de holerites de uma pessoa (RH): página, o ano e o tipo da competência (o tipo chega como texto para o caso de uso ler pelo nome do contrato) e se traz as versões substituídas.</summary>
public sealed record FiltrosDaListaDeHoleritesDoFuncionario(int Pagina = 1, int TamanhoPagina = 20, int? Ano = null, string? Tipo = null, bool TodasAsVersoes = false);

/// <summary>
/// Os holerites pelo RH (<c>/api/rh</c>): o envio individual e a nova versão (<c>multipart/form-data</c>, lido de <c>HttpRequest</c> como nos documentos para o 415 e o 400 certos), a lista de uma pessoa e o link de download. Os <c>operationId</c> e os códigos de resposta são os de
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
        servicos.AddScoped<EnviarHoleritesEmLoteCasoDeUso>();
        servicos.AddScoped<ListarHoleritesDoFuncionarioCasoDeUso>();
        servicos.AddScoped<BaixarHoleriteComoRhCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapHoleritesRh(this IEndpointRouteBuilder rotas)
    {
        var rh = rotas.GrupoRh().WithTags(Etiqueta);

        rh.MapGet("/funcionarios/{funcionarioId:int}/holerites", Listar)
            .WithName("listarHoleritesDoFuncionario")
            .Produces<HoleriteListaRh>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status500InternalServerError);

        rh.MapGet("/holerites/{holeriteId:int}/download", Baixar)
            .WithName("obterLinkDeDownloadDoHolerite")
            .Produces<LinkTemporario>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

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

        rh.MapPost("/holerites/lote", EnviarEmLote)
            .WithName(FormulariosDoContrato.EnviarHoleritesEmLote)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(LimitesDoLoteDeHolerites.TetoDaRequisicaoBytes), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = LimitesDoLoteDeHolerites.TetoDaRequisicaoBytes, ValueCountLimit = 1000 })
            .Produces<HoleriteLoteResultado>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<HoleriteListaRh> Listar(int funcionarioId, [AsParameters] FiltrosDaListaDeHoleritesDoFuncionario f, ListarHoleritesDoFuncionarioCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(funcionarioId, f.Pagina, f.TamanhoPagina, f.Ano, f.Tipo, f.TodasAsVersoes, ct);

    private static async Task<LinkTemporario> Baixar(int holeriteId, BaixarHoleriteComoRhCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(holeriteId, ct);

    private static async Task<IResult> EnviarEmLote(HttpRequest requisicao, EnviarHoleritesEmLoteCasoDeUso casoDeUso, CancellationToken ct)
    {
        var formulario = await LeituraDeFormulario.LerAsync(requisicao, ct);
        var arquivos = formulario.Files.GetFiles("arquivos").Select(f => new RHSystem.Application.Documentos.ArquivoRecebido(f.FileName, f.Length, f.OpenReadStream)).ToList();
        var lote = new NovoLoteDeHoleritesBruto(Texto(formulario, "mes"), Texto(formulario, "ano"), Texto(formulario, "tipo"), Texto(formulario, "publicarEm"), Texto(formulario, "noDiaDoPagamento"), Texto(formulario, "itens"), arquivos, formulario.Files.Count);
        return Results.Ok(await casoDeUso.ExecutarAsync(lote, ct));
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
