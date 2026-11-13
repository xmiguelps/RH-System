using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Treinamentos.Acompanhar;

namespace RHSystem.Api.Treinamentos;

/// <summary>Os parâmetros de <c>GET /api/rh/treinamentos/{treinamentoId}/participantes</c>; <c>Situacao</c>, <c>OrdenarPor</c> e <c>Direcao</c> chegam como texto (o caso de uso os lê e devolve todos os erros juntos num 400 só).</summary>
public sealed record FiltrosDosParticipantes(int Pagina = 1, int TamanhoPagina = 20, string? Busca = null, string? Direcao = null, string? Situacao = null, int? SetorId = null, string? OrdenarPor = null);

/// <summary>Os parâmetros da exportação: os mesmos filtros da lista, sem página nem ordem.</summary>
public sealed record FiltrosDaExportacaoDosParticipantes(string? Busca = null, string? Situacao = null, int? SetorId = null);

/// <summary>
/// "Quem fez" pelo RH (<c>/api/rh/treinamentos/{treinamentoId}/participantes</c>): a lista e o CSV. Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/60-fatia-6-tad.yaml</c>. As duas rotas são só do RH.
/// </summary>
public static class TreinamentosRhAcompanhamentoEndpoints
{
    private const string Etiqueta = "Treinamentos";

    public static IServiceCollection AdicionarCasosDeUsoDoAcompanhamentoDosTreinamentos(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarParticipantesDeTreinamentoCasoDeUso>();
        servicos.AddScoped<ExportarParticipantesDeTreinamentoCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapTreinamentosRhAcompanhamento(this IEndpointRouteBuilder rotas)
    {
        var rh = rotas.GrupoRh().WithTags(Etiqueta);

        rh.MapGet("/treinamentos/{treinamentoId:int}/participantes", Listar)
            .WithName("listarParticipantesDeTreinamento")
            .Produces<TreinamentoParticipantesLista>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        rh.MapGet("/treinamentos/{treinamentoId:int}/participantes/exportacao", Exportar)
            .WithName("exportarParticipantesDeTreinamento")
            .Produces<string>(StatusCodes.Status200OK, "text/csv")
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<TreinamentoParticipantesLista> Listar(int treinamentoId, [AsParameters] FiltrosDosParticipantes f, ListarParticipantesDeTreinamentoCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(treinamentoId, f.Pagina, f.TamanhoPagina, f.Busca, f.Situacao, f.SetorId, f.OrdenarPor, f.Direcao, ct);

    private static async Task<IResult> Exportar(int treinamentoId, [AsParameters] FiltrosDaExportacaoDosParticipantes f, ExportarParticipantesDeTreinamentoCasoDeUso casoDeUso, HttpContext http, CancellationToken ct)
    {
        var arquivo = await casoDeUso.ExecutarAsync(treinamentoId, f.Busca, f.Situacao, f.SetorId, ct);
        http.Response.Headers.ContentDisposition = $"attachment; filename=\"{arquivo.NomeDoArquivo}\"";
        http.Response.Headers.CacheControl = "no-store"; // a lista de quem fez é dado pessoal: nenhum cache guarda o arquivo
        return Results.Bytes(arquivo.Conteudo, "text/csv; charset=utf-8");
    }
}
