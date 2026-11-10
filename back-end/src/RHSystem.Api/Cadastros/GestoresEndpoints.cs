using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Gestores;

namespace RHSystem.Api.Cadastros;

/// <summary>
/// "Gestores" (tela 29): lista, cadastra quem do RH decide os pedidos de gestores e liga e desliga "Gestor ausente". Só o perfil RH (<c>GrupoRh</c>). Os <c>operationId</c> e os códigos de resposta são os de
/// <c>contrato/fontes/20-fatia-1-colaboradores.yaml</c>.
/// </summary>
public static class GestoresEndpoints
{
    private const string Etiqueta = "Cadastros de apoio";

    public static IServiceCollection AdicionarCasosDeUsoDeGestores(this IServiceCollection servicos)
    {
        servicos.AddScoped<GestoresCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapGestores(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoRh().WithTags(Etiqueta);

        grupo.MapGet("/gestores", Listar)
            .WithName("listarGestores")
            .Produces<GestorListaResposta>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/gestores", Cadastrar)
            .WithName("cadastrarGestor")
            .Produces<GestorItem>(StatusCodes.Status201Created)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        grupo.MapPut("/gestores/{gestorId:int}/ausencia", ConfigurarAusencia)
            .WithName("configurarAusenciaDoGestor")
            .Produces<GestorAusenciaResposta>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<GestorListaResposta> Listar(GestoresCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ListarAsync(ct);

    private static async Task<IResult> Cadastrar(GestorCadastrarComando comando, GestoresCasoDeUso casoDeUso, CancellationToken ct)
    {
        var gestor = await casoDeUso.CadastrarAsync(comando, ct);
        return Results.Created($"/api/rh/gestores/{gestor.GestorId}", gestor);
    }

    private static async Task<GestorAusenciaResposta> ConfigurarAusencia(int gestorId, GestorAusenciaComando comando, GestoresCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ConfigurarAusenciaAsync(gestorId, comando, ct);
}
