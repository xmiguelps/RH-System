using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comunicacao.Advertencias;

namespace RHSystem.Api.Comunicacao;

/// <summary>Os parâmetros da lista de advertências do RH: página, <c>busca</c> (nome e motivo), o ano da data do ocorrido, a situação e o tipo (texto, lidos pelo nome do contrato no caso de uso) e o setor atual da pessoa.</summary>
public sealed record FiltrosDaListaDeAdvertencias(int Pagina = 1, int TamanhoPagina = 20, string? Busca = null, int? Ano = null, string? Situacao = null, int? SetorId = null, string? Tipo = null);

/// <summary>O parâmetro dos resumos: o ano da data do ocorrido (sem ele, o ano atual).</summary>
public sealed record FiltroDoAnoDoResumo(int? Ano = null);

/// <summary>A página das listas por pessoa (a ficha).</summary>
public sealed record FiltrosDaListaDaFicha(int Pagina = 1, int TamanhoPagina = 20);

/// <summary>
/// As advertências pelo RH (<c>/api/rh</c>): a lista e o resumo do ano, o detalhe, a resposta à contestação e, por pessoa (a ficha), a lista e o registro. Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/40-fatia-4-comunicacao-e-documentos.yaml</c>.
/// </summary>
public static class AdvertenciasRhEndpoints
{
    private const string Etiqueta = "Advertências";

    public static IServiceCollection AdicionarCasosDeUsoDeAdvertenciasDoRh(this IServiceCollection servicos)
    {
        servicos.AddScoped<RegistradorDeAdvertencia>();
        servicos.AddScoped<ListarAdvertenciasDoRhCasoDeUso>();
        servicos.AddScoped<ObterResumoDasAdvertenciasCasoDeUso>();
        servicos.AddScoped<ObterAdvertenciaDoRhCasoDeUso>();
        servicos.AddScoped<ResponderContestacaoDeAdvertenciaCasoDeUso>();
        servicos.AddScoped<ListarAdvertenciasDoFuncionarioCasoDeUso>();
        servicos.AddScoped<RegistrarAdvertenciaDoRhCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapAdvertenciasRh(this IEndpointRouteBuilder rotas)
    {
        var rh = rotas.GrupoRh().WithTags(Etiqueta);

        rh.MapGet("/advertencias", Listar)
            .WithName("listarAdvertencias")
            .Produces<RespostaPaginada<AdvertenciaItem>>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        rh.MapGet("/advertencias/resumo", Resumo)
            .WithName("obterResumoDasAdvertencias")
            .Produces<AdvertenciaResumoRh>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        rh.MapGet("/advertencias/{advertenciaId:int}", Obter)
            .WithName("obterAdvertencia")
            .Produces<AdvertenciaDetalheRh>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        rh.MapPost("/advertencias/{advertenciaId:int}/resposta", Responder)
            .WithName("responderContestacaoDeAdvertencia")
            .Produces<AdvertenciaDetalheRh>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        rh.MapGet("/funcionarios/{funcionarioId:int}/advertencias", ListarDoFuncionario)
            .WithName("listarAdvertenciasDoFuncionario")
            .Produces<RespostaPaginada<AdvertenciaItem>>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status500InternalServerError);

        rh.MapPost("/funcionarios/{funcionarioId:int}/advertencias", Registrar)
            .WithName("registrarAdvertencia")
            .Produces<AdvertenciaDetalheRh>(StatusCodes.Status201Created)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<RespostaPaginada<AdvertenciaItem>> Listar([AsParameters] FiltrosDaListaDeAdvertencias f, ListarAdvertenciasDoRhCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, f.Busca, f.Ano, f.Situacao, f.SetorId, f.Tipo, ct);

    private static async Task<AdvertenciaResumoRh> Resumo([AsParameters] FiltroDoAnoDoResumo f, ObterResumoDasAdvertenciasCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(f.Ano, ct);

    private static async Task<AdvertenciaDetalheRh> Obter(int advertenciaId, ObterAdvertenciaDoRhCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(advertenciaId, ct);

    private static async Task<AdvertenciaDetalheRh> Responder(int advertenciaId, AdvertenciaRespostaComando corpo, ResponderContestacaoDeAdvertenciaCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(advertenciaId, corpo, ct);

    private static async Task<RespostaPaginada<AdvertenciaItem>> ListarDoFuncionario(int funcionarioId, [AsParameters] FiltrosDaListaDaFicha f, ListarAdvertenciasDoFuncionarioCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(funcionarioId, f.Pagina, f.TamanhoPagina, ct);

    private static async Task<IResult> Registrar(int funcionarioId, AdvertenciaRegistroRhComando corpo, RegistrarAdvertenciaDoRhCasoDeUso casoDeUso, CancellationToken ct) =>
        Results.Json(await casoDeUso.ExecutarAsync(funcionarioId, corpo, ct), statusCode: StatusCodes.Status201Created);
}
