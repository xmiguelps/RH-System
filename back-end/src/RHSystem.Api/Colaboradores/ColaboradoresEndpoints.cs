using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Consultas;

namespace RHSystem.Api.Colaboradores;

/// <summary>
/// Os parâmetros de <c>GET /api/rh/funcionarios</c> (os cinco de lista e os filtros dela). <c>Situacao</c>, <c>TipoContrato</c>, <c>OrdenarPor</c> e <c>Direcao</c> chegam como texto (o binder do minimal API só entende o nome do C#, <c>Asc</c>, e o contrato manda <c>ASC</c>): o caso de uso os lê pelo nome do
/// contrato (<c>EM_ADMISSAO</c>, <c>DATA_ADMISSAO</c>) e devolve todos os erros juntos num 400 só.
/// </summary>
public sealed record FiltrosDaListaDeFuncionarios(
    int Pagina = 1,
    int TamanhoPagina = 20,
    string? Busca = null,
    string? Situacao = null,
    int? SetorId = null,
    string? TipoContrato = null,
    string? OrdenarPor = null,
    string? Direcao = null);

/// <summary>
/// O cadastro de colaboradores pelo RH: lista (tela 01), ficha e indicadores (tela 03). Só o perfil RH (<c>GrupoRh</c>). Os <c>operationId</c> e os códigos de resposta são os de
/// <c>contrato/fontes/20-fatia-1-colaboradores.yaml</c>.
/// </summary>
public static class ColaboradoresEndpoints
{
    private const string Etiqueta = "Colaboradores";

    public static IServiceCollection AdicionarCasosDeUsoDeColaboradores(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarFuncionariosCasoDeUso>();
        servicos.AddScoped<CriarFuncionarioCasoDeUso>();
        servicos.AddScoped<EditarFuncionarioCasoDeUso>();
        servicos.AddScoped<ObterFichaDoFuncionarioCasoDeUso>();
        servicos.AddScoped<ObterIndicadoresDoFuncionarioCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapColaboradores(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoRh().WithTags(Etiqueta);

        grupo.MapGet("/funcionarios", Listar)
            .WithName("listarFuncionarios")
            .Produces<FuncionarioListaRespostaRh>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/funcionarios", Criar)
            .WithName("criarFuncionario")
            .Produces<FuncionarioFichaRh>(StatusCodes.Status201Created)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        grupo.MapPut("/funcionarios/{funcionarioId:int}", Editar)
            .WithName("editarFuncionario")
            .Produces<FuncionarioFichaRh>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        grupo.MapGet("/funcionarios/{funcionarioId:int}", ObterFicha)
            .WithName("obterFichaDoFuncionario")
            .Produces<FuncionarioFichaRh>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        grupo.MapGet("/funcionarios/{funcionarioId:int}/indicadores", ObterIndicadores)
            .WithName("obterIndicadoresDoFuncionario")
            .Produces<FuncionarioIndicadoresRh>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<FuncionarioListaRespostaRh> Listar([AsParameters] FiltrosDaListaDeFuncionarios f, ListarFuncionariosCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, f.Busca, f.Situacao, f.SetorId, f.TipoContrato, f.OrdenarPor, f.Direcao, ct);

    private static async Task<IResult> Criar(FuncionarioCriarComando comando, CriarFuncionarioCasoDeUso casoDeUso, CancellationToken ct)
    {
        var ficha = await casoDeUso.ExecutarAsync(comando, ct);
        return Results.Created($"/api/rh/funcionarios/{ficha.Id}", ficha);
    }

    private static async Task<FuncionarioFichaRh> Editar(int funcionarioId, FuncionarioEditarComando comando, EditarFuncionarioCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(funcionarioId, comando, ct);

    private static async Task<FuncionarioFichaRh> ObterFicha(int funcionarioId, ObterFichaDoFuncionarioCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(funcionarioId, ct);

    private static async Task<FuncionarioIndicadoresRh> ObterIndicadores(int funcionarioId, ObterIndicadoresDoFuncionarioCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(funcionarioId, ct);
}
