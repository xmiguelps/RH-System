using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Demo;

/// <summary>
/// A demo de verdade nos testes: um banco novo com os scripts aplicados, o <c>Banco de Dados/Demo/seed_demo.sql</c> rodado do jeito que o dono vai rodar (o arquivo inteiro, numa sessão, com <c>@hoje</c> definido)
/// e a API inteira por cima, com o relógio fixo em <see cref="Hoje"/> às 09:42 em Brasília. Nada do elenco de teste: só as pessoas do seed, que entram pelo login de verdade.
/// </summary>
internal static class DemoDeTeste
{
    public const string Senha = "Demo#RH2026";

    public const string Dominio = "rhsystem-demo.example";

    /// <summary>Quarta-feira, o mesmo dia do relógio fixo dos outros testes (2026-10-07T12:42Z = 09:42 em Brasília).</summary>
    public static readonly DateOnly Hoje = new(2026, 10, 7);

    public static string Email(string local) => $"{local}@{Dominio}";

    /// <summary>Banco novo, scripts aplicados e o seed da demo rodado; devolve a API sobre ele, com o relógio fixo no dia do seed.</summary>
    public static async Task<ApiFactory> ApiAsync(MySqlFixture mysql)
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await RodarOSeedAsync(conexao, Hoje, Dominio);
        var api = new ApiFactory(conexao);
        api.Relogio.Fixar(new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc));
        return api;
    }

    public static async Task RodarOSeedAsync(string conexao, DateOnly hoje, string dominio)
    {
        var sql = File.ReadAllText(ArquivoDoSeed());
        var construtor = new MySqlConnectionStringBuilder(conexao) { AllowUserVariables = true };
        await using var c = new MySqlConnection(construtor.ConnectionString);
        await c.OpenAsync();
        await using (var variaveis = new MySqlCommand($"SET @hoje = '{hoje:yyyy-MM-dd}', @dominio = '{dominio}'", c))
        {
            await variaveis.ExecuteNonQueryAsync();
        }

        // O seed não usa DELIMITER nem tem ponto e vírgula dentro de texto: cada instrução termina em ";" no fim da linha.
        var semComentarios = string.Join('\n', sql.Replace("\r\n", "\n").Split('\n').Where(linha => !linha.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        foreach (var instrucao in semComentarios.Split(";\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(t => t.TrimEnd(';')).Where(t => t.Length > 0))
        {
            await using var comando = new MySqlCommand(instrucao, c) { CommandTimeout = 120 };
            try
            {
                await comando.ExecuteNonQueryAsync();
            }
            catch (MySqlException erro)
            {
                throw new InvalidOperationException($"O seed falhou: {erro.Message}\n--- instrução ---\n{instrucao[..Math.Min(instrucao.Length, 600)]}", erro);
            }
        }
    }

    public static string ArquivoDoSeed()
    {
        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
        {
            var candidato = Path.Combine(pasta.FullName, "Banco de Dados", "Demo", "seed_demo.sql");
            if (File.Exists(candidato))
            {
                return candidato;
            }
        }

        throw new FileNotFoundException("Banco de Dados/Demo/seed_demo.sql não foi encontrado.");
    }

    /// <summary>Entra pelo login de verdade (<c>POST /api/auth/entrar</c>) e devolve um cliente com o token.</summary>
    public static async Task<HttpClient> EntrarComoAsync(ApiFactory api, string local, string senha = Senha)
    {
        var cliente = api.CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/api/auth/entrar", new { email = Email(local), senha });
        Assert.True(resposta.IsSuccessStatusCode, $"O login de {local} falhou ({(int)resposta.StatusCode}).");
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", corpo.GetProperty("tokenDeAcesso").GetString());
        return cliente;
    }

    public static async Task<JsonElement> GetAsync(HttpClient cliente, string rota)
    {
        var resposta = await cliente.GetAsync(rota);
        Assert.True(resposta.IsSuccessStatusCode, $"GET {rota} respondeu {(int)resposta.StatusCode}: {await resposta.Content.ReadAsStringAsync()}");
        return await CorpoAsync(resposta);
    }
}
