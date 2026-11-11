using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Ponto;

namespace RHSystem.Api.Ponto;

/// <summary>
/// O ponto para o RH (<c>/api/rh</c>, só o perfil RH). Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/30-fatia-3-ponto.yaml</c>. Registrar por <c>operationId</c>, nunca por prefixo: sob
/// <c>/api/rh/funcionarios/{funcionarioId}/</c> o contrato também tem rotas de outras fatias.
/// </summary>
/// <summary>Os parâmetros da exportação para a folha: <c>ano</c> e <c>mes</c> são obrigatórios no contrato (ausentes dão o 400 do vínculo dos parâmetros; fora da faixa, o 400 do caso de uso).</summary>
public sealed record FiltrosDaFolha(int Ano, int Mes);

public static class PontoRhEndpoints
{
    private const string Etiqueta = "Ponto";

    public static IEndpointRouteBuilder MapPontoRh(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoRh().WithTags(Etiqueta);

        grupo.MapGet("/funcionarios/{funcionarioId:int}/ponto", ObterEspelho)
            .WithName("obterEspelhoDePontoDeFuncionario")
            .Produces<PontoEspelho>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        grupo.MapGet("/funcionarios/{funcionarioId:int}/ponto/banco-de-horas", ObterBancoDeHoras)
            .WithName("obterBancoDeHorasDeFuncionario")
            .Produces<PontoBancoDeHorasExtrato>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        grupo.MapGet("/ponto/folha/exportacao", ExportarParaFolha)
            .WithName("exportarPontoParaFolha")
            .Produces<string>(StatusCodes.Status200OK, "text/csv")
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapPut("/funcionarios/{funcionarioId:int}/ponto/dias/{dia}", CorrigirDia)
            .WithName("corrigirMarcacoesDoDia")
            .Produces<PontoEspelhoDia>(StatusCodes.Status200OK)
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

    private static async Task<PontoEspelhoDia> CorrigirDia(int funcionarioId, DateOnly dia, PontoDiaCorrigir corpo, CorrigirMarcacoesDoDiaCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(funcionarioId, dia, corpo, ct);

    private static async Task<IResult> ExportarParaFolha([AsParameters] FiltrosDaFolha f, ExportarPontoParaFolhaCasoDeUso casoDeUso, HttpContext http, CancellationToken ct)
    {
        var arquivo = await casoDeUso.ExecutarAsync(f.Ano, f.Mes, ct);
        http.Response.Headers.ContentDisposition = $"attachment; filename=\"{arquivo.NomeDoArquivo}\"";
        http.Response.Headers.CacheControl = "no-store"; // nome e horas de todo mundo: nenhum cache guarda o arquivo
        return Results.Bytes(arquivo.Conteudo, "text/csv; charset=utf-8");
    }

    private static async Task<PontoBancoDeHorasExtrato> ObterBancoDeHoras(int funcionarioId, [AsParameters] FiltrosDoExtrato f, ObterExtratoDoBancoDeHorasCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.DeFuncionarioAsync(funcionarioId, f.Pagina, f.TamanhoPagina, f.De, f.Ate, f.Tipo, ct);

    private static async Task<PontoEspelho> ObterEspelho(int funcionarioId, [AsParameters] FiltrosDoEspelho filtros, ObterEspelhoParaRhCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(funcionarioId, filtros.Ano, filtros.Mes, ct);
}
