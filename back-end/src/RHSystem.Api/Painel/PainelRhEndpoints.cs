using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Painel;

namespace RHSystem.Api.Painel;

/// <summary>Os parâmetros da lista de pendências do RH e SÓ eles (<c>Tipo</c> chega como texto: o caso de uso o lê pelo nome do contrato e responde 400 com o erro por campo).</summary>
public sealed record FiltrosDasPendenciasDoRh(int Pagina = 1, int TamanhoPagina = 20, string? Tipo = null);

/// <summary>Os parâmetros da lista de pessoas sem registro: só a paginação.</summary>
public sealed record FiltrosDaPresencaSemRegistro(int Pagina = 1, int TamanhoPagina = 20);

/// <summary>O painel inicial do RH e as duas listas dele (<c>/api/rh/painel</c>, só o perfil RH): as pendências uma a uma e as pessoas sem registro de ponto hoje. Os códigos de resposta são os do contrato.</summary>
public static class PainelRhEndpoints
{
    private const string Etiqueta = "Painéis";

    public static IServiceCollection AdicionarCasosDeUsoDoPainelDoRh(this IServiceCollection servicos)
    {
        servicos.AddScoped<ColetorDePendenciasDoRh>();
        servicos.AddScoped<ObterPainelDoRhCasoDeUso>();
        servicos.AddScoped<ListarPendenciasDoRhCasoDeUso>();
        servicos.AddScoped<ListarPessoasSemRegistroHojeCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapPainelDoRh(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoRh().WithTags(Etiqueta);

        grupo.MapGet("/painel", Obter)
            .WithName("obterPainelDoRh")
            .Produces<PainelRhDto>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapGet("/painel/pendencias", ListarPendencias)
            .WithName("listarPendenciasDoRh")
            .Produces<RespostaPaginada<PainelPendenciaItemDto>>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapGet("/painel/presenca/sem-registro", ListarSemRegistro)
            .WithName("listarPessoasSemRegistroHoje")
            .Produces<RespostaPaginada<PainelSemRegistroItemDto>>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<PainelRhDto> Obter(ObterPainelDoRhCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(ct);

    private static async Task<RespostaPaginada<PainelPendenciaItemDto>> ListarPendencias([AsParameters] FiltrosDasPendenciasDoRh f, ListarPendenciasDoRhCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, f.Tipo, ct);

    private static async Task<RespostaPaginada<PainelSemRegistroItemDto>> ListarSemRegistro([AsParameters] FiltrosDaPresencaSemRegistro f, ListarPessoasSemRegistroHojeCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, ct);
}
