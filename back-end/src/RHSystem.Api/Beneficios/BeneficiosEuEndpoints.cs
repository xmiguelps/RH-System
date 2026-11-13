using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Beneficios.Adesoes;
using RHSystem.Application.Beneficios.Adesoes.Regras;

namespace RHSystem.Api.Beneficios;

/// <summary>
/// Os benefícios da PRÓPRIA pessoa logada (<c>/api/eu/beneficios</c> e <c>/api/eu/beneficios-adesoes</c>, qualquer perfil; o gestor usa as mesmas rotas para os dele e nunca vê dependente nem trajeto da equipe): a tela com os cartões, os disponíveis e os pedidos, e os três pedidos (adesão, dependente
/// e trajeto). Quem responde é o RH, em <c>/api/rh/beneficios-adesoes</c>.
/// </summary>
public static class BeneficiosEuEndpoints
{
    private const string Etiqueta = "Benefícios";

    public static IServiceCollection AdicionarCasosDeUsoDosMeusBeneficios(this IServiceCollection servicos)
    {
        servicos.AddScoped<ObterMeusBeneficiosCasoDeUso>();
        servicos.AddScoped<PedirAdesaoABeneficioCasoDeUso>();
        servicos.AddScoped<PedirInclusaoDeDependenteCasoDeUso>();
        servicos.AddScoped<PedirAtualizacaoDeTrajetoCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapBeneficiosEu(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoEu().WithTags(Etiqueta);

        grupo.MapGet("/beneficios", Obter)
            .WithName("obterMeusBeneficios")
            .Produces<BeneficiosEuResposta>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/beneficios-adesoes", PedirAdesao)
            .WithName("pedirAdesaoABeneficio")
            .Produces<ResumoDoPedido>(StatusCodes.Status201Created)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/beneficios-adesoes/{adesaoId:int}/dependentes", PedirDependente)
            .WithName("pedirInclusaoDeDependente")
            .Produces<ResumoDoPedido>(StatusCodes.Status201Created)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/beneficios-adesoes/{adesaoId:int}/trajetos", PedirTrajeto)
            .WithName("pedirAtualizacaoDeTrajeto")
            .Produces<ResumoDoPedido>(StatusCodes.Status201Created)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<BeneficiosEuResposta> Obter(ObterMeusBeneficiosCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(ct);

    private static async Task<IResult> PedirAdesao(AdesaoPedirComando corpo, PedirAdesaoABeneficioCasoDeUso casoDeUso, CancellationToken ct) =>
        Results.Created("/api/eu/beneficios", await casoDeUso.ExecutarAsync(corpo, ct));

    private static async Task<IResult> PedirDependente(int adesaoId, DadosDoDependente corpo, PedirInclusaoDeDependenteCasoDeUso casoDeUso, CancellationToken ct) =>
        Results.Created("/api/eu/beneficios", await casoDeUso.ExecutarAsync(adesaoId, corpo, ct));

    private static async Task<IResult> PedirTrajeto(int adesaoId, DadosDoTrajeto corpo, PedirAtualizacaoDeTrajetoCasoDeUso casoDeUso, CancellationToken ct) =>
        Results.Created("/api/eu/beneficios", await casoDeUso.ExecutarAsync(adesaoId, corpo, ct));
}
