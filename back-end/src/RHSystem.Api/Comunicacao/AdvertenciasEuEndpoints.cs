using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comunicacao.Advertencias;

namespace RHSystem.Api.Comunicacao;

/// <summary>Os parâmetros da lista de advertências de quem está logado: página e a situação (texto, lida pelo nome do contrato no caso de uso).</summary>
public sealed record FiltrosDaListaDeMinhasAdvertencias(int Pagina = 1, int TamanhoPagina = 20, string? Situacao = null);

/// <summary>
/// As advertências de quem está logado (<c>/api/eu/advertencias</c>, qualquer perfil): a lista, a ciência e a contestação. Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/40-fatia-4-comunicacao-e-documentos.yaml</c>. Nenhuma tem 403: a família
/// <c>/api/eu</c> é de qualquer pessoa logada e só enxerga o que é dela (a advertência de outra pessoa dá 404).
/// </summary>
public static class AdvertenciasEuEndpoints
{
    private const string Etiqueta = "Advertências";

    public static IServiceCollection AdicionarCasosDeUsoDeAdvertenciasEu(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarMinhasAdvertenciasCasoDeUso>();
        servicos.AddScoped<ConfirmarCienciaDeAdvertenciaCasoDeUso>();
        servicos.AddScoped<ContestarAdvertenciaCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapAdvertenciasEu(this IEndpointRouteBuilder rotas)
    {
        var eu = rotas.GrupoEu().WithTags(Etiqueta);

        eu.MapGet("/advertencias", Listar)
            .WithName("listarMinhasAdvertencias")
            .Produces<RespostaPaginada<AdvertenciaEu>>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);

        eu.MapPost("/advertencias/{advertenciaId:int}/ciencia", ConfirmarCiencia)
            .WithName("confirmarCienciaDeAdvertencia")
            .Produces<AdvertenciaEu>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        eu.MapPost("/advertencias/{advertenciaId:int}/contestacao", Contestar)
            .WithName("contestarAdvertencia")
            .Produces<AdvertenciaEu>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<RespostaPaginada<AdvertenciaEu>> Listar([AsParameters] FiltrosDaListaDeMinhasAdvertencias f, ListarMinhasAdvertenciasCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, f.Situacao, ct);

    private static async Task<AdvertenciaEu> ConfirmarCiencia(int advertenciaId, ConfirmarCienciaDeAdvertenciaCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(advertenciaId, ct);

    private static async Task<AdvertenciaEu> Contestar(int advertenciaId, AdvertenciaContestacaoComando corpo, ContestarAdvertenciaCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(advertenciaId, corpo, ct);
}
