using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Application.Acesso;
using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Acesso;

/// <summary>Atalhos dos testes de senha: chamar as rotas, pegar o token que só existe na carga da fila e entrar de verdade.</summary>
internal static class AcessoDeTeste
{
    public const string CodigoDeLinkInvalido = "LINK_INVALIDO_OU_VENCIDO";

    public static Task<HttpResponseMessage> PostAsync(HttpClient cliente, string caminho, object corpo) => cliente.PostAsJsonAsync(caminho, corpo);

    public static async Task<JsonElement> CorpoAsync(HttpResponseMessage resposta) =>
        JsonSerializer.Deserialize<JsonElement>(await resposta.Content.ReadAsStringAsync());

    /// <summary>O token do e-mail mais recente do tipo, lido da carga da fila (o único lugar onde ele existe em claro).</summary>
    public static async Task<string> TokenDoUltimoEmailAsync(ApiDeAcesso api, int funcionarioId, string tipo)
    {
        var carga = (await Sql.EscalarAsync(api.Conexao, $"SELECT CARGA FROM FILA_EMAILS WHERE ID_FUNCIONARIO = {funcionarioId} AND TIPO = '{tipo}' ORDER BY ID_EMAIL DESC LIMIT 1"))!.ToString()!;
        return JsonDocument.Parse(carga).RootElement.GetProperty("token").GetString()!;
    }

    /// <summary>Emite o convite pelo serviço (o ator é o RH) e devolve o token do e-mail.</summary>
    public static async Task<string> NovoConviteAsync(ApiDeAcesso api, PessoaDoTeste pessoa)
    {
        await api.NaTransacaoAsync(sp => sp.GetRequiredService<IServicoDeConvite>().EmitirAsync([pessoa.FuncionarioId], CancellationToken.None));
        return await TokenDoUltimoEmailAsync(api, pessoa.FuncionarioId, "CONVITE");
    }

    /// <summary>Pede a redefinição pelo serviço e devolve o token do e-mail (a pessoa precisa de login ativo com senha definida).</summary>
    public static async Task<string> NovaRedefinicaoAsync(ApiDeAcesso api, PessoaDoTeste pessoa)
    {
        await api.NoEscopoAsync(async sp =>
        {
            await sp.GetRequiredService<IServicoDeConvite>().SolicitarRedefinicaoAsync(pessoa.Email!, CancellationToken.None);
            return 0;
        });
        return await TokenDoUltimoEmailAsync(api, pessoa.FuncionarioId, "REDEFINICAO_SENHA");
    }

    /// <summary>Entra de verdade pelo <c>/api/auth/entrar</c>: devolve o token de acesso e o refresh token.</summary>
    public static async Task<(string Acesso, string Refresh)> EntrarAsync(HttpClient cliente, string email, string senha = ApiFactory.SenhaPadrao)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/auth/entrar", new { email, senha });
        resposta.EnsureSuccessStatusCode();
        var corpo = await CorpoAsync(resposta);
        return (corpo.GetProperty("tokenDeAcesso").GetString()!, corpo.GetProperty("refreshToken").GetString()!);
    }

    /// <summary>O status de uma renovação com o refresh token dado (200 se vale, 401 se foi revogado).</summary>
    public static async Task<int> RenovarAsync(HttpClient cliente, string refresh) =>
        (int)(await cliente.PostAsJsonAsync("/api/auth/renovar", new { refreshToken = refresh })).StatusCode;

    public static HttpClient ClienteComToken(HttpClient cliente, string tokenDeAcesso)
    {
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenDeAcesso);
        return cliente;
    }
}
