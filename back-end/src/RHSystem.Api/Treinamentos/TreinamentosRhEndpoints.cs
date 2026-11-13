using Microsoft.AspNetCore.Mvc;
using RHSystem.Api.Autorizacao;
using RHSystem.Api.Comum;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Documentos;
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
        servicos.AddScoped<AdicionarModuloDeTreinamentoCasoDeUso>();
        servicos.AddScoped<ReordenarModulosDeTreinamentoCasoDeUso>();
        servicos.AddScoped<AtualizarModuloDeTreinamentoCasoDeUso>();
        servicos.AddScoped<ExcluirModuloDeTreinamentoCasoDeUso>();
        servicos.AddScoped<PublicarTreinamentoCasoDeUso>();
        servicos.AddScoped<EncerrarTreinamentoCasoDeUso>();
        servicos.AddScoped<ExcluirTreinamentoCasoDeUso>();
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

        // O corpo do módulo é multipart (escrito à mão no documento por FormulariosDoContrato); o handler lê o formulário de HttpRequest e responde 415 se o corpo não é multipart.
        rh.MapPost("/treinamentos/{treinamentoId:int}/modulos", AdicionarModulo)
            .WithName("adicionarModuloDeTreinamento")
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(LimitesDoModulo.TetoDaRequisicaoBytes), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = LimitesDoModulo.TetoDaRequisicaoBytes })
            .Produces<TreinamentoModuloRh>(StatusCodes.Status201Created)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        rh.MapPut("/treinamentos/{treinamentoId:int}/modulos/ordem", ReordenarModulos)
            .WithName("reordenarModulosDeTreinamento")
            .Produces<TreinamentoModulosRh>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        rh.MapPut("/treinamentos/{treinamentoId:int}/modulos/{moduloId:int}", AtualizarModulo)
            .WithName("atualizarModuloDeTreinamento")
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(LimitesDoModulo.TetoDaRequisicaoBytes), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = LimitesDoModulo.TetoDaRequisicaoBytes })
            .Produces<TreinamentoModuloRh>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        rh.MapDelete("/treinamentos/{treinamentoId:int}/modulos/{moduloId:int}", ExcluirModulo)
            .WithName("excluirModuloDeTreinamento")
            .Produces(StatusCodes.Status204NoContent)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        rh.MapPost("/treinamentos/{treinamentoId:int}/publicar", Publicar)
            .WithName("publicarTreinamento")
            .Produces<TreinamentoRh>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        rh.MapPost("/treinamentos/{treinamentoId:int}/encerrar", Encerrar)
            .WithName("encerrarTreinamento")
            .Produces<TreinamentoRh>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        rh.MapDelete("/treinamentos/{treinamentoId:int}", Excluir)
            .WithName("excluirTreinamento")
            .Produces(StatusCodes.Status204NoContent)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<IResult> Criar(TreinamentoComando corpo, CriarTreinamentoCasoDeUso casoDeUso, CancellationToken ct) => Results.Json(await casoDeUso.ExecutarAsync(corpo, ct), statusCode: StatusCodes.Status201Created);

    private static async Task<TreinamentoPublicoContagem> ContarPublico([AsParameters] FiltrosDaContagemDoPublico f, ContarPublicoDoTreinamentoCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Publico, f.SetorIds, f.FuncionarioIds, ct);

    private static async Task<TreinamentoRh> Obter(int treinamentoId, ObterTreinamentoRhCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(treinamentoId, ct);

    private static async Task<TreinamentoRh> Publicar(int treinamentoId, PublicarTreinamentoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(treinamentoId, ct);

    private static async Task<TreinamentoRh> Encerrar(int treinamentoId, bool? confirmar, EncerrarTreinamentoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(treinamentoId, confirmar ?? false, ct);

    private static async Task<IResult> Excluir(int treinamentoId, bool? confirmar, ExcluirTreinamentoCasoDeUso casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(treinamentoId, confirmar ?? false, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> AdicionarModulo(int treinamentoId, HttpRequest requisicao, AdicionarModuloDeTreinamentoCasoDeUso casoDeUso, CancellationToken ct) =>
        Results.Json(await casoDeUso.ExecutarAsync(treinamentoId, await LerModuloAsync(requisicao, ct), ct), statusCode: StatusCodes.Status201Created);

    private static async Task<TreinamentoModulosRh> ReordenarModulos(int treinamentoId, TreinamentoModulosOrdemComando corpo, ReordenarModulosDeTreinamentoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(treinamentoId, corpo, ct);

    private static async Task<TreinamentoModuloRh> AtualizarModulo(int treinamentoId, int moduloId, HttpRequest requisicao, AtualizarModuloDeTreinamentoCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(treinamentoId, moduloId, await LerModuloAsync(requisicao, ct), ct);

    private static async Task<IResult> ExcluirModulo(int treinamentoId, int moduloId, ExcluirModuloDeTreinamentoCasoDeUso casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExecutarAsync(treinamentoId, moduloId, ct);
        return Results.NoContent();
    }

    /// <summary>O módulo como chegou: os campos de texto crus e o arquivo (a parte <c>arquivo</c>); outro corpo que não é multipart é 415.</summary>
    private static async Task<NovoModulo> LerModuloAsync(HttpRequest requisicao, CancellationToken ct)
    {
        var formulario = await LeituraDeFormulario.LerAsync(requisicao, ct);
        var enviado = formulario.Files.GetFile("arquivo");
        var arquivo = enviado is null ? null : new ArquivoRecebido(enviado.FileName, enviado.Length, enviado.OpenReadStream);
        return new NovoModulo(Texto(formulario, "titulo"), Texto(formulario, "tipo"), Texto(formulario, "duracaoMinutos"), Texto(formulario, "urlExterna"), Texto(formulario, "conteudoTexto"), arquivo, formulario.Files.Count);
    }

    private static string? Texto(IFormCollection formulario, string campo) => formulario.TryGetValue(campo, out var valor) ? valor.ToString() : null;

    private static async Task<TreinamentoRh> Atualizar(int treinamentoId, TreinamentoComando corpo, AtualizarTreinamentoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(treinamentoId, corpo, ct);
}
