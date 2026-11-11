using Microsoft.AspNetCore.Mvc;
using RHSystem.Api.Autorizacao;
using RHSystem.Api.Comum;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Ponto.Atestados;

namespace RHSystem.Api.Ponto;

/// <summary>Os parâmetros da lista dos atestados da PRÓPRIA pessoa e SÓ eles (sem <c>busca</c> nem <c>funcionarioId</c>, que o contrato não declara aqui). <c>Situacao</c> chega como texto: o caso de uso o lê pelo nome do contrato (<c>ENVIADO</c>) e devolve o erro por campo num 400.</summary>
public sealed record FiltrosDaListaDeMeusAtestados(int Pagina = 1, int TamanhoPagina = 20, string? Situacao = null);

/// <summary>Os parâmetros da lista dos atestados da equipe: os da própria lista mais <c>funcionarioId</c> (o gestor só enxerga a equipe dele).</summary>
public sealed record FiltrosDaListaDeAtestadosDaEquipe(int Pagina = 1, int TamanhoPagina = 20, string? Situacao = null, int? FuncionarioId = null);

/// <summary>Os parâmetros da lista dos atestados do RH: a empresa toda, com <c>busca</c> no nome.</summary>
public sealed record FiltrosDaListaDeAtestados(int Pagina = 1, int TamanhoPagina = 20, string? Busca = null, string? Situacao = null, int? FuncionarioId = null);

/// <summary>
/// Os atestados de ponto, pelas três famílias de rota: a própria pessoa (<c>/api/eu/ponto/atestados</c>, qualquer perfil) lista e envia; o gestor (<c>/api/gestor/ponto/atestados</c>) vê e baixa o da equipe dele; o RH (<c>/api/rh/ponto/atestados</c>) lista tudo, baixa, valida e recusa.
/// Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/30-fatia-3-ponto.yaml</c>. O envio recebe <c>multipart/form-data</c> LIDO de <c>HttpRequest</c> (não por <c>[FromForm]</c>, para a autorização rodar antes do 415; o corpo do contrato está em
/// <see cref="FormulariosDoContrato"/>), com <c>DisableAntiforgery</c> (a autenticação é por cabeçalho, sem cookie) e o teto de 16 MiB para a requisição inteira. O arquivo nunca passa pela API no download: a rota devolve um link temporário.
/// </summary>
public static class AtestadosEndpoints
{
    private const string Etiqueta = "Atestados";

    public static IServiceCollection AdicionarCasosDeUsoDeAtestados(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarAtestadosCasoDeUso>();
        servicos.AddScoped<EnviarAtestadoCasoDeUso>();
        servicos.AddScoped<BaixarAtestadoCasoDeUso>();
        servicos.AddScoped<DecidirAtestadoCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapAtestados(this IEndpointRouteBuilder rotas)
    {
        var eu = rotas.GrupoEu().WithTags(Etiqueta);
        eu.MapGet("/ponto/atestados", ListarMeus)
            .WithName("listarMeusAtestados")
            .Produces<RespostaPaginada<PontoAtestado>>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);

        eu.MapPost("/ponto/atestados", Enviar)
            .WithName(FormulariosDoContrato.EnviarAtestado)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(LimitesDeAtestado.TetoDaRequisicaoBytes), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = LimitesDeAtestado.TetoDaRequisicaoBytes })
            .Produces<PontoAtestado>(StatusCodes.Status201Created)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);

        var gestor = rotas.GrupoGestor().WithTags(Etiqueta);
        gestor.MapGet("/ponto/atestados", ListarDaEquipe)
            .WithName("listarAtestadosDaEquipe")
            .Produces<RespostaPaginada<PontoAtestado>>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        gestor.MapGet("/ponto/atestados/{atestadoId:int}/download", BaixarDaEquipe)
            .WithName("baixarAtestadoDaEquipe")
            .Produces<LinkTemporario>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        var rh = rotas.GrupoRh().WithTags(Etiqueta);
        rh.MapGet("/ponto/atestados", ListarParaRh)
            .WithName("listarAtestados")
            .Produces<RespostaPaginada<PontoAtestado>>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        rh.MapGet("/ponto/atestados/{atestadoId:int}/download", BaixarComoRh)
            .WithName("baixarAtestado")
            .Produces<LinkTemporario>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        rh.MapPost("/ponto/atestados/{atestadoId:int}/validar", Validar)
            .WithName("validarAtestado")
            .Produces<PontoAtestado>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status500InternalServerError);

        rh.MapPost("/ponto/atestados/{atestadoId:int}/recusar", Recusar)
            .WithName("recusarAtestado")
            .Produces<PontoAtestado>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<RespostaPaginada<PontoAtestado>> ListarMeus([AsParameters] FiltrosDaListaDeMeusAtestados f, ListarAtestadosCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.MeusAsync(f.Pagina, f.TamanhoPagina, f.Situacao, ct);

    private static async Task<IResult> Enviar(HttpRequest requisicao, EnviarAtestadoCasoDeUso casoDeUso, CancellationToken ct)
    {
        var formulario = await LeituraDeFormulario.LerAsync(requisicao, ct);
        var novo = new NovoAtestado(
            Texto(formulario, "dataInicio"),
            Texto(formulario, "dataFim"),
            Texto(formulario, "horaInicio"),
            Texto(formulario, "horaFim"),
            LeituraDeFormulario.Arquivo(formulario),
            formulario.Files.Count); // conta as partes de arquivo de QUALQUER campo
        return Results.Json(await casoDeUso.ExecutarAsync(novo, ct), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<RespostaPaginada<PontoAtestado>> ListarDaEquipe([AsParameters] FiltrosDaListaDeAtestadosDaEquipe f, ListarAtestadosCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.DaEquipeAsync(f.Pagina, f.TamanhoPagina, f.Situacao, f.FuncionarioId, ct);

    private static async Task<LinkTemporario> BaixarDaEquipe(int atestadoId, BaixarAtestadoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.DaEquipeAsync(atestadoId, ct);

    private static async Task<RespostaPaginada<PontoAtestado>> ListarParaRh([AsParameters] FiltrosDaListaDeAtestados f, ListarAtestadosCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ParaRhAsync(f.Pagina, f.TamanhoPagina, f.Situacao, f.Busca, f.FuncionarioId, ct);

    private static async Task<LinkTemporario> BaixarComoRh(int atestadoId, BaixarAtestadoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ComoRhAsync(atestadoId, ct);

    private static async Task<PontoAtestado> Validar(int atestadoId, DecidirAtestadoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ValidarAsync(atestadoId, ct);

    private static async Task<PontoAtestado> Recusar(int atestadoId, PontoAtestadoRecusar corpo, DecidirAtestadoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.RecusarAsync(atestadoId, corpo, ct);

    private static string? Texto(IFormCollection formulario, string campo) => formulario.TryGetValue(campo, out var valor) ? valor.ToString() : null;
}
