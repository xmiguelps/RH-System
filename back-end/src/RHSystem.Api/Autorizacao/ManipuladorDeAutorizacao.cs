using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Routing.Patterns;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Api.Autorizacao;

/// <summary>
/// O 403 de quem está logado mas não tem o perfil da rota sai como <c>ProblemDetails</c> do contrato ("Sem permissão.") e grava o evento
/// <c>Acesso/AcessoNegado</c> ("Acesso negado: MÉTODO /rota/com/{parametro}": o modelo da rota, nunca o valor), com a pessoa logada como ator. O evento
/// sai por <c>RegistrarEGravarAsync</c> (a requisição não salva nada, então não há transação) e, se falhar, o 403 sai do mesmo jeito e a falha vai para o
/// log: negar o acesso não pode depender de a auditoria estar de pé. O 401 continua o <c>OnChallenge</c> do JwtBearer (fatia 0) e não grava (não há
/// pessoa). O que passa segue o fluxo normal.
/// </summary>
internal sealed class ManipuladorDeAutorizacao : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _padrao = new();

    public async Task HandleAsync(RequestDelegate proximo, HttpContext contexto, AuthorizationPolicy politica, PolicyAuthorizationResult resultado)
    {
        if (resultado.Forbidden)
        {
            await RegistrarAcessoNegadoAsync(contexto);
            await Problemas.EscreverPadraoAsync(contexto, StatusCodes.Status403Forbidden);
            return;
        }

        await _padrao.HandleAsync(proximo, contexto, politica, resultado);
    }

    private static async Task RegistrarAcessoNegadoAsync(HttpContext contexto)
    {
        var servicos = contexto.RequestServices;
        try
        {
            var ator = servicos.GetRequiredService<IAtorAtual>();
            if (ator.Tipo != TipoDeAtorAtual.Pessoa)
            {
                return; // sem pessoa não há ator para o evento
            }

            var evento = new EventoDeAuditoriaNovo(
                AuditoriaArea.Acesso,
                AuditoriaAcao.AcessoNegado,
                $"Acesso negado: {contexto.Request.Method} {ModeloDaRota(contexto.GetEndpoint())}",
                $"Colaborador {ator.Nome}");
            await servicos.GetRequiredService<IAuditor>().RegistrarEGravarAsync(evento, contexto.RequestAborted);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            servicos.GetRequiredService<ILogger<ManipuladorDeAutorizacao>>()
                .LogError(erro, "Não foi possível gravar o evento de acesso negado ({Metodo} {Caminho}); o 403 sai do mesmo jeito.", contexto.Request.Method, ModeloDaRota(contexto.GetEndpoint()));
        }
    }

    /// <summary>
    /// O modelo da rota com <c>{parametro}</c> no lugar do valor e sem as restrições (<c>{id:int}</c> vira <c>{id}</c>), que é como o contrato
    /// escreve o caminho. Nunca usa o caminho da requisição: ele traria o valor (um id, um token).
    /// </summary>
    internal static string ModeloDaRota(Endpoint? endpoint)
    {
        if (endpoint is not RouteEndpoint rota)
        {
            return "(rota desconhecida)";
        }

        var segmentos = rota.RoutePattern.PathSegments.Select(segmento => string.Concat(segmento.Parts.Select(parte => parte switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternParameterPart parametro => "{" + parametro.Name + "}",
            RoutePatternSeparatorPart separador => separador.Content,
            _ => string.Empty,
        })));
        return "/" + string.Join('/', segmentos);
    }
}
