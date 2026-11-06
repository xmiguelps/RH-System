using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Colaboradores;

namespace RHSystem.Api.Colaboradores;

/// <summary>
/// A equipe do gestor (lista, ficha e indicadores). Só o perfil Gestor (<c>GrupoGestor</c>): o RH e o funcionário recebem 403. Tudo o que está fora da equipe responde 404 (nunca 403 nem 200 vazio), o mesmo 404 de um id
/// que não existe. Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/20-fatia-1-colaboradores.yaml</c>. Registrar por <c>operationId</c>, nunca por prefixo: sob
/// <c>/api/gestor/equipe/{funcionarioId}/</c> o contrato também tem rotas de ponto, férias, treinamentos e advertências, que são de outras fatias.
/// </summary>
public static class EquipeEndpoints
{
    private const string Etiqueta = "Equipe";

    public static IServiceCollection AdicionarCasosDeUsoDeEquipe(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarEquipeDoGestorCasoDeUso>();
        servicos.AddScoped<ObterFichaDaEquipeCasoDeUso>();
        servicos.AddScoped<ObterIndicadoresDaEquipeCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapEquipe(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoGestor().WithTags(Etiqueta);

        grupo.MapGet("/equipe", Listar)
            .WithName("listarEquipeDoGestor")
            .Produces<FuncionarioEquipeResposta>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapGet("/equipe/{funcionarioId:int}", ObterFicha)
            .WithName("obterFichaDaEquipe")
            .Produces<FuncionarioFichaGestor>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        grupo.MapGet("/equipe/{funcionarioId:int}/indicadores", ObterIndicadores)
            .WithName("obterIndicadoresDaEquipe")
            .Produces<FuncionarioIndicadoresGestor>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<FuncionarioEquipeResposta> Listar(ListarEquipeDoGestorCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(ct);

    private static async Task<FuncionarioFichaGestor> ObterFicha(int funcionarioId, ObterFichaDaEquipeCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(funcionarioId, ct);

    private static async Task<FuncionarioIndicadoresGestor> ObterIndicadores(int funcionarioId, ObterIndicadoresDaEquipeCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(funcionarioId, ct);
}
