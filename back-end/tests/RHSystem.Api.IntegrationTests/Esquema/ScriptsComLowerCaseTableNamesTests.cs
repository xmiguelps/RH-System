using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Infrastructure.Esquema;

namespace RHSystem.Api.IntegrationTests.Esquema;

/// <summary>
/// O Flexible Server do Azure pode guardar os nomes de tabela em minúsculas (<c>lower_case_table_names=1</c>; suposto, não verificado).
/// Este teste sobe um servidor assim e aplica todos os scripts reais: um script que dependa de maiúsculas apareceria aqui, não no deploy.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ScriptsComLowerCaseTableNamesTests(MySqlLowerCaseFixture mysql) : IClassFixture<MySqlLowerCaseFixture>
{
    [Fact]
    public async Task Executor_aplica_todos_os_scripts_com_lower_case_table_names_1()
    {
        var conexao = await mysql.NovoBancoAsync();
        var scripts = new ScriptsEmbutidos().Carregar();

        await new ExecutorDeScripts(conexao, NullLogger<ExecutorDeScripts>.Instance).AplicarPendentesAsync(scripts, CancellationToken.None);

        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var modo = new MySqlCommand("SELECT @@lower_case_table_names", c);
        Assert.Equal(1L, Convert.ToInt64(await modo.ExecuteScalarAsync())); // controle: o servidor está mesmo nesse modo
        await using var contagem = new MySqlCommand("SELECT COUNT(*) FROM schema_versoes", c); // minúscula: nesse modo o nome é indiferente
        Assert.Equal(scripts.Count, Convert.ToInt32(await contagem.ExecuteScalarAsync()));
    }
}
