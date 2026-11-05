using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Painel;

namespace RHSystem.Api.Painel;

/// <summary>
/// As bolinhas do menu, uma rota por perfil (<c>contrato/fontes/70-fatia-7-gestao.yaml</c>): <c>/api/eu</c> (qualquer perfil), <c>/api/gestor</c> e <c>/api/rh</c>.
/// Leves: o front as chama a cada navegação. Cada contador vale 0 até a fatia dona registrar a sua <c>IFonteDeContadorDoMenu</c>
/// (documentos: 1c; avisos: 4; formulários: 5; ponto: 3; férias: 7). Os <c>operationId</c> e os códigos de resposta são os do contrato.
/// </summary>
public static class ContadoresDoMenuEndpoints
{
    private const string Etiqueta = "Painéis";

    public static IServiceCollection AdicionarCasosDeUsoDePainel(this IServiceCollection servicos)
    {
        servicos.AddScoped<ContadoresDoMenuCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapContadoresDoMenu(this IEndpointRouteBuilder rotas)
    {
        rotas.GrupoEu().WithTags(Etiqueta).MapGet("/contadores-do-menu", ObterDoFuncionario)
            .WithName("obterContadoresDoMenuDoFuncionario")
            .Produces<PainelContadoresFuncionario>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);

        rotas.GrupoGestor().WithTags(Etiqueta).MapGet("/contadores-do-menu", ObterDoGestor)
            .WithName("obterContadoresDoMenuDoGestor")
            .Produces<PainelContadoresGestor>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        rotas.GrupoRh().WithTags(Etiqueta).MapGet("/contadores-do-menu", ObterDoRh)
            .WithName("obterContadoresDoMenuDoRh")
            .Produces<PainelContadoresRh>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<PainelContadoresFuncionario> ObterDoFuncionario(ContadoresDoMenuCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ObterDoFuncionarioAsync(ct);

    private static async Task<PainelContadoresGestor> ObterDoGestor(ContadoresDoMenuCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ObterDoGestorAsync(ct);

    private static async Task<PainelContadoresRh> ObterDoRh(ContadoresDoMenuCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ObterDoRhAsync(ct);
}
