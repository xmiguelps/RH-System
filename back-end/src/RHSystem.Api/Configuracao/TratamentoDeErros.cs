using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MySqlConnector;

namespace RHSystem.Api.Configuracao;

/// <summary>Monta os erros da API: sempre <c>application/problem+json</c>, em português, com <c>traceId</c> e sem stack trace.</summary>
internal static class Problemas
{
    public static ProblemDetails Criar(
        HttpContext contexto,
        int status,
        string titulo,
        string detalhe,
        IDictionary<string, string[]>? erros = null,
        string? regraDoBanco = null)
    {
        var problema = new ProblemDetails { Status = status, Title = titulo, Detail = detalhe };
        problema.Extensions["traceId"] = Activity.Current?.Id ?? contexto.TraceIdentifier;
        if (erros is not null)
        {
            problema.Extensions["errors"] = erros;
        }

        if (regraDoBanco is not null)
        {
            problema.Extensions["regraDoBanco"] = regraDoBanco;
        }

        return problema;
    }

    public static IResult Resultado(
        HttpContext contexto,
        int status,
        string titulo,
        string detalhe,
        IDictionary<string, string[]>? erros = null,
        string? regraDoBanco = null) =>
        Results.Problem(Criar(contexto, status, titulo, detalhe, erros, regraDoBanco));

    public static Task EscreverAsync(HttpContext contexto, int status, string titulo, string detalhe) =>
        Resultado(contexto, status, titulo, detalhe).ExecuteAsync(contexto);

    // Textos do contrato (contrato/fontes/00-base.yaml e 10-fatia-0-autenticacao.yaml).
    public static Task NaoAutenticadoAsync(HttpContext contexto) =>
        EscreverAsync(contexto, StatusCodes.Status401Unauthorized, "Não autenticado.", "Entre novamente para continuar.");

    public static IResult NaoAutenticado(HttpContext contexto) =>
        Resultado(contexto, StatusCodes.Status401Unauthorized, "Não autenticado.", "Entre novamente para continuar.");
}

/// <summary>
/// Último recurso: qualquer exceção que ninguém tratou vira um <c>ProblemDetails</c> sem stack trace e sem a mensagem da exceção (que
/// pode ter dado interno). Um erro de trigger do banco (<c>SQLSTATE 45000</c>) vira 422 com a mensagem original em <c>regraDoBanco</c>.
/// </summary>
internal sealed class TratadorGlobalDeErros(ILogger<TratadorGlobalDeErros> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excecao, CancellationToken ct)
    {
        var regraDoBanco = MensagemDeTriggerDoBanco(excecao);
        if (regraDoBanco is not null)
        {
            log.LogWarning("Uma regra do banco de dados recusou a operação em {Metodo} {Caminho}: {Regra}", contexto.Request.Method, contexto.Request.Path, regraDoBanco);
            await Problemas.Resultado(
                contexto,
                StatusCodes.Status422UnprocessableEntity,
                "Operação recusada.",
                "A operação foi recusada por uma regra do banco de dados.",
                regraDoBanco: regraDoBanco).ExecuteAsync(contexto);
            return true;
        }

        if (excecao is BadHttpRequestException)
        {
            await Problemas.EscreverAsync(contexto, StatusCodes.Status400BadRequest, "Dados inválidos.", "Confira os campos indicados.");
            return true;
        }

        log.LogError(excecao, "Erro inesperado em {Metodo} {Caminho}", contexto.Request.Method, contexto.Request.Path);
        await Problemas.EscreverAsync(contexto, StatusCodes.Status500InternalServerError, "Erro no servidor.", "Erro no servidor. Tente novamente em instantes.");
        return true;
    }

    private static string? MensagemDeTriggerDoBanco(Exception excecao)
    {
        for (var atual = excecao; atual is not null; atual = atual.InnerException)
        {
            if (atual is MySqlException { SqlState: "45000" } erroDoBanco)
            {
                return erroDoBanco.Message;
            }
        }

        return null;
    }
}

public static class TratamentoDeErrosExtensions
{
    public static IServiceCollection AdicionarTratamentoDeErros(this IServiceCollection servicos)
    {
        servicos.AddProblemDetails();
        servicos.AddExceptionHandler<TratadorGlobalDeErros>();

        // Corpo ausente ou JSON inválido: por padrão só o ambiente de desenvolvimento lança BadHttpRequestException; nos demais o framework
        // responde um 400 sem corpo, direto. Sempre lançar faz o tratador global montar o ProblemDetails do contrato em todo ambiente.
        servicos.Configure<RouteHandlerOptions>(opcoes => opcoes.ThrowOnBadRequest = true);
        return servicos;
    }
}
