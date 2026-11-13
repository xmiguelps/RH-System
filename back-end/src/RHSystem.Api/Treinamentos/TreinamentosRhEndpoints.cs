using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Treinamentos.Construtor;

namespace RHSystem.Api.Treinamentos;

/// <summary>Os parâmetros de <c>GET /api/rh/treinamentos/publico/contagem</c>. <c>Publico</c> chega como texto (o caso de uso o lê pelo nome do contrato e devolve todos os erros juntos num 400 só); as duas listas se repetem na consulta (<c>setorIds=1&amp;setorIds=2</c>).</summary>
public sealed record FiltrosDaContagemDoPublico(string? Publico = null, int[]? SetorIds = null, int[]? FuncionarioIds = null);

/// <summary>
/// Os treinamentos pelo RH (<c>/api/rh/treinamentos</c>). Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/60-fatia-6-tad.yaml</c>. Esta parte: criar, obter, atualizar e contar o público; as demais rotas do construtor entram nas tarefas seguintes da fatia.
/// </summary>
public static class TreinamentosRhEndpoints
{
    private const string Etiqueta = "Treinamentos";

    public static IServiceCollection AdicionarCasosDeUsoDosTreinamentosDoRh(this IServiceCollection servicos)
    {
        servicos.AddScoped<CriarTreinamentoCasoDeUso>();
        servicos.AddScoped<ObterTreinamentoRhCasoDeUso>();
        servicos.AddScoped<AtualizarTreinamentoCasoDeUso>();
        servicos.AddScoped<ContarPublicoDoTreinamentoCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapTreinamentosRh(this IEndpointRouteBuilder rotas)
    {
        var rh = rotas.GrupoRh().WithTags(Etiqueta);

        rh.MapPost("/treinamentos", Criar)
            .WithName("criarTreinamento")
            .Produces<TreinamentoRh>(StatusCodes.Status201Created)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        rh.MapGet("/treinamentos/publico/contagem", ContarPublico)
            .WithName("contarPublicoDeTreinamento")
            .Produces<TreinamentoPublicoContagem>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        rh.MapGet("/treinamentos/{treinamentoId:int}", Obter)
            .WithName("obterTreinamento")
            .Produces<TreinamentoRh>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        rh.MapPut("/treinamentos/{treinamentoId:int}", Atualizar)
            .WithName("atualizarTreinamento")
            .Produces<TreinamentoRh>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<IResult> Criar(TreinamentoComando corpo, CriarTreinamentoCasoDeUso casoDeUso, CancellationToken ct) => Results.Json(await casoDeUso.ExecutarAsync(corpo, ct), statusCode: StatusCodes.Status201Created);

    private static async Task<TreinamentoPublicoContagem> ContarPublico([AsParameters] FiltrosDaContagemDoPublico f, ContarPublicoDoTreinamentoCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Publico, f.SetorIds, f.FuncionarioIds, ct);

    private static async Task<TreinamentoRh> Obter(int treinamentoId, ObterTreinamentoRhCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(treinamentoId, ct);

    private static async Task<TreinamentoRh> Atualizar(int treinamentoId, TreinamentoComando corpo, AtualizarTreinamentoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(treinamentoId, corpo, ct);
}
