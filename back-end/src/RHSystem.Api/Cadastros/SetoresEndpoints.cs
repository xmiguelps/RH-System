using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Cadastros;
using RHSystem.Application.Comum.Consultas;

namespace RHSystem.Api.Cadastros;

/// <summary>
/// "Setores" (tela 29): lista, cria e edita (não há exclusão), com a troca de gestor do setor. Só o perfil RH (<c>GrupoRh</c>). Os <c>operationId</c> e os códigos de resposta são os de
/// <c>contrato/fontes/20-fatia-1-colaboradores.yaml</c>.
/// </summary>
public static class SetoresEndpoints
{
    private const string Etiqueta = "Cadastros de apoio";

    public static IServiceCollection AdicionarCasosDeUsoDeSetores(this IServiceCollection servicos)
    {
        servicos.AddScoped<SetoresCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapSetores(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoRh().WithTags(Etiqueta);

        grupo.MapGet("/setores", Listar)
            .WithName("listarSetores")
            .Produces<SetorListaResposta>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/setores", Criar)
            .WithName("criarSetor")
            .Produces<SetorItem>(StatusCodes.Status201Created)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        grupo.MapPut("/setores/{setorId:int}", Editar)
            .WithName("editarSetor")
            .Produces<SetorItem>(StatusCodes.Status200OK)
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

    private static async Task<SetorListaResposta> Listar([AsParameters] FiltrosDaListaDeCadastros f, SetoresCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ListarAsync(new ParametrosDeLista(f.Pagina, f.TamanhoPagina, f.Busca), ct);

    private static async Task<IResult> Criar(SetorComando comando, SetoresCasoDeUso casoDeUso, CancellationToken ct)
    {
        var setor = await casoDeUso.CriarAsync(comando, ct);
        return Results.Created($"/api/rh/setores/{setor.Id}", setor);
    }

    private static async Task<SetorItem> Editar(int setorId, SetorComando comando, SetoresCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.EditarAsync(setorId, comando, ct);
}
