using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Cadastros;
using RHSystem.Application.Comum.Consultas;

namespace RHSystem.Api.Cadastros;

/// <summary>
/// "Cargos" (tela 29): lista, cria e edita (não há exclusão). Só o perfil RH (<c>GrupoRh</c>). Os <c>operationId</c> e os códigos de resposta são os de
/// <c>contrato/fontes/20-fatia-1-colaboradores.yaml</c>.
/// </summary>
public static class CargosEndpoints
{
    private const string Etiqueta = "Cadastros de apoio";

    public static IServiceCollection AdicionarCasosDeUsoDeCargos(this IServiceCollection servicos)
    {
        servicos.AddScoped<CargosCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapCargos(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoRh().WithTags(Etiqueta);

        grupo.MapGet("/cargos", Listar)
            .WithName("listarCargos")
            .Produces<CargoListaResposta>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/cargos", Criar)
            .WithName("criarCargo")
            .Produces<CargoItem>(StatusCodes.Status201Created)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status409Conflict, StatusCodes.Status500InternalServerError);

        grupo.MapPut("/cargos/{cargoId:int}", Editar)
            .WithName("editarCargo")
            .Produces<CargoItem>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<CargoListaResposta> Listar([AsParameters] FiltrosDaListaDeCadastros f, CargosCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ListarAsync(new ParametrosDeLista(f.Pagina, f.TamanhoPagina, f.Busca), ct);

    private static async Task<IResult> Criar(CargoComando comando, CargosCasoDeUso casoDeUso, CancellationToken ct)
    {
        var cargo = await casoDeUso.CriarAsync(comando, ct);
        return Results.Created($"/api/rh/cargos/{cargo.Id}", cargo);
    }

    private static async Task<CargoItem> Editar(int cargoId, CargoComando comando, CargosCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.EditarAsync(cargoId, comando, ct);
}
