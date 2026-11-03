using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Infrastructure.Esquema;

namespace RHSystem.Api.IntegrationTests.Esquema;

/// <summary>
/// Aplica os scripts REAIS de "Banco de Dados/Scripts" (V001 em diante) com o usuário restrito ao banco. Os números esperados saem do
/// texto dos scripts, e não de constantes, para os testes acompanharem o grupo quando ele mudar a base.
/// </summary>
[Collection(MySqlCollection.Nome)]
public partial class ScriptsDoGrupoTests(MySqlFixture mysql)
{
    private static readonly string[] TabelasDaFatia0 =
        ["NIVEIS_DE_ACESSO", "FUNCIONARIOS", "CARGOS", "SETORES", "USUARIOS", "REFRESH_TOKENS", "SCHEMA_VERSOES"];

    // O trigger de USUARIOS recusa senha com menos de 40 caracteres ("armazene a senha como hash"): um valor do tamanho de um BCrypt.
    private const string HashDeTeste = "$2a$11$abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456";

    private static IReadOnlyList<ScriptSql> Scripts => new ScriptsEmbutidos().Carregar();

    [GeneratedRegex(@"^[ \t]*CREATE[ \t]+TABLE[ \t]+(?:IF[ \t]+NOT[ \t]+EXISTS[ \t]+)?`?(?<nome>\w+)`?", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CreateTable();

    [GeneratedRegex(@"^[ \t]*CREATE[ \t]+TRIGGER[ \t]+(?:IF[ \t]+NOT[ \t]+EXISTS[ \t]+)?`?(?<nome>\w+)`?", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CreateTrigger();

    [GeneratedRegex(@"^[ \t]*CREATE[ \t]+FUNCTION[ \t]+(?:IF[ \t]+NOT[ \t]+EXISTS[ \t]+)?`?(?<nome>\w+)`?", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CreateFunction();

    /// <summary>Nomes distintos criados no início de linha (comentário não conta).</summary>
    private static HashSet<string> NomesCriados(Regex padrao, IEnumerable<ScriptSql> scripts) =>
        scripts
            .SelectMany(s => padrao.Matches(s.Conteudo).Select(m => m.Groups["nome"].Value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static async Task<List<string>> ListarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        await using var leitor = await comando.ExecuteReaderAsync();
        var linhas = new List<string>();
        while (await leitor.ReadAsync())
        {
            linhas.Add(Convert.ToString(leitor.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!);
        }

        return linhas;
    }

    private static async Task<long> ContarAsync(string conexao, string sql) =>
        long.Parse((await ListarAsync(conexao, sql))[0]);

    private static async Task ExecutarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        await comando.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Todos_os_scripts_reais_aplicam_com_o_usuario_restrito_ao_banco_e_as_tabelas_usadas_pela_fatia_0_existem()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();

        var tabelas = await ListarAsync(conexao, "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'");
        foreach (var esperada in TabelasDaFatia0)
        {
            Assert.Contains(esperada, tabelas, StringComparer.OrdinalIgnoreCase);
        }

        Assert.Equal(Scripts.Count, await ContarAsync(conexao, "SELECT COUNT(*) FROM SCHEMA_VERSOES"));
        Assert.Equal([1, 2, 3, 4, 5], Scripts.Take(5).Select(s => s.Versao));
    }

    [Fact]
    public async Task Quantidade_de_triggers_no_banco_e_igual_ao_numero_de_CREATE_TRIGGER_dos_scripts()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();

        var esperados = NomesCriados(CreateTrigger(), Scripts);
        var noBanco = await ListarAsync(conexao, "SELECT TRIGGER_NAME FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA = DATABASE()");

        Assert.NotEmpty(esperados);
        Assert.Equal(esperados.Count, noBanco.Count);
        Assert.Empty(esperados.Except(noBanco, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Quantidade_de_funcoes_no_banco_e_igual_ao_numero_de_CREATE_FUNCTION_dos_scripts()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();

        var esperadas = NomesCriados(CreateFunction(), Scripts);
        var noBanco = await ListarAsync(conexao, "SELECT ROUTINE_NAME FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = DATABASE() AND ROUTINE_TYPE = 'FUNCTION'");

        Assert.NotEmpty(esperadas);
        Assert.Equal(esperadas.Count, noBanco.Count);
        Assert.Empty(esperadas.Except(noBanco, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Quantidade_de_tabelas_no_banco_e_igual_aos_nomes_distintos_de_CREATE_TABLE_nos_scripts_mais_SCHEMA_VERSOES()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();

        var esperadas = NomesCriados(CreateTable(), Scripts);
        esperadas.Add("SCHEMA_VERSOES"); // criada pelo executor, não por um script
        var noBanco = await ListarAsync(conexao, "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'");

        Assert.Equal(esperadas.Count, noBanco.Count);
        Assert.Empty(esperadas.Except(noBanco, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Niveis_de_acesso_semeados_sao_RH_Funcionario_e_Funcionario_Gestor_e_aplicar_de_novo_nao_duplica()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var consulta = "SELECT NOME_NIVEL_ACESSO FROM NIVEIS_DE_ACESSO ORDER BY NOME_NIVEL_ACESSO";

        Assert.Equal(["Funcionario", "Funcionario Gestor", "RH"], await ListarAsync(conexao, consulta));

        // Pelo executor: nada pendente. Direto: o INSERT IGNORE do V005 reaplicado pula o que já existe.
        await new ExecutorDeScripts(conexao, NullLogger<ExecutorDeScripts>.Instance).AplicarPendentesAsync(Scripts, CancellationToken.None);
        await ExecutarAsync(conexao, Scripts.Single(s => s.Versao == 5).Conteudo);

        Assert.Equal(["Funcionario", "Funcionario Gestor", "RH"], await ListarAsync(conexao, consulta));
    }

    /// <summary>
    /// A exceção de FÉRIAS (o holerite de férias é pago antes das férias, então pode ter competência futura) mora dentro do trigger
    /// do grupo no V002. Se o grupo trocar o V002 por uma versão sem ela, este teste é quem avisa.
    /// </summary>
    [Fact]
    public async Task Trigger_de_holerite_aceita_competencia_futura_so_para_ferias()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await ExecutarAsync(conexao, "INSERT INTO FUNCIONARIOS (NOME, DATA_ADMISSAO, ATIVO, EMAIL_CORPORATIVO) VALUES ('Holerite Teste', '2026-01-02', TRUE, 'holerite.teste@empresa.com.br')");
        var id = await ContarAsync(conexao, "SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = 'holerite.teste@empresa.com.br'");
        var futuro = DateTime.UtcNow.Year + 2;

        Task Inserir(string tipo, int mes, int ano) =>
            ExecutarAsync(conexao, $"INSERT INTO HOLERITES (ID_FUNCIONARIO, MES, ANO, TIPO_HOLERITE) VALUES ({id}, {mes}, {ano}, '{tipo}')");

        // Controle: o mesmo funcionário recebe um holerite mensal de uma competência passada (a recusa abaixo é por ser futura).
        await Inserir("MENSAL", 2, 2026);

        await Inserir("FERIAS", 6, futuro);

        foreach (var tipo in new[] { "MENSAL", "DECIMO_TERCEIRO" })
        {
            var erro = await Assert.ThrowsAsync<MySqlException>(() => Inserir(tipo, 6, futuro));
            Assert.Contains("Nao e possivel cadastrar holerite de competencia futura.", erro.Message);
        }
    }

    [Fact]
    public async Task Trigger_do_grupo_dispara_usuario_para_funcionario_inativo_e_recusado()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await ExecutarAsync(conexao, "INSERT INTO FUNCIONARIOS (NOME, DATA_ADMISSAO, ATIVO, EMAIL_CORPORATIVO) VALUES ('Ativa Teste', '2026-01-02', TRUE, 'ativa.teste@empresa.com.br')");
        await ExecutarAsync(conexao, "INSERT INTO FUNCIONARIOS (NOME, DATA_ADMISSAO, ATIVO, EMAIL_CORPORATIVO) VALUES ('Inativo Teste', '2026-01-02', FALSE, 'inativo.teste@empresa.com.br')");
        var idAtiva = await ContarAsync(conexao, "SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = 'ativa.teste@empresa.com.br'");
        var idInativo = await ContarAsync(conexao, "SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = 'inativo.teste@empresa.com.br'");

        // Controle: para o funcionário ativo o mesmo INSERT funciona.
        await ExecutarAsync(conexao, $"INSERT INTO USUARIOS (ID_FUNCIONARIO, SENHA) VALUES ({idAtiva}, '{HashDeTeste}')");

        var erro = await Assert.ThrowsAsync<MySqlException>(
            () => ExecutarAsync(conexao, $"INSERT INTO USUARIOS (ID_FUNCIONARIO, SENHA) VALUES ({idInativo}, '{HashDeTeste}')"));

        Assert.Contains("Nao e possivel criar usuario para funcionario inativo.", erro.Message);
    }
}
