using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MySqlConnector;
using RHSystem.Application.Comum;

namespace RHSystem.Api.Configuracao;

/// <summary>Monta os erros da API: sempre <c>application/problem+json</c>, em português, com <c>traceId</c> e sem stack trace.</summary>
internal static class Problemas
{
    /// <summary>
    /// Marca no <c>HttpContext.Items</c> que a resposta já foi montada aqui (com o título e o detalhe certos), para o
    /// <c>CustomizeProblemDetails</c> não trocar o título que a rota escolheu (por exemplo "E-mail ou senha inválidos.").
    /// </summary>
    public const string ItemDoContexto = "rh-system.problema-montado";

    public static ProblemDetails Criar(
        HttpContext contexto,
        int status,
        string titulo,
        string detalhe,
        IDictionary<string, string[]>? erros = null,
        string? regraDoBanco = null,
        string? codigo = null,
        IReadOnlyDictionary<string, object?>? extensoes = null)
    {
        contexto.Items[ItemDoContexto] = true;
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

        if (codigo is not null)
        {
            problema.Extensions["codigo"] = codigo;
        }

        // Extras do corpo (ProblemaDeApiException): a chave sai em camelCase e nunca sobrescreve o que a resposta já tem nem os campos fixos do ProblemDetails.
        foreach (var (chave, valor) in extensoes ?? new Dictionary<string, object?>())
        {
            var nome = JsonNamingPolicy.CamelCase.ConvertName(chave);
            if (!CamposReservados.Contains(nome) && !problema.Extensions.ContainsKey(nome))
            {
                problema.Extensions[nome] = valor;
            }
        }

        return problema;
    }

    private static readonly HashSet<string> CamposReservados = new(["title", "status", "detail", "traceId", "codigo", "type", "instance"], StringComparer.Ordinal);

    public static IResult Resultado(
        HttpContext contexto,
        int status,
        string titulo,
        string detalhe,
        IDictionary<string, string[]>? erros = null,
        string? regraDoBanco = null,
        string? codigo = null,
        IReadOnlyDictionary<string, object?>? extensoes = null) =>
        Results.Problem(Criar(contexto, status, titulo, detalhe, erros, regraDoBanco, codigo, extensoes));

    public static Task EscreverAsync(HttpContext contexto, int status, string titulo, string detalhe) =>
        Resultado(contexto, status, titulo, detalhe).ExecuteAsync(contexto);

    /// <summary>Um erro com o título do contrato e o detalhe dado, ou o padrão do contrato para aquele status.</summary>
    public static Task EscreverPadraoAsync(
        HttpContext contexto,
        int status,
        string? detalhe = null,
        IDictionary<string, string[]>? erros = null,
        string? regraDoBanco = null,
        string? codigo = null) =>
        Resultado(contexto, status, TextosDeErro.Titulo(status), detalhe ?? TextosDeErro.DetalhePadrao(status), erros, regraDoBanco, codigo).ExecuteAsync(contexto);

    public static Task NaoAutenticadoAsync(HttpContext contexto) => EscreverPadraoAsync(contexto, StatusCodes.Status401Unauthorized);

    public static IResult NaoAutenticado(HttpContext contexto) =>
        Resultado(
            contexto,
            StatusCodes.Status401Unauthorized,
            TextosDeErro.Titulo(StatusCodes.Status401Unauthorized),
            TextosDeErro.DetalhePadrao(StatusCodes.Status401Unauthorized));
}

