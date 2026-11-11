using RHSystem.Api.Autorizacao;
using RHSystem.Api.Comum;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Ponto.Feriados;

namespace RHSystem.Api.Ponto;

/// <summary>Os parâmetros da lista de feriados. <c>Ano</c> é obrigatório no contrato (de 2000 a 2100): ausente é o 400 do próprio vínculo dos parâmetros e fora do intervalo é o 400 em <c>ano</c> do caso de uso.</summary>
public sealed record FiltrosDaListaDeFeriados(int Ano, int? Mes = null, bool IncluirInativos = false);

/// <summary>
/// Os feriados do ponto (<c>/api/rh/ponto/feriados</c>, só o RH): lista, cadastro do municipal e edição. Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/30-fatia-3-ponto.yaml</c>.
/// </summary>
public static class PontoFeriadosEndpoints
{
    private const string Etiqueta = "Feriados";

    public static IServiceCollection AdicionarCasosDeUsoDeFeriados(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarFeriadosCasoDeUso>();
        servicos.AddScoped<CriarFeriadoCasoDeUso>();
        servicos.AddScoped<EditarFeriadoCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapFeriados(this IEndpointRouteBuilder rotas)
    {
        var rh = rotas.GrupoRh().WithTags(Etiqueta);
        rh.MapGet("/ponto/feriados", Listar)
            .WithName("listarFeriados")
            .Produces<PontoFeriadoLista>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        rh.MapPost("/ponto/feriados", Criar)
            .WithName("criarFeriado")
            .Produces<PontoFeriado>(StatusCodes.Status201Created)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        rh.MapPut("/ponto/feriados/{feriadoId:int}", Editar)
            .WithName("editarFeriado")
            .Produces<PontoFeriado>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<PontoFeriadoLista> Listar([AsParameters] FiltrosDaListaDeFeriados f, ListarFeriadosCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(f.Ano, f.Mes, f.IncluirInativos, ct);

    private static async Task<IResult> Criar(PontoFeriadoCriar corpo, CriarFeriadoCasoDeUso casoDeUso, CancellationToken ct) => Results.Json(await casoDeUso.ExecutarAsync(corpo, ct), statusCode: StatusCodes.Status201Created);

    private static async Task<PontoFeriado> Editar(int feriadoId, PontoFeriadoEditar corpo, EditarFeriadoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(feriadoId, corpo, ct);
}
