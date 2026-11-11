using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Cadastros;
using RHSystem.Application.Comum.Consultas;

namespace RHSystem.Api.Cadastros;

/// <summary>
/// "Benefícios" do catálogo (tela 29): lista, cria, edita e exclui. Só o perfil RH (<c>GrupoRh</c>). Os <c>operationId</c> e os códigos de resposta são os de
/// <c>contrato/fontes/20-fatia-1-colaboradores.yaml</c>. <c>/api/rh/beneficios</c> é prefixo textual de <c>/api/rh/beneficios-adesoes</c> (fatia 7): o escopo do contrato é por <c>operationId</c>.
/// </summary>
public static class BeneficiosEndpoints
{
    private const string Etiqueta = "Cadastros de apoio";

    public static IServiceCollection AdicionarCasosDeUsoDeBeneficios(this IServiceCollection servicos)
    {
        servicos.AddScoped<BeneficiosCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapBeneficios(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoRh().WithTags(Etiqueta);

        grupo.MapGet("/beneficios", Listar)
            .WithName("listarBeneficiosDoCatalogo")
            .Produces<BeneficioCatalogoListaResposta>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/beneficios", Criar)
            .WithName("criarBeneficioDoCatalogo")
            .Produces<BeneficioCatalogoItem>(StatusCodes.Status201Created)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status409Conflict, StatusCodes.Status500InternalServerError);

        grupo.MapPut("/beneficios/{beneficioId:int}", Editar)
            .WithName("editarBeneficioDoCatalogo")
            .Produces<BeneficioCatalogoItem>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        grupo.MapDelete("/beneficios/{beneficioId:int}", Excluir)
            .WithName("excluirBeneficioDoCatalogo")
            .Produces(StatusCodes.Status204NoContent)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<BeneficioCatalogoListaResposta> Listar([AsParameters] FiltrosDaListaDeCadastros f, BeneficiosCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ListarAsync(new ParametrosDeLista(f.Pagina, f.TamanhoPagina, f.Busca), ct);

    private static async Task<IResult> Criar(BeneficioCatalogoComando comando, BeneficiosCasoDeUso casoDeUso, CancellationToken ct)
    {
        var beneficio = await casoDeUso.CriarAsync(comando, ct);
        return Results.Created($"/api/rh/beneficios/{beneficio.Id}", beneficio);
    }

    private static async Task<BeneficioCatalogoItem> Editar(int beneficioId, BeneficioCatalogoComando comando, BeneficiosCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.EditarAsync(beneficioId, comando, ct);

    private static async Task<IResult> Excluir(int beneficioId, BeneficiosCasoDeUso casoDeUso, CancellationToken ct)
    {
        await casoDeUso.ExcluirAsync(beneficioId, ct);
        return Results.NoContent();
    }
}
