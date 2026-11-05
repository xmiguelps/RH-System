using System.Globalization;
using MySqlConnector;
using RHSystem.Application.Autenticacao;
using RHSystem.Infrastructure.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Infra;

/// <summary>
/// Semeia, por SQL (sem passar pelo domínio nem pela API), o que os testes do cadastro precisam além do <see cref="ElencoDeTeste"/>: cargo com carga semanal, benefício,
/// setor, dados pessoais, tipo de contrato e senha. Cada método devolve o que criou.
/// </summary>
public static class SementeDoCadastro
{
    public static async Task<int> CargoAsync(string conexao, string nome, decimal cargaSemanal) =>
        await InserirAsync(conexao, "INSERT INTO CARGOS (NOME_CARGO, CARGA_HORARIA) VALUES (@nome, @carga)", ("@nome", nome), ("@carga", cargaSemanal));

    public static async Task<int> BeneficioAsync(string conexao, string nome) =>
        await InserirAsync(conexao, "INSERT INTO BENEFICIOS (NOME_BENEFICIO) VALUES (@nome)", ("@nome", nome));

    /// <summary>Um setor com o <c>ID_GESTOR</c> de <c>GESTORES</c> dado (ou sem gestor).</summary>
    public static async Task<int> SetorAsync(string conexao, string nome, int? gestorId = null) =>
        await InserirAsync(conexao, "INSERT INTO SETORES (NOME_SETOR, ID_GESTOR) VALUES (@nome, @gestor)", ("@nome", nome), ("@gestor", (object?)gestorId));

    public static async Task DetalhesAsync(string conexao, int funcionarioId, string cpf, string? telefone = "11987654410") =>
        await ExecutarAsync(
            conexao,
            "INSERT INTO FUNCIONARIOS_DETALHES (ID_FUNCIONARIO, CPF, TELEFONE) VALUES (@id, @cpf, @telefone)",
            ("@id", funcionarioId),
            ("@cpf", cpf),
            ("@telefone", (object?)telefone));

    /// <summary>O banco guarda texto livre: serve para pôr um valor que a API não conhece ("PJ") e ver a leitura não cair.</summary>
    public static async Task DefinirTipoDeContratoAsync(string conexao, int funcionarioId, string texto) =>
        await ExecutarAsync(conexao, "UPDATE FUNCIONARIOS SET TIPO_CONTRATO = @texto WHERE ID_FUNCIONARIO = @id", ("@texto", texto), ("@id", funcionarioId));

    /// <summary>Grava o hash que o <see cref="SenhaHasher"/> de verdade gera (o login confere pelo mesmo caminho).</summary>
    public static async Task DefinirSenhaAsync(string conexao, PessoaDeTeste pessoa, string senha) =>
        await ExecutarAsync(
            conexao,
            "UPDATE USUARIOS SET SENHA = @hash, SENHA_PROVISORIA = FALSE WHERE ID_USUARIO = @id",
            ("@hash", new SenhaHasher().Gerar(senha)),
            ("@id", pessoa.UsuarioId));

    private static async Task ExecutarAsync(string conexao, string sql, params (string Nome, object? Valor)[] parametros)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = Comando(c, sql, parametros);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<int> InserirAsync(string conexao, string sql, params (string Nome, object? Valor)[] parametros)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using (var insercao = Comando(c, sql, parametros))
        {
            await insercao.ExecuteNonQueryAsync();
        }

        await using var ultimo = new MySqlCommand("SELECT LAST_INSERT_ID()", c);
        return Convert.ToInt32(await ultimo.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static MySqlCommand Comando(MySqlConnection conexao, string sql, (string Nome, object? Valor)[] parametros)
    {
        var comando = new MySqlCommand(sql, conexao);
        foreach (var (nome, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nome, valor ?? DBNull.Value);
        }

        return comando;
    }
}
