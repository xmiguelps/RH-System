using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Formularios.Responder;

namespace RHSystem.Api.Formularios;

/// <summary>Os parâmetros de <c>GET /api/eu/formularios</c>. <c>Tipo</c> e <c>Situacao</c> chegam como texto: o caso de uso os lê pelo nome do contrato e devolve todos os erros juntos num 400 só.</summary>
public sealed record FiltrosDaListaDeMeusFormularios(int Pagina = 1, int TamanhoPagina = 20, string? Busca = null, string? Tipo = null, string? Situacao = null);

/// <summary>
/// Os formulários de quem está logado (<c>/api/eu/formularios</c>, qualquer perfil: nenhuma rota tem 403). Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/50-fatia-5-formularios.yaml</c>. Esta parte: a lista de cartões e abrir o formulário para responder.
/// </summary>
public static class FormulariosEuEndpoints
{
    private const string Etiqueta = "Formulários";

    public static IServiceCollection AdicionarCasosDeUsoDosFormulariosEu(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarMeusFormulariosCasoDeUso>();
        servicos.AddScoped<ObterFormularioParaResponderCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapFormulariosEu(this IEndpointRouteBuilder rotas)
    {
        var eu = rotas.GrupoEu().WithTags(Etiqueta);

        eu.MapGet("/formularios", Listar)
            .WithName("listarMeusFormularios")
            .Produces<FormularioMeusLista>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);

        eu.MapGet("/formularios/{formularioId:int}", Obter)
            .WithName("obterFormularioParaResponder")
            .Produces<FormularioParaResponder>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<FormularioMeusLista> Listar([AsParameters] FiltrosDaListaDeMeusFormularios f, ListarMeusFormulariosCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, f.Busca, f.Tipo, f.Situacao, ct);

    private static async Task<FormularioParaResponder> Obter(int formularioId, ObterFormularioParaResponderCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(formularioId, ct);
}
