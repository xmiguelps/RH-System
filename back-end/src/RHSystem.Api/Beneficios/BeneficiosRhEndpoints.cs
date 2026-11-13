using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Beneficios.Adesoes;
using RHSystem.Application.Comum.Consultas;

namespace RHSystem.Api.Beneficios;

/// <summary>Os parâmetros da lista de adesões do RH e SÓ eles (<c>Situacao</c> chega como texto: o caso de uso o lê pelo nome do contrato e responde 400 com o erro por campo).</summary>
public sealed record FiltrosDaListaDeAdesoesDoRh(int Pagina = 1, int TamanhoPagina = 20, string? Busca = null, string? Situacao = null, int? BeneficioId = null);

/// <summary>
/// Os pedidos de benefício para o RH (<c>/api/rh/beneficios-adesoes</c>, só o perfil RH, porque tem dado pessoal): a lista e as seis decisões (aprovar e recusar a adesão, o dependente e o trajeto). Os códigos de resposta são os de <c>contrato/fontes/70-fatia-7-gestao.yaml</c>. As rotas com id usam <c>:int</c>.
/// </summary>
public static class BeneficiosRhEndpoints
{
    private const string Etiqueta = "Benefícios";

    public static IServiceCollection AdicionarCasosDeUsoDosBeneficiosParaORh(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarPedidosDeBeneficioCasoDeUso>();
        servicos.AddScoped<DecidirPedidoDeBeneficioCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapBeneficiosRh(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoRh().WithTags(Etiqueta);

        grupo.MapGet("/beneficios-adesoes", Listar)
            .WithName("listarPedidosDeBeneficio")
            .Produces<RespostaPaginada<AdesaoParaRhDto>>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/beneficios-adesoes/{adesaoId:int}/aprovar", AprovarAdesao)
            .WithName("aprovarAdesaoABeneficio")
            .Produces<AdesaoParaRhDto>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/beneficios-adesoes/{adesaoId:int}/recusar", RecusarAdesao)
            .WithName("recusarAdesaoABeneficio")
            .Produces<AdesaoParaRhDto>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/beneficios-adesoes/{adesaoId:int}/dependentes/{dependenteId:int}/aprovar", AprovarDependente)
            .WithName("aprovarDependenteDeBeneficio")
            .Produces<AdesaoParaRhDto>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/beneficios-adesoes/{adesaoId:int}/dependentes/{dependenteId:int}/recusar", RecusarDependente)
            .WithName("recusarDependenteDeBeneficio")
            .Produces<AdesaoParaRhDto>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/beneficios-adesoes/{adesaoId:int}/trajetos/{trajetoId:int}/aprovar", AprovarTrajeto)
            .WithName("aprovarTrajetoDeBeneficio")
            .Produces<AdesaoParaRhDto>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/beneficios-adesoes/{adesaoId:int}/trajetos/{trajetoId:int}/recusar", RecusarTrajeto)
            .WithName("recusarTrajetoDeBeneficio")
            .Produces<AdesaoParaRhDto>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<RespostaPaginada<AdesaoParaRhDto>> Listar([AsParameters] FiltrosDaListaDeAdesoesDoRh f, ListarPedidosDeBeneficioCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, f.Busca, f.Situacao, f.BeneficioId, ct);

    private static async Task<AdesaoParaRhDto> AprovarAdesao(int adesaoId, DecidirPedidoDeBeneficioCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.AprovarAdesaoAsync(adesaoId, ct);

    private static async Task<AdesaoParaRhDto> RecusarAdesao(int adesaoId, AdesaoRecusarComando corpo, DecidirPedidoDeBeneficioCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.RecusarAdesaoAsync(adesaoId, corpo, ct);

    private static async Task<AdesaoParaRhDto> AprovarDependente(int adesaoId, int dependenteId, DecidirPedidoDeBeneficioCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.AprovarDependenteAsync(adesaoId, dependenteId, ct);

    private static async Task<AdesaoParaRhDto> RecusarDependente(int adesaoId, int dependenteId, AdesaoRecusarComando corpo, DecidirPedidoDeBeneficioCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.RecusarDependenteAsync(adesaoId, dependenteId, corpo, ct);

    private static async Task<AdesaoParaRhDto> AprovarTrajeto(int adesaoId, int trajetoId, DecidirPedidoDeBeneficioCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.AprovarTrajetoAsync(adesaoId, trajetoId, ct);

    private static async Task<AdesaoParaRhDto> RecusarTrajeto(int adesaoId, int trajetoId, AdesaoRecusarComando corpo, DecidirPedidoDeBeneficioCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.RecusarTrajetoAsync(adesaoId, trajetoId, corpo, ct);
}
