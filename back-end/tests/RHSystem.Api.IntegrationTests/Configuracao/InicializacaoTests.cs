using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Infrastructure.Esquema;

namespace RHSystem.Api.IntegrationTests.Configuracao;

/// <summary>Scripts na inicialização (controlados por configuração) e o seed do administrador.</summary>
[Collection(MySqlCollection.Nome)]
public class InicializacaoTests(MySqlFixture mysql)
{
    private const string SenhaDoSeed = "Segredo#Seed1";

    private sealed class FonteFalsa(IReadOnlyList<ScriptSql> scripts) : IFonteDeScripts
    {
        public IReadOnlyList<ScriptSql> Carregar() => scripts;
    }

    private static async Task<long> ContarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        return Convert.ToInt64(await comando.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Mensagens(Exception? erro)
    {
        var partes = new List<string>();
        for (var atual = erro; atual is not null; atual = atual.InnerException)
        {
            partes.Add(atual.Message);
        }

        return string.Join(" | ", partes);
    }

    private static WebApplicationFactory<Program> ComSeed(string conexao, ColetorDeLogs? logs = null) =>
        new ApiFactory(conexao).WithWebHostBuilder(b =>
        {
            b.UseSetting("Seed:AdminNome", "Administrador do Sistema");
            b.UseSetting("Seed:AdminEmail", "  Admin.Seed@Empresa.com.br ");
            b.UseSetting("Seed:AdminSenha", SenhaDoSeed);
            if (logs is not null)
            {
                b.ConfigureLogging(l => l.AddProvider(logs));
            }
        });

    [Fact]
    public async Task Inicializacao_aplica_os_scripts_quando_AplicarScriptsAoIniciar_e_true()
    {
        var conexao = await mysql.NovoBancoAsync(); // banco novo e vazio

        // Controle: com a opção desligada (o padrão do ApiFactory), subir a API não cria nada.
        using (var semAplicar = new ApiFactory(conexao))
        {
            semAplicar.CreateClient();
        }

        Assert.Equal(0, await ContarAsync(conexao, "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE()"));

        using var fabrica = new ApiFactory(conexao).WithWebHostBuilder(b => b.UseSetting("Database:AplicarScriptsAoIniciar", "true"));
        var cliente = fabrica.CreateClient();

        Assert.Equal(new ScriptsEmbutidos().Carregar().Count, await ContarAsync(conexao, "SELECT COUNT(*) FROM SCHEMA_VERSOES"));
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task Inicializacao_recusa_subir_se_um_script_aplicado_foi_alterado_com_IFonteDeScripts_falsa() // Review Focus 2
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var alterados = new ScriptsEmbutidos().Carregar()
            .Select(s => s.Versao == 1 ? s with { Conteudo = s.Conteudo + "\n-- alterado depois de aplicado\n" } : s)
            .ToList();
        using var fabrica = new ApiFactory(conexao).WithWebHostBuilder(b =>
        {
            b.UseSetting("Database:AplicarScriptsAoIniciar", "true");
            b.ConfigureTestServices(servicos =>
            {
                servicos.RemoveAll<IFonteDeScripts>();
                servicos.AddSingleton<IFonteDeScripts>(new FonteFalsa(alterados));
            });
        });

        var erro = Assert.ThrowsAny<Exception>(() => fabrica.CreateClient());

        Assert.Contains("O script V001 já foi aplicado e foi alterado", Mensagens(erro));
    }

    [Fact]
    public async Task Seed_cria_funcionario_e_usuario_admin_uma_unica_vez_e_nao_cria_sem_configuracao()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();

        // Sem configuração de seed: nada é criado.
        using (var semSeed = new ApiFactory(conexao))
        {
            semSeed.CreateClient();
        }

        Assert.Equal(0, await ContarAsync(conexao, "SELECT COUNT(*) FROM USUARIOS"));

        // Com a configuração: cria uma vez. O admin entra, e GET /api/auth/eu traz cargo e setor nulos.
        using (var primeira = ComSeed(conexao))
        {
            var cliente = primeira.CreateClient();
            var entrada = await cliente.PostAsJsonAsync("/api/auth/entrar", new { email = "admin.seed@empresa.com.br", senha = SenhaDoSeed });
            Assert.Equal(HttpStatusCode.OK, entrada.StatusCode);
            var token = JsonSerializer.Deserialize<JsonElement>(await entrada.Content.ReadAsStringAsync()).GetProperty("tokenDeAcesso").GetString();

            using var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/auth/eu");
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var eu = JsonSerializer.Deserialize<JsonElement>(await (await cliente.SendAsync(pedido)).Content.ReadAsStringAsync());
            Assert.Equal("RH", eu.GetProperty("perfil").GetString());
            Assert.Equal("Administrador do Sistema", eu.GetProperty("nome").GetString());
            Assert.Equal("admin.seed@empresa.com.br", eu.GetProperty("email").GetString()); // e-mail normalizado
            Assert.Equal(JsonValueKind.Null, eu.GetProperty("cargo").ValueKind);
            Assert.Equal(JsonValueKind.Null, eu.GetProperty("setor").ValueKind);
        }

        // Subir de novo com o mesmo banco não duplica (já existe usuário).
        using (var segunda = ComSeed(conexao))
        {
            segunda.CreateClient();
        }

        Assert.Equal(1, await ContarAsync(conexao, "SELECT COUNT(*) FROM USUARIOS"));
        Assert.Equal(1, await ContarAsync(conexao, "SELECT COUNT(*) FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = 'admin.seed@empresa.com.br'"));
        Assert.Equal(0, await ContarAsync(conexao, "SELECT SENHA_PROVISORIA FROM USUARIOS")); // senha de verdade, não provisória
        Assert.Equal(1, await ContarAsync(conexao, "SELECT COUNT(*) FROM FUNCIONARIOS WHERE ID_CARGO IS NULL AND ID_SETOR IS NULL AND MATRICULA IS NULL")); // sem cargo, setor e matrícula
        Assert.InRange(await ContarAsync(conexao, "SELECT ABS(DATEDIFF(DATA_ADMISSAO, CURDATE())) FROM FUNCIONARIOS"), 0, 1); // admissão = hoje no fuso -03:00
    }

    [Fact]
    public async Task Seed_nunca_registra_a_senha_em_log()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var logs = new ColetorDeLogs();

        using (var fabrica = ComSeed(conexao, logs))
        {
            fabrica.CreateClient();
        }

        Assert.NotEmpty(logs.Mensagens);
        Assert.DoesNotContain(logs.Mensagens, m => m.Contains(SenhaDoSeed, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Seed_falha_com_mensagem_clara_se_o_nivel_RH_nao_existe()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var c = new MySqlConnection(conexao))
        {
            await c.OpenAsync();
            await using var apagar = new MySqlCommand("DELETE FROM NIVEIS_DE_ACESSO WHERE NOME_NIVEL_ACESSO = 'RH'", c);
            await apagar.ExecuteNonQueryAsync();
        }

        using var fabrica = ComSeed(conexao);
        var erro = Assert.ThrowsAny<Exception>(() => fabrica.CreateClient());

        Assert.Contains("RH", Mensagens(erro));
        Assert.Contains("nível", Mensagens(erro), StringComparison.OrdinalIgnoreCase);
    }
}
