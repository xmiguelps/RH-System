using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Treinamentos.Equipe;

namespace RHSystem.Api.Treinamentos;

/// <summary>
/// Os treinamentos na ficha e na equipe: a aba da ficha do RH (<c>/api/rh/funcionarios/{funcionarioId}/treinamentos</c>) e as três rotas do gestor (<c>/api/gestor/treinamentos</c>, <c>/api/gestor/treinamentos/{treinamentoId}/equipe</c> e <c>/api/gestor/equipe/{funcionarioId}/treinamentos</c>). Os <c>operationId</c> e os códigos de resposta são os de
/// <c>contrato/fontes/60-fatia-6-tad.yaml</c>. O gestor só vê a equipe de hoje e NUNCA certificado, código ou conteúdo de módulo.
/// </summary>
public static class TreinamentosEquipeEndpoints
{
    private const string Etiqueta = "Treinamentos";

    public static IServiceCollection AdicionarCasosDeUsoDosTreinamentosDaEquipe(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarTreinamentosDeFuncionarioCasoDeUso>();
        servicos.AddScoped<ListarTreinamentosDePessoaDaEquipeCasoDeUso>();
        servicos.AddScoped<ListarTreinamentosDaEquipeCasoDeUso>();
        servicos.AddScoped<ListarPessoasDaEquipeNoTreinamentoCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapTreinamentosEquipe(this IEndpointRouteBuilder rotas)
    {
        rotas.GrupoRh().WithTags(Etiqueta).MapGet("/funcionarios/{funcionarioId:int}/treinamentos", FichaDoRh)
            .WithName("listarTreinamentosDeFuncionario")
            .Produces<TreinamentoFichaRhResposta>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        var gestor = rotas.GrupoGestor().WithTags(Etiqueta);

        gestor.MapGet("/treinamentos", ListaDaEquipe)
            .WithName("listarTreinamentosDaEquipe")
            .Produces<TreinamentoGestorLista>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        gestor.MapGet("/treinamentos/{treinamentoId:int}/equipe", PessoasDaEquipe)
            .WithName("listarPessoasDaEquipeNoTreinamento")
            .Produces<TreinamentoGestorEquipeResposta>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        gestor.MapGet("/equipe/{funcionarioId:int}/treinamentos", FichaDaEquipe)
            .WithName("listarTreinamentosDePessoaDaEquipe")
            .Produces<TreinamentoFichaGestorResposta>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<TreinamentoFichaRhResposta> FichaDoRh(int funcionarioId, ListarTreinamentosDeFuncionarioCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(funcionarioId, ct);

    private static async Task<TreinamentoGestorLista> ListaDaEquipe(ListarTreinamentosDaEquipeCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(ct);

    private static async Task<TreinamentoGestorEquipeResposta> PessoasDaEquipe(int treinamentoId, ListarPessoasDaEquipeNoTreinamentoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(treinamentoId, ct);

    private static async Task<TreinamentoFichaGestorResposta> FichaDaEquipe(int funcionarioId, ListarTreinamentosDePessoaDaEquipeCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(funcionarioId, ct);
}
