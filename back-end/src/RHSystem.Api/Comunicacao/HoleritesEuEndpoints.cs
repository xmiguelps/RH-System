using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Comunicacao.Holerites;

namespace RHSystem.Api.Comunicacao;

/// <summary>Os parâmetros da lista de holerites de quem está logado: página, o ano de competência (sem ele, o ano atual) e o tipo (texto, lido pelo nome do contrato no caso de uso).</summary>
public sealed record FiltrosDaListaDeMeusHolerites(int Pagina = 1, int TamanhoPagina = 20, int? Ano = null, string? Tipo = null);

/// <summary>
/// Os holerites de quem está logado (<c>/api/eu/holerites</c>, qualquer perfil): a lista e o link de download, só dos PUBLICADOS e só da versão atual. Os <c>operationId</c> e os códigos de resposta são os de
/// <c>contrato/fontes/40-fatia-4-comunicacao-e-documentos.yaml</c>. Nenhuma tem 403: a família <c>/api/eu</c> é de qualquer pessoa logada.
/// </summary>
public static class HoleritesEuEndpoints
{
    private const string Etiqueta = "Holerites";

    public static IServiceCollection AdicionarCasosDeUsoDeHoleritesEu(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarMeusHoleritesCasoDeUso>();
        servicos.AddScoped<BaixarMeuHoleriteCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapHoleritesEu(this IEndpointRouteBuilder rotas)
    {
        var eu = rotas.GrupoEu().WithTags(Etiqueta);

        eu.MapGet("/holerites", Listar)
            .WithName("listarMeusHolerites")
            .Produces<HoleriteListaEu>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);

        eu.MapGet("/holerites/{holeriteId:int}/download", Baixar)
            .WithName("obterLinkDeDownloadDoMeuHolerite")
            .Produces<LinkTemporario>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<HoleriteListaEu> Listar([AsParameters] FiltrosDaListaDeMeusHolerites f, ListarMeusHoleritesCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, f.Ano, f.Tipo, ct);

    private static async Task<LinkTemporario> Baixar(int holeriteId, BaixarMeuHoleriteCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(holeriteId, ct);
}
