using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Auditoria;

namespace RHSystem.Api.Auditoria;

/// <summary>
/// O log de auditoria (tela 31): lista, detalhe e exportação em CSV. Só o perfil RH (<c>GrupoRh</c>), só leitura: não existe rota para editar nem apagar evento.
/// Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/20-fatia-1-colaboradores.yaml</c>.
/// </summary>
public static class AuditoriaEndpoints
{
    public static IServiceCollection AdicionarCasosDeUsoDeAuditoria(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarEventosDeAuditoriaCasoDeUso>();
        servicos.AddScoped<ObterEventoDeAuditoriaCasoDeUso>();
        servicos.AddScoped<ExportarAuditoriaCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapAuditoria(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoRh().WithTags("Auditoria");

        grupo.MapGet("/auditoria", Listar)
            .WithName("listarEventosDeAuditoria")
            .Produces<AuditoriaListaResposta>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapGet("/auditoria/exportacao", Exportar)
            .WithName("exportarAuditoria")
            .Produces<string>(StatusCodes.Status200OK, "text/csv")
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapGet("/auditoria/{eventoId:long}", Obter)
            .WithName("obterEventoDeAuditoria")
            .Produces<AuditoriaEventoDetalhe>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<AuditoriaListaResposta> Listar([AsParameters] FiltrosDaListaDeAuditoria filtros, ListarEventosDeAuditoriaCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(filtros.ParaPedido(), filtros.Pagina, filtros.TamanhoPagina, ct);

    private static async Task<AuditoriaEventoDetalhe> Obter(long eventoId, ObterEventoDeAuditoriaCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(eventoId, ct);

    private static async Task<IResult> Exportar([AsParameters] FiltrosDaExportacaoDeAuditoria filtros, ExportarAuditoriaCasoDeUso casoDeUso, HttpContext http, CancellationToken ct)
    {
        var arquivo = await casoDeUso.ExecutarAsync(filtros.ParaPedido(), ct);
        http.Response.Headers.ContentDisposition = $"attachment; filename=\"{arquivo.NomeDoArquivo}\"";
        http.Response.Headers.CacheControl = "no-store"; // o log é dado sensível: nenhum cache guarda o arquivo
        return Results.Bytes(arquivo.Conteudo, "text/csv; charset=utf-8");
    }
}
