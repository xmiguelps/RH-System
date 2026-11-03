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

        if (excecao is BadHttpRequestException requisicaoInvalida)
        {
            await EscreverRequisicaoInvalidaAsync(contexto, requisicaoInvalida.StatusCode);
            return true;
        }

        log.LogError(excecao, "Erro inesperado em {Metodo} {Caminho}", contexto.Request.Method, contexto.Request.Path);
        await Problemas.EscreverAsync(contexto, StatusCodes.Status500InternalServerError, "Erro no servidor.", "Erro no servidor. Tente novamente em instantes.");
        return true;
    }

    /// <summary>
    /// Corpo ausente, JSON inválido ou de tipo errado. O 400 segue o contrato: o <c>ProblemaDeValidacao</c> (com <c>errors</c>) e, na rota
    /// <c>entrar</c>, o texto do <c>Erro400Login</c>; nas outras, "Confira os campos indicados.". Outro status do framework (415 de tipo de mídia
    /// errado, 413 de corpo grande demais) é respeitado: não são "dados inválidos".
    /// </summary>
    private static async Task EscreverRequisicaoInvalidaAsync(HttpContext contexto, int status)
    {
        if (status != StatusCodes.Status400BadRequest)
        {
            await Problemas.EscreverAsync(contexto, status, "Requisição inválida.", "Confira a requisição e tente de novo.");
            return;
        }

        // O middleware de exceções zera o endpoint da requisição antes de chamar o tratador; o original vem na feature.
        var endpoint = contexto.Features.Get<IExceptionHandlerFeature>()?.Endpoint ?? contexto.GetEndpoint();
        var emEntrar = endpoint?.Metadata.GetMetadata<EndpointNameMetadata>()?.EndpointName == "entrar";
        var erros = new Dictionary<string, string[]> { ["corpo"] = ["O corpo da requisição é obrigatório e precisa ser um JSON válido."] };
        await Problemas.Resultado(
            contexto,
            StatusCodes.Status400BadRequest,
            "Dados inválidos.",
            emEntrar ? "Preencha o e-mail e a senha." : "Confira os campos indicados.",
            erros).ExecuteAsync(contexto);
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
    /// <summary>
    /// Resposta de erro SEM corpo vinda do framework (404 de rota inexistente, 405, 415) vira <c>application/problem+json</c> com os textos do
    /// contrato, como todo erro da API. Resposta que já tem corpo (401, 403, 422, 429 e 500 montados aqui) não é tocada.
    /// </summary>
    public static WebApplication UsarRespostasDeErroPadrao(this WebApplication app)
    {
        app.UseStatusCodePages(contexto =>
        {
            var http = contexto.HttpContext;
            var status = http.Response.StatusCode;
            var (titulo, detalhe) = status switch
            {
                StatusCodes.Status401Unauthorized => ("Não autenticado.", "Entre novamente para continuar."),
                StatusCodes.Status403Forbidden => ("Sem permissão.", "Você não tem permissão para esta ação."),
                StatusCodes.Status404NotFound => ("Não encontrado.", "O registro não foi encontrado."),
                >= 500 => ("Erro no servidor.", "Erro no servidor. Tente novamente em instantes."),
                _ => ("Requisição inválida.", "Confira a requisição e tente de novo."),
            };
            return Problemas.EscreverAsync(http, status, titulo, detalhe);
        });
        return app;
    }

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
