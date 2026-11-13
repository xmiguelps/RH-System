using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Ferias;

namespace RHSystem.Api.Ferias;

/// <summary>Os parâmetros da lista de férias da equipe e SÓ eles; <c>Situacao</c> chega como texto (o caso de uso o lê pelo nome do contrato e responde 400 com o erro por campo).</summary>
public sealed record FiltrosDaListaDeFeriasDaEquipe(int Pagina = 1, int TamanhoPagina = 20, string? Situacao = null);

/// <summary>Os parâmetros do calendário e SÓ eles: o dia de início (qualquer dia da semana) e quantas semanas mostrar (padrão 13).</summary>
public sealed record FiltrosDoCalendarioDeFerias(DateOnly? Inicio = null, int Semanas = 13);

/// <summary>
/// As férias da EQUIPE do gestor (<c>/api/gestor/ferias</c>, só o perfil Gestor): a fila dele e as decisões da etapa dele (aprovar, recusar, sugerir outra data). Pedido do próprio gestor, de outro gestor, de outro setor ou que não existe: o mesmo 404. Os códigos de resposta são os de
/// <c>contrato/fontes/70-fatia-7-gestao.yaml</c>. As rotas com <c>{feriasId}</c> usam <c>:int</c> (o calendário é um literal no mesmo prefixo).
/// </summary>
public static class FeriasGestorEndpoints
{
    private const string Etiqueta = "Férias";

    public static IServiceCollection AdicionarCasosDeUsoDasFeriasDaEquipe(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarFeriasDaEquipeCasoDeUso>();
        servicos.AddScoped<DecidirFeriasComoGestorCasoDeUso>();
        servicos.AddScoped<SugerirOutraDataDasFeriasCasoDeUso>();
        servicos.AddScoped<ObterFeriasDaPessoaDaEquipeCasoDeUso>();
        servicos.AddScoped<ObterCalendarioDeFeriasDaEquipeCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapFeriasGestor(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoGestor().WithTags(Etiqueta);

        grupo.MapGet("/ferias", Listar)
            .WithName("listarFeriasDaEquipe")
            .Produces<RespostaPaginada<FeriasPedidoAnalise>>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/ferias/{feriasId:int}/aprovar", Aprovar)
            .WithName("aprovarFeriasComoGestor")
            .Produces<FeriasPedidoAnalise>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/ferias/{feriasId:int}/recusar", Recusar)
            .WithName("recusarFeriasComoGestor")
            .Produces<FeriasPedidoAnalise>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/ferias/{feriasId:int}/sugerir-outra-data", Sugerir)
            .WithName("sugerirOutraDataDasFerias")
            .Produces<FeriasPedidoAnalise>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        // O literal "calendario" convive com o {feriasId:int} no mesmo prefixo sem ambiguidade (o :int não casa com texto).
        grupo.MapGet("/ferias/calendario", Calendario)
            .WithName("obterCalendarioDeFeriasDaEquipe")
            .Produces<FeriasCalendario>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        // O prefixo /api/gestor/equipe/{funcionarioId}/ é de várias fatias: a rota é registrada com o literal "ferias" e o :int.
        grupo.MapGet("/equipe/{funcionarioId:int}/ferias", ObterDaPessoa)
            .WithName("obterFeriasDaPessoaDaEquipe")
            .Produces<FeriasFicha>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<FeriasCalendario> Calendario([AsParameters] FiltrosDoCalendarioDeFerias f, ObterCalendarioDeFeriasDaEquipeCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(f.Inicio, f.Semanas, ct);

    private static async Task<FeriasFicha> ObterDaPessoa(int funcionarioId, ObterFeriasDaPessoaDaEquipeCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(funcionarioId, ct);

    private static async Task<RespostaPaginada<FeriasPedidoAnalise>> Listar([AsParameters] FiltrosDaListaDeFeriasDaEquipe f, ListarFeriasDaEquipeCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, f.Situacao, ct);

    private static async Task<FeriasPedidoAnalise> Aprovar(int feriasId, DecidirFeriasComoGestorCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.AprovarAsync(feriasId, ct);

    private static async Task<FeriasPedidoAnalise> Recusar(int feriasId, FeriasRecusarComando corpo, DecidirFeriasComoGestorCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.RecusarAsync(feriasId, corpo, ct);

    private static async Task<FeriasPedidoAnalise> Sugerir(int feriasId, FeriasSugerirComando corpo, SugerirOutraDataDasFeriasCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(feriasId, corpo, ct);
}