/// <summary>
/// Traduz o que o caso de uso lança (<c>NaoEncontradoException</c> 404, <c>ConflitoException</c> 409, <c>ErroDeNegocioException</c> 422,
/// <c>ErroDeValidacaoException</c> 400) e o que o banco recusa. Chave única violada (<c>1062</c>) vira 409 genérico, sem o nome da chave nem o
/// valor. Um erro de trigger do banco (<c>SQLSTATE 45000</c>) vira 422 com a mensagem original em <c>regraDoBanco</c>. Qualquer outra exceção, inclusive
/// <c>CHECK</c>, chave estrangeira e <c>NOT NULL</c> do banco, vira 500 sem stack trace, sem SQL e sem a mensagem da exceção (que pode ter dado
/// interno); o detalhe fica no log.
/// </summary>
internal sealed class TratadorGlobalDeErros(ILogger<TratadorGlobalDeErros> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excecao, CancellationToken ct)
    {
        switch (excecao)
        {
            case ProblemaDeApiException problema:
                foreach (var (cabecalho, valor) in problema.Cabecalhos ?? new Dictionary<string, string>())
                {
                    contexto.Response.Headers[cabecalho] = valor;
                }

                await Problemas.Resultado(contexto, problema.Status, problema.Titulo, problema.Detalhe, codigo: problema.Codigo, extensoes: problema.Extensoes).ExecuteAsync(contexto);
                return true;
            case NaoEncontradoException naoEncontrado:
                await Problemas.EscreverPadraoAsync(contexto, StatusCodes.Status404NotFound, naoEncontrado.Detalhe);
                return true;
            case ConflitoException conflito:
                await Problemas.EscreverPadraoAsync(contexto, StatusCodes.Status409Conflict, conflito.Detalhe, codigo: conflito.Codigo);
                return true;
            case ErroDeNegocioException negocio:
                await Problemas.EscreverPadraoAsync(contexto, StatusCodes.Status422UnprocessableEntity, negocio.Detalhe, codigo: negocio.Codigo);
                return true;
            case ErroDeValidacaoException validacao:
                await Problemas.EscreverPadraoAsync(contexto, StatusCodes.Status400BadRequest, erros: validacao.Erros.ToDictionary(e => e.Key, e => e.Value));
                return true;
        }

        var erroDoBanco = ErroDoMySql(excecao);

        var regraDoBanco = erroDoBanco is { SqlState: "45000" } ? erroDoBanco.Message : null;
        if (regraDoBanco is not null)
        {
            log.LogWarning("Uma regra do banco de dados recusou a operação em {Metodo} {Caminho}: {Regra}", contexto.Request.Method, CaminhoParaLog(contexto), regraDoBanco);
            await Problemas.EscreverPadraoAsync(contexto, StatusCodes.Status422UnprocessableEntity, regraDoBanco: regraDoBanco);
            return true;
        }

        if (erroDoBanco is { ErrorCode: MySqlErrorCode.DuplicateKeyEntry })
        {
            // A mensagem do MySQL traz o valor repetido e o nome da chave: nem vai para a resposta nem para o log.
            log.LogWarning("Uma chave única do banco de dados recusou a operação em {Metodo} {Caminho}", contexto.Request.Method, CaminhoParaLog(contexto));
            await Problemas.EscreverPadraoAsync(contexto, StatusCodes.Status409Conflict);
            return true;
        }

        if (excecao is BadHttpRequestException requisicaoInvalida)
        {
            await EscreverRequisicaoInvalidaAsync(contexto, requisicaoInvalida.StatusCode);
            return true;
        }

        log.LogError(excecao, "Erro inesperado em {Metodo} {Caminho}", contexto.Request.Method, CaminhoParaLog(contexto));
        await Problemas.EscreverPadraoAsync(contexto, StatusCodes.Status500InternalServerError);
        return true;
    }

    /// <summary>O caminho que vai ao log: nas rotas públicas do formulário o código do link (o segredo de quem tem o link) sai como <c>{token}</c>, em qualquer erro.</summary>
    internal static string CaminhoParaLog(HttpContext contexto)
    {
        const string prefixo = "/api/publico/formularios/";
        var caminho = contexto.Request.Path.Value ?? string.Empty;
        if (!caminho.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
        {
            return caminho;
        }

        var resto = caminho[prefixo.Length..];
        var barra = resto.IndexOf('/', StringComparison.Ordinal);
        return prefixo + "{token}" + (barra < 0 ? string.Empty : resto[barra..]);
    }

    /// <summary>
    /// Corpo ausente, JSON inválido, enum desconhecido ou parâmetro que não converte. O 400 segue o contrato: o <c>ProblemaDeValidacao</c> (com
    /// <c>errors</c>) e, na rota <c>entrar</c>, o texto do <c>Erro400Login</c>; nas outras, "Confira os campos indicados.". Outro status do
    /// framework (415 de tipo de mídia errado, 413 de corpo grande demais) é respeitado: não são "dados inválidos".
    /// </summary>
    private static async Task EscreverRequisicaoInvalidaAsync(HttpContext contexto, int status)
    {
        if (status != StatusCodes.Status400BadRequest)
        {
            await Problemas.EscreverPadraoAsync(contexto, status);
            return;
        }

        // O middleware de exceções zera o endpoint da requisição antes de chamar o tratador; o original vem na feature.
        var endpoint = contexto.Features.Get<IExceptionHandlerFeature>()?.Endpoint ?? contexto.GetEndpoint();
        var emEntrar = endpoint?.Metadata.GetMetadata<EndpointNameMetadata>()?.EndpointName == "entrar";
        var erros = new Dictionary<string, string[]> { ["corpo"] = ["O corpo da requisição é obrigatório e precisa ser um JSON válido."] };
        await Problemas.EscreverPadraoAsync(
            contexto,
            StatusCodes.Status400BadRequest,
            emEntrar ? "Preencha o e-mail e a senha." : null,
            erros);
    }

    /// <summary>O primeiro erro do MySQL na cadeia de exceções (o EF embrulha o do provedor em <c>DbUpdateException</c>), ou <c>null</c>.</summary>
    private static MySqlException? ErroDoMySql(Exception excecao)
    {
        for (var atual = excecao; atual is not null; atual = atual.InnerException)
        {
            if (atual is MySqlException erroDoBanco)
            {
                return erroDoBanco;
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
            return Problemas.EscreverPadraoAsync(http, http.Response.StatusCode);
        });
        return app;
    }

    public static IServiceCollection AdicionarTratamentoDeErros(this IServiceCollection servicos)
    {
        // Quem escreve pelo IProblemDetailsService (o próprio framework, por exemplo) sai com o título e o detalhe do contrato e o traceId,
        // e não com o texto em inglês do padrão.
        servicos.AddProblemDetails(opcoes => opcoes.CustomizeProblemDetails = contexto =>
        {
            if (contexto.HttpContext.Items.ContainsKey(Problemas.ItemDoContexto))
            {
                return; // já montado em Problemas.Criar, com os textos que a rota escolheu
            }

            var problema = contexto.ProblemDetails;
            var status = problema.Status ?? contexto.HttpContext.Response.StatusCode;
            problema.Status = status;
            problema.Title = TextosDeErro.Titulo(status);
            problema.Detail ??= TextosDeErro.DetalhePadrao(status);
            problema.Extensions["traceId"] = Activity.Current?.Id ?? contexto.HttpContext.TraceIdentifier;
        });
        servicos.AddExceptionHandler<TratadorGlobalDeErros>();

        // Corpo ausente ou JSON inválido: por padrão só o ambiente de desenvolvimento lança BadHttpRequestException; nos demais o framework
        // responde um 400 sem corpo, direto. Sempre lançar faz o tratador global montar o ProblemDetails do contrato em todo ambiente.
        servicos.Configure<RouteHandlerOptions>(opcoes => opcoes.ThrowOnBadRequest = true);
        return servicos;
    }
}
