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
        // So desempenho (nao mudam o comportamento): cada teste cria um banco aplicando todos os scripts, e o DDL do MySQL e lento por causa
        // do fsync. Sem durabilidade, num contêiner descartável.
        "--innodb-flush-log-at-trx-commit=0",
        "--sync-binlog=0",
        "--innodb-doublewrite=OFF",
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
    /// <summary>
    /// A collation padrão do banco de teste: <c>utf8mb4_unicode_ci</c>, a das tabelas. Para provar que o código que compara variável com coluna (as procedures do <c>V007</c>) não depende dela, rode a suíte com a variável de ambiente <c>RH_TESTES_COLACAO=utf8mb4_0900_ai_ci</c>
    /// (o padrão de um MySQL 8 criado sem opções, como o Flexible Server do Azure): as tabelas continuam em <c>unicode_ci</c> (o <c>V001</c> as declara), mas variáveis e parâmetros de procedure passam a ter a outra.
    /// </summary>
    private static string ColacaoDoBanco { get; } = Environment.GetEnvironmentVariable("RH_TESTES_COLACAO") is { Length: > 0 } c && c.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_') ? c : "utf8mb4_unicode_ci";

    public async Task<string> NovoBancoAsync()
    {
        // Cada banco de teste tem o próprio usuário, logo o próprio pool de conexões, e o pool de um teste já terminado só se limpa quando
        // alguém o usa de novo: sem isto as conexões ociosas se acumulam (a suíte inteira chegou a 146 de 151) e os testes do fim falham com
        // "Too many connections". Os testes da coleção rodam em fila, então, quando um pede o próximo banco, as conexões do anterior já estão ociosas.
        await MySqlConnection.ClearAllPoolsAsync();

        var sufixo = Guid.NewGuid().ToString("N")[..12];
        var banco = $"rh_t_{sufixo}";
        var usuario = $"u_{sufixo}";
        var senha = $"Sn{Guid.NewGuid():N}";

        await using var admin = new MySqlConnection(ConexaoDeAdministracao());
        await admin.OpenAsync();
        await ExecutarAsync(admin, $"CREATE DATABASE `{banco}` CHARACTER SET utf8mb4 COLLATE {ColacaoDoBanco}");
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
