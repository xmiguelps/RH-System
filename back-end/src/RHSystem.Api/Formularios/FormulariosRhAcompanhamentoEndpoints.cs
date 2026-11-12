using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Formularios.Acompanhar;

namespace RHSystem.Api.Formularios;

/// <summary>Os parâmetros de <c>GET /api/rh/formularios/{id}/respondentes</c>. <c>Situacao</c> chega como texto (o caso de uso o lê pelo nome do contrato e devolve todos os erros juntos num 400 só).</summary>
public sealed record FiltrosDosRespondentes(int Pagina = 1, int TamanhoPagina = 20, string? Busca = null, string? Situacao = null, int? SetorId = null);

/// <summary>Os parâmetros de <c>GET /api/rh/formularios/{id}/respostas/funcionarios/{funcionarioId}</c>: o avaliado (nas avaliações) e o instante do envio (na Sugestão).</summary>
public sealed record FiltrosDasRespostasDeFuncionario(int? AvaliadoId = null, DateTime? RespondidoEmUtc = null);

/// <summary>
/// Quem respondeu e ver respostas, só o RH (<c>/api/rh/formularios/{id}/...</c>). Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/50-fatia-5-formularios.yaml</c>.
/// </summary>
public static class FormulariosRhAcompanhamentoEndpoints
{
    private const string Etiqueta = "Formulários";

    public static IServiceCollection AdicionarCasosDeUsoDoAcompanhamentoDosFormularios(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarRespondentesDoFormularioCasoDeUso>();
        servicos.AddScoped<ObterRespostasDeFuncionarioCasoDeUso>();
        servicos.AddScoped<BaixarArquivoDeRespostaCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapFormulariosRhAcompanhamento(this IEndpointRouteBuilder rotas)
    {
        var rh = rotas.GrupoRh().WithTags(Etiqueta);

        rh.MapGet("/formularios/{formularioId:int}/respondentes", Respondentes)
            .WithName("listarRespondentesFormulario")
            .Produces<FormularioRespondentesRh>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        rh.MapGet("/formularios/{formularioId:int}/respostas/funcionarios/{funcionarioId:int}", RespostasDeFuncionario)
            .WithName("obterRespostasDeFuncionario")
            .Produces<RespostaFormularioFuncionario>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        rh.MapGet("/formularios/{formularioId:int}/arquivos/{respostaId:int}/download", BaixarArquivo)
            .WithName("baixarArquivoDeResposta")
            .Produces<LinkTemporario>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<FormularioRespondentesRh> Respondentes(int formularioId, [AsParameters] FiltrosDosRespondentes f, ListarRespondentesDoFormularioCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(formularioId, f.Pagina, f.TamanhoPagina, f.Busca, f.Situacao, f.SetorId, ct);

    private static async Task<RespostaFormularioFuncionario> RespostasDeFuncionario(int formularioId, int funcionarioId, [AsParameters] FiltrosDasRespostasDeFuncionario f, ObterRespostasDeFuncionarioCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(formularioId, funcionarioId, f.AvaliadoId, f.RespondidoEmUtc, ct);

    private static async Task<LinkTemporario> BaixarArquivo(int formularioId, int respostaId, BaixarArquivoDeRespostaCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, respostaId, ct);
}
