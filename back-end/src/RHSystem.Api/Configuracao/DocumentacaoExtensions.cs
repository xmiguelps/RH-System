namespace RHSystem.Api.Configuracao;

/// <summary>
/// O documento OpenAPI nativo do .NET, gerado pela própria API. Só existe com <c>Documentacao:Habilitada=true</c> e é servido SEM login (a
/// rota fica fora de qualquer família e sem política de autorização). O contrato, e não este documento, é a fonte de verdade: o teste de
/// divergência confere um contra o outro.
/// </summary>
public static class DocumentacaoExtensions
{
    private const string Chave = "Documentacao:Habilitada";

    public static IServiceCollection AdicionarDocumentacao(this IServiceCollection servicos, IConfiguration configuracao)
    {
        if (configuracao.GetValue<bool>(Chave))
        {
            servicos.AddOpenApi();
        }

        return servicos;
    }

    public static WebApplication UsarDocumentacao(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>(Chave))
        {
            app.MapOpenApi().AllowAnonymous();
        }

        return app;
    }
}
