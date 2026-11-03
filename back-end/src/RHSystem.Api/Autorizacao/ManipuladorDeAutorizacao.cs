using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using RHSystem.Api.Configuracao;

namespace RHSystem.Api.Autorizacao;

/// <summary>
/// O 403 de quem está logado mas não tem o perfil da rota sai como <c>ProblemDetails</c> do contrato ("Sem permissão."). O 401 continua o
/// <c>OnChallenge</c> do JwtBearer (fatia 0). O que passa segue o fluxo normal. (O evento de auditoria do 403 entra na Task 8.2.)
/// </summary>
internal sealed class ManipuladorDeAutorizacao : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _padrao = new();

    public async Task HandleAsync(RequestDelegate proximo, HttpContext contexto, AuthorizationPolicy politica, PolicyAuthorizationResult resultado)
    {
        if (resultado.Forbidden)
        {
            await Problemas.EscreverPadraoAsync(contexto, StatusCodes.Status403Forbidden);
            return;
        }

        await _padrao.HandleAsync(proximo, contexto, politica, resultado);
    }
}
