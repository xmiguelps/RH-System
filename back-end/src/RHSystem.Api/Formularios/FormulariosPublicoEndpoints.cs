using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Formularios.Publico;

namespace RHSystem.Api.Formularios;

/// <summary>
/// O formulário do candidato (<c>/api/publico/formularios/{token}</c>, sem login): a página e, na tarefa seguinte, a candidatura. O limite por IP (30 consultas por minuto, 10 envios por hora) roda ANTES de qualquer coisa e o código do link nunca vai a log. Os <c>operationId</c> e os códigos de
/// resposta são os de <c>contrato/fontes/50-fatia-5-formularios.yaml</c>.
/// </summary>
public static class FormulariosPublicoEndpoints
{
    private const string Etiqueta = "Formulário público";

    public static IServiceCollection AdicionarCasosDeUsoDoFormularioPublico(this IServiceCollection servicos, IConfiguration configuracao)
    {
        servicos.AddSingleton(configuracao.GetSection(OpcoesDoFormularioPublico.Secao).Get<OpcoesDoFormularioPublico>() ?? new OpcoesDoFormularioPublico());
        servicos.AddScoped<ObterFormularioPublicoCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapFormulariosPublico(this IEndpointRouteBuilder rotas)
    {
        var publico = rotas.GrupoPublico().WithTags(Etiqueta);

        publico.MapGet("/formularios/{token}", Pagina)
            .WithName("obterFormularioPublico")
            .RequireRateLimiting(LimiteDeTentativasExtensions.PoliticaDeFormularioConsulta)
            .Produces<FormularioPaginaPublica>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status429TooManyRequests, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<FormularioPaginaPublica> Pagina(string token, ObterFormularioPublicoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(token, ct);
}
