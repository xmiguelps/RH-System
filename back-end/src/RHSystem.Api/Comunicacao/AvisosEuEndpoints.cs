using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Comunicacao.Avisos;

namespace RHSystem.Api.Comunicacao;

/// <summary>Os parâmetros da lista de avisos de quem está logado: página, <c>busca</c> (título e conteúdo), só os não lidos e os fixados primeiro. Sem <c>ordenarPor</c>.</summary>
public sealed record FiltrosDaListaDeMeusAvisos(int Pagina = 1, int TamanhoPagina = 20, string? Busca = null, bool ApenasNaoLidos = false, bool FixadosPrimeiro = false);

/// <summary>
/// Os avisos de quem está logado (<c>/api/eu/avisos</c>, qualquer perfil): a lista dos vigentes, o aviso inteiro e a marca de leitura. Os <c>operationId</c> e os códigos de resposta são os de
/// <c>contrato/fontes/40-fatia-4-comunicacao-e-documentos.yaml</c>. Nenhuma tem 403: a família <c>/api/eu</c> é de qualquer pessoa logada.
/// </summary>
public static class AvisosEuEndpoints
{
    private const string Etiqueta = "Avisos";

    public static IServiceCollection AdicionarCasosDeUsoDeAvisosEu(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarMeusAvisosCasoDeUso>();
        servicos.AddScoped<ObterMeuAvisoCasoDeUso>();
        servicos.AddScoped<MarcarAvisoComoLidoCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapAvisosEu(this IEndpointRouteBuilder rotas)
    {
        var eu = rotas.GrupoEu().WithTags(Etiqueta);

        eu.MapGet("/avisos", Listar)
            .WithName("listarMeusAvisos")
            .Produces<AvisoListaEu>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);

        eu.MapGet("/avisos/{avisoId:int}", Obter)
            .WithName("obterMeuAviso")
            .Produces<AvisoDetalheEu>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        eu.MapPost("/avisos/{avisoId:int}/leitura", MarcarComoLido)
            .WithName("marcarAvisoComoLido")
            .Produces(StatusCodes.Status204NoContent)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<AvisoListaEu> Listar([AsParameters] FiltrosDaListaDeMeusAvisos f, ListarMeusAvisosCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, f.Busca, f.ApenasNaoLidos, f.FixadosPrimeiro, ct);

    private static async Task<AvisoDetalheEu> Obter(int avisoId, ObterMeuAvisoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(avisoId, ct);

    private static async Task<IResult> MarcarComoLido(int avisoId, MarcarAvisoComoLidoCasoDeUso casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(avisoId, ct);
        return Results.NoContent();
    }
}
