using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Autorizacao;

/// <summary>
/// Uma API (e um banco) para todos os testes de autorização: eles só criam logins (refresh tokens novos) e leem rotas de teste. Além das pessoas
/// do <see cref="ApiFactory"/> (RH, Funcionário), semeia um Gestor.
/// </summary>
internal static class ApiDeAutorizacao
{
    public const string EmailGestor = "gestor@empresa.com.br";

    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static ApiFactory? _api;

    public static async Task<ApiFactory> ObterAsync(MySqlFixture mysql)
    {
        await Trava.WaitAsync();
        try
        {
            if (_api is null)
            {
                var api = await ApiFactory.CriarAsync(mysql);
                await api.CriarUsuarioAsync(EmailGestor, nivel: "Funcionario Gestor");
                _api = api;
            }

            return _api;
        }
        finally
        {
            Trava.Release();
        }
    }

    /// <summary>O token de acesso que o <c>/api/auth/entrar</c> devolve para a pessoa.</summary>
    public static async Task<string> TokenAsync(ApiFactory api, string email)
    {
        var resposta = await api.CreateClient().PostAsJsonAsync("/api/auth/entrar", new { email, senha = ApiFactory.SenhaPadrao });
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tokenDeAcesso").GetString()!;
    }

    /// <summary>
    /// Um token assinado com a chave certa e válido, mas com o <c>perfil</c> que o teste quiser (ou sem o claim): o que o emissor da API nunca
    /// emite, e por isso só assim se prova que a autorização não confia em qualquer token válido.
    /// </summary>
    public static string TokenForjado(string? perfil)
    {
        var claims = new Dictionary<string, object>
        {
            ["sub"] = "999",
            ["fid"] = "999",
            ["email"] = "forjado@empresa.com.br",
            ["nome"] = "Pessoa Forjada",
        };
        if (perfil is not null)
        {
            claims["perfil"] = perfil;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "rh-system-teste",
            Audience = "rh-system-teste-web",
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.ChaveJwt)), SecurityAlgorithms.HmacSha256),
            Claims = claims,
        });
    }

    public static async Task<HttpResponseMessage> GetAsync(HttpClient cliente, string rota, string? token = null)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, rota);
        if (token is not null)
        {
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await cliente.SendAsync(pedido);
    }

    public static async Task<JsonElement> LerAsync(HttpResponseMessage resposta) =>
        JsonSerializer.Deserialize<JsonElement>(await resposta.Content.ReadAsStringAsync());

    public static async Task AssertProblemaAsync(HttpResponseMessage resposta, int status, string titulo, string detalhe)
    {
        Assert.Equal(status, (int)resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        var corpo = await LerAsync(resposta);
        Assert.Equal(status, corpo.GetProperty("status").GetInt32());
        Assert.Equal(titulo, corpo.GetProperty("title").GetString());
        Assert.Equal(detalhe, corpo.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrWhiteSpace(corpo.GetProperty("traceId").GetString()));
    }
}
