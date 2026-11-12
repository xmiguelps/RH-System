using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comunicacao.Advertencias;

namespace RHSystem.Api.Comunicacao;

/// <summary>Os parâmetros da lista de advertências da equipe: página, o ano da data do ocorrido (sem ele, o ano atual) e a situação (texto, lida pelo nome do contrato no caso de uso).</summary>
public sealed record FiltrosDaListaDeAdvertenciasDaEquipe(int Pagina = 1, int TamanhoPagina = 20, int? Ano = null, string? Situacao = null);

/// <summary>
/// As advertências pelo gestor (<c>/api/gestor</c>), SÓ da equipe dele: a lista e o resumo do ano, o detalhe, a lista de uma pessoa e o registro. Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/40-fatia-4-comunicacao-e-documentos.yaml</c>. Quem não é o
/// gestor da equipe (outro setor, o próprio gestor, pessoa inativa) recebe 404; o RH e o funcionário recebem 403 (<c>GrupoGestor</c>).
/// </summary>
public static class AdvertenciasGestorEndpoints
{
    private const string Etiqueta = "Advertências";

    public static IServiceCollection AdicionarCasosDeUsoDeAdvertenciasDoGestor(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarAdvertenciasDaEquipeCasoDeUso>();
        servicos.AddScoped<ObterResumoDasAdvertenciasDaEquipeCasoDeUso>();
        servicos.AddScoped<ObterAdvertenciaDaEquipeCasoDeUso>();
        servicos.AddScoped<ListarAdvertenciasDoFuncionarioDaEquipeCasoDeUso>();
        servicos.AddScoped<RegistrarAdvertenciaDaEquipeCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapAdvertenciasGestor(this IEndpointRouteBuilder rotas)
    {
        var gestor = rotas.GrupoGestor().WithTags(Etiqueta);

        gestor.MapGet("/advertencias", Listar)
            .WithName("listarAdvertenciasDaEquipe")
            .Produces<RespostaPaginada<AdvertenciaItem>>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        gestor.MapGet("/advertencias/resumo", Resumo)
            .WithName("obterResumoDasAdvertenciasDaEquipe")
            .Produces<AdvertenciaResumoGestor>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        gestor.MapGet("/advertencias/{advertenciaId:int}", Obter)
            .WithName("obterAdvertenciaDaEquipe")
            .Produces<AdvertenciaDetalheGestor>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        gestor.MapGet("/equipe/{funcionarioId:int}/advertencias", ListarDoFuncionario)
            .WithName("listarAdvertenciasDoFuncionarioDaEquipe")
            .Produces<RespostaPaginada<AdvertenciaItem>>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status500InternalServerError);

        gestor.MapPost("/equipe/{funcionarioId:int}/advertencias", Registrar)
            .WithName("registrarAdvertenciaDaEquipe")
            .Produces<AdvertenciaDetalheGestor>(StatusCodes.Status201Created)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<RespostaPaginada<AdvertenciaItem>> Listar([AsParameters] FiltrosDaListaDeAdvertenciasDaEquipe f, ListarAdvertenciasDaEquipeCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, f.Ano, f.Situacao, ct);

    private static async Task<AdvertenciaResumoGestor> Resumo([AsParameters] FiltroDoAnoDoResumo f, ObterResumoDasAdvertenciasDaEquipeCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(f.Ano, ct);

    private static async Task<AdvertenciaDetalheGestor> Obter(int advertenciaId, ObterAdvertenciaDaEquipeCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(advertenciaId, ct);

    private static async Task<RespostaPaginada<AdvertenciaItem>> ListarDoFuncionario(int funcionarioId, [AsParameters] FiltrosDaListaDaFicha f, ListarAdvertenciasDoFuncionarioDaEquipeCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(funcionarioId, f.Pagina, f.TamanhoPagina, ct);

    private static async Task<IResult> Registrar(int funcionarioId, AdvertenciaRegistroGestorComando corpo, RegistrarAdvertenciaDaEquipeCasoDeUso casoDeUso, CancellationToken ct) =>
        Results.Json(await casoDeUso.ExecutarAsync(funcionarioId, corpo, ct), statusCode: StatusCodes.Status201Created);
}
