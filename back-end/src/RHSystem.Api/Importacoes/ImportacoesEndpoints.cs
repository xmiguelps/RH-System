using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Importacoes;

namespace RHSystem.Api.Importacoes;

/// <summary>
/// A importação de colaboradores por planilha Excel (fatia 2), só para o RH (<c>GrupoRh</c>): baixar o modelo, enviar a planilha e receber a prévia, confirmar. Os <c>operationId</c> e os códigos de resposta são os de
/// <c>contrato/fontes/25-fatia-2-importacao.yaml</c>.
/// </summary>
public static class ImportacoesEndpoints
{
    private const string Etiqueta = "Importação";

    public static IServiceCollection AdicionarCasosDeUsoDeImportacao(this IServiceCollection servicos)
    {
        servicos.AddScoped<ModeloDeImportacaoCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapImportacoes(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoRh().WithTags(Etiqueta);

        grupo.MapGet("/importacoes/modelo", BaixarModelo)
            .WithName("baixarModeloDeImportacao")
            .Produces(StatusCodes.Status200OK, contentType: ModeloDeImportacaoCasoDeUso.TipoDeConteudo)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<IResult> BaixarModelo(ModeloDeImportacaoCasoDeUso casoDeUso, CancellationToken ct) =>
        Results.File(await casoDeUso.GerarAsync(ct), ModeloDeImportacaoCasoDeUso.TipoDeConteudo, ModeloDeImportacaoCasoDeUso.NomeDoArquivo);
}
