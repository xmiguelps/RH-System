using RHSystem.Api.Autorizacao;
using RHSystem.Api.Comum;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comunicacao.Avisos;

namespace RHSystem.Api.Comunicacao;

/// <summary>
/// Os parâmetros da lista de avisos do RH. <c>Situacao</c> e <c>Tipo</c> chegam como texto: o caso de uso os lê pelo nome do contrato e devolve o 400 certo (<c>errors.situacao</c>, <c>errors.tipo</c>). A lista não tem <c>ordenarPor</c>: a ordem é a da aba.
/// </summary>
/// <summary>Os parâmetros da lista "Quem viu": a página e <c>situacao</c> (<c>LIDO</c> ou <c>NAO_LIDO</c>, em texto: o caso de uso o lê pelo nome do contrato e devolve o 400 em <c>errors.situacao</c>).</summary>
public sealed record FiltrosDasLeiturasDoAviso(int Pagina = 1, int TamanhoPagina = 20, string? Situacao = null);

public sealed record FiltrosDaListaDeAvisos(int Pagina = 1, int TamanhoPagina = 20, string? Busca = null, string? Situacao = null, string? Tipo = null);

/// <summary>
/// Os avisos pelo RH (<c>/api/rh/avisos</c>): listar, criar, resumo, obter, editar, excluir, publicar e encerrar. Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/40-fatia-4-comunicacao-e-documentos.yaml</c>. As rotas
/// <c>/resumo</c> e <c>/{avisoId:int}</c> não colidem porque o id é restrito a inteiro.
/// </summary>
public static class AvisosRhEndpoints
{
    private const string Etiqueta = "Avisos";

    public static IServiceCollection AdicionarCasosDeUsoDeAvisosDoRh(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarAvisosDoRhCasoDeUso>();
        servicos.AddScoped<ObterResumoDosAvisosCasoDeUso>();
        servicos.AddScoped<ObterAvisoDoRhCasoDeUso>();
        servicos.AddScoped<CriarAvisoCasoDeUso>();
        servicos.AddScoped<EditarAvisoCasoDeUso>();
        servicos.AddScoped<ExcluirAvisoCasoDeUso>();
        servicos.AddScoped<PublicarAvisoCasoDeUso>();
        servicos.AddScoped<EncerrarAvisoCasoDeUso>();
        servicos.AddScoped<ListarLeiturasDoAvisoCasoDeUso>();
        servicos.AddScoped<LembrarQuemNaoViuAvisoCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapAvisosRh(this IEndpointRouteBuilder rotas)
    {
        var rh = rotas.GrupoRh().WithTags(Etiqueta);

        rh.MapGet("/avisos", Listar)
            .WithName("listarAvisos")
            .Produces<RespostaPaginada<AvisoItemRh>>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        rh.MapPost("/avisos", Criar)
            .WithName("criarAviso")
            .Produces<AvisoDetalheRh>(StatusCodes.Status201Created)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        rh.MapGet("/avisos/resumo", Resumo)
            .WithName("obterResumoDosAvisos")
            .Produces<AvisoPainelRh>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        rh.MapGet("/avisos/{avisoId:int}", Obter)
            .WithName("obterAviso")
            .Produces<AvisoDetalheRh>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        rh.MapPut("/avisos/{avisoId:int}", Editar)
            .WithName("editarAviso")
            .Produces<AvisoDetalheRh>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        rh.MapDelete("/avisos/{avisoId:int}", Excluir)
            .WithName("excluirAviso")
            .Produces(StatusCodes.Status204NoContent)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        rh.MapPost("/avisos/{avisoId:int}/publicar", Publicar)
            .WithName("publicarAviso")
            .Produces<AvisoDetalheRh>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        rh.MapPost("/avisos/{avisoId:int}/encerrar", Encerrar)
            .WithName("encerrarAviso")
            .Produces<AvisoDetalheRh>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        rh.MapGet("/avisos/{avisoId:int}/leituras", ListarLeituras)
            .WithName("listarLeiturasDoAviso")
            .Produces<AvisoLeituras>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        rh.MapPost("/avisos/{avisoId:int}/lembretes", Lembrar)
            .WithName("lembrarQuemNaoViuAviso")
            .Produces<AvisoLembreteResultado>(StatusCodes.Status202Accepted)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<AvisoLeituras> ListarLeituras(int avisoId, [AsParameters] FiltrosDasLeiturasDoAviso f, ListarLeiturasDoAvisoCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(avisoId, f.Pagina, f.TamanhoPagina, f.Situacao, ct);

    private static async Task<IResult> Lembrar(int avisoId, LembrarQuemNaoViuAvisoCasoDeUso casoDeUso, CancellationToken ct) => Results.Json(await casoDeUso.ExecutarAsync(avisoId, ct), statusCode: StatusCodes.Status202Accepted);

    private static async Task<RespostaPaginada<AvisoItemRh>> Listar([AsParameters] FiltrosDaListaDeAvisos f, ListarAvisosDoRhCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, f.Busca, f.Situacao, f.Tipo, ct);

    private static async Task<IResult> Criar(AvisoCriacaoComando corpo, CriarAvisoCasoDeUso casoDeUso, CancellationToken ct) => Results.Json(await casoDeUso.ExecutarAsync(corpo, ct), statusCode: StatusCodes.Status201Created);

    private static async Task<AvisoPainelRh> Resumo(ObterResumoDosAvisosCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(ct);

    private static async Task<AvisoDetalheRh> Obter(int avisoId, ObterAvisoDoRhCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(avisoId, ct);

    private static async Task<AvisoDetalheRh> Editar(int avisoId, AvisoEdicaoComando corpo, EditarAvisoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(avisoId, corpo, ct);

    private static async Task<IResult> Excluir(int avisoId, ExcluirAvisoCasoDeUso casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(avisoId, ct);
        return Results.NoContent();
    }

    private static async Task<AvisoDetalheRh> Publicar(int avisoId, PublicarAvisoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(avisoId, ct);

    private static async Task<AvisoDetalheRh> Encerrar(int avisoId, EncerrarAvisoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(avisoId, ct);
}
