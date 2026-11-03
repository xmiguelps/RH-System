using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using RHSystem.Infrastructure.Esquema;
using Testcontainers.MySql;

namespace RHSystem.Api.IntegrationTests.Infra;

/// <summary>
/// Um MySQL 8.4 real em contêiner, com os mesmos parâmetros de servidor do docker-compose.yml e do Azure.
/// Cada teste pede o seu próprio banco vazio (e um usuário restrito a ele) com <see cref="NovoBancoAsync"/>.
/// </summary>
public class MySqlFixture : IAsyncLifetime
{
    private const string SenhaRoot = "root_de_teste";

    // Mesmos parâmetros do docker-compose.yml. Sem o fuso, os testes que dependem de CURDATE() e NOW() (os triggers do grupo usam as
    // duas) rodariam em UTC e esconderiam diferenças que só apareceriam no deploy.
    private static readonly string[] ParametrosDoServidor =
    [
        "--character-set-server=utf8mb4",
        "--collation-server=utf8mb4_unicode_ci",
        "--default-time-zone=-03:00",
        "--log-bin-trust-function-creators=1",
    ];

    private readonly MySqlContainer _container;

    public MySqlFixture()
        : this([])
    {
    }

    /// <summary>Para variantes de servidor (por exemplo, com <c>--lower-case-table-names=1</c>): os parâmetros extras vão depois dos padrão.</summary>
    protected MySqlFixture(string[] parametrosExtras)
    {
        _container = new MySqlBuilder("mysql:8.4")
            .WithUsername("root")
            .WithPassword(SenhaRoot)
            .WithCommand([.. ParametrosDoServidor, .. parametrosExtras])
            .Build();
    }

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private string ConexaoDeAdministracao() =>
        new MySqlConnectionStringBuilder
        {
            Server = _container.Hostname,
            Port = _container.GetMappedPublicPort(3306),
            UserID = "root",
            Password = SenhaRoot,
            AllowUserVariables = true,
        }.ConnectionString;

    /// <summary>
    /// Cria um banco vazio e exclusivo (utf8mb4, utf8mb4_unicode_ci) e um usuário restrito a ele, com os mesmos privilégios que o
    /// usuário do Azure terá, e devolve a connection string desse usuário (assim um problema de privilégio aparece aqui).
    /// </summary>
    public async Task<string> NovoBancoAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..12];
        var banco = $"rh_t_{sufixo}";
        var usuario = $"u_{sufixo}";
        var senha = $"Sn{Guid.NewGuid():N}";

        await using var admin = new MySqlConnection(ConexaoDeAdministracao());
        await admin.OpenAsync();
        await ExecutarAsync(admin, $"CREATE DATABASE `{banco}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        await ExecutarAsync(admin, $"CREATE USER '{usuario}'@'%' IDENTIFIED BY '{senha}'");
        await ExecutarAsync(
            admin,
            $"GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, INDEX, REFERENCES, TRIGGER, CREATE ROUTINE, ALTER ROUTINE, EXECUTE ON `{banco}`.* TO '{usuario}'@'%'");

        return new MySqlConnectionStringBuilder(ConexaoDeAdministracao())
        {
            UserID = usuario,
            Password = senha,
            Database = banco,
        }.ConnectionString;
    }

    /// <summary>Como <see cref="NovoBancoAsync"/>, com todos os scripts embutidos (V001 em diante) já aplicados.</summary>
    public async Task<string> NovoBancoComScriptsAsync()
    {
        var conexao = await NovoBancoAsync();
        var executor = new ExecutorDeScripts(conexao, NullLogger<ExecutorDeScripts>.Instance);
        await executor.AplicarPendentesAsync(new ScriptsEmbutidos().Carregar(), CancellationToken.None);
        return conexao;
    }

    private static async Task ExecutarAsync(MySqlConnection conexao, string sql)
    {
        await using var comando = new MySqlCommand(sql, conexao);
        await comando.ExecuteNonQueryAsync();
    }
}

/// <summary>O mesmo servidor, com os nomes de tabela em minúsculas (suposto no Flexible Server do Azure; não verificado).</summary>
public sealed class MySqlLowerCaseFixture() : MySqlFixture(["--lower-case-table-names=1"]);

[CollectionDefinition(Nome)]
public sealed class MySqlCollection : ICollectionFixture<MySqlFixture>
{
    public const string Nome = "MySQL";
}
