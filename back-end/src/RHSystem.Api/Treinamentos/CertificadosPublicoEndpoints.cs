using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Treinamentos.Publico;

namespace RHSystem.Api.Treinamentos;

/// <summary>
/// A conferência pública do certificado (<c>/api/publico/certificados/{codigoValidacao}</c>, sem login). O <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/60-fatia-6-tad.yaml</c>. O limite de consultas por IP (<see cref="LimiteDeTentativasExtensions.PoliticaDeCertificadoConferencia"/>) roda ANTES de tudo, inclusive do 404; a rota nunca responde 401 nem 403.
/// O código é o segredo de quem tem o certificado: nenhum log o traz (o tratador global escreve <c>/api/publico/certificados/{codigo}</c>).
/// </summary>
public static class CertificadosPublicoEndpoints
{
    public static IServiceCollection AdicionarCasosDeUsoDosCertificadosPublicos(this IServiceCollection servicos)
    {
        servicos.AddScoped<ValidarCertificadoCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapCertificadosPublico(this IEndpointRouteBuilder rotas)
    {
        var publico = rotas.GrupoPublico().WithTags("Certificados");

        publico.MapGet("/certificados/{codigoValidacao}", Validar)
            .WithName("validarCertificado")
            .RequireRateLimiting(LimiteDeTentativasExtensions.PoliticaDeCertificadoConferencia)
            .Produces<CertificadoValidacaoResposta>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status429TooManyRequests, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<CertificadoValidacaoResposta> Validar(string codigoValidacao, ValidarCertificadoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(codigoValidacao, ct);
}
