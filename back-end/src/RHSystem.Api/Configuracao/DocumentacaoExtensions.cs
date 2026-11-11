using Scalar.AspNetCore;

namespace RHSystem.Api.Configuracao;

/// <summary>
/// O documento OpenAPI nativo do .NET, gerado pela própria API, e uma UI (Scalar) que LÊ esse documento (não gera um segundo, como o
/// Swashbuckle faz). Só existem com <c>Documentacao:Habilitada=true</c> e são servidos SEM login (fora de qualquer família de rota e sem
/// política de autorização). O contrato, e não este documento, é a fonte de verdade: o teste de divergência confere um contra o outro.
/// </summary>
public static class DocumentacaoExtensions
{
    private const string Chave = "Documentacao:Habilitada";

    public static IServiceCollection AdicionarDocumentacao(this IServiceCollection servicos, IConfiguration configuracao)
    {
        if (configuracao.GetValue<bool>(Chave))
        {
            servicos.AddOpenApi(opcoes => opcoes.UsarEsquemasDoContrato().UsarNomesDeParametrosDoContrato().UsarEnumsDeParametrosDoContrato().UsarFormulariosDoContrato());
        }

        return servicos;
    }

    public static WebApplication UsarDocumentacao(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>(Chave))
        {
            app.MapOpenApi().AllowAnonymous(); // /openapi/v1.json
            app.MapScalarApiReference(opcoes => opcoes.WithTitle("RH-System: API")).AllowAnonymous(); // /scalar/v1, lendo o documento acima
        }

        return app;
    }
}
