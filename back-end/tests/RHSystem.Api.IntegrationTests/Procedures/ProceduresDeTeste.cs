using System.Globalization;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Procedures;

/// <summary>
/// Apoio dos testes das procedures do <c>V007</c>: chamar uma procedure como o cliente <c>mysql</c> faria (<c>CALL SP_X(...)</c>, com os parâmetros <c>OUT</c> lidos de variáveis de sessão), conferir a recusa (<c>SIGNAL SQLSTATE '45000'</c>) e montar as pessoas de apoio com SQL cru. Os testes rodam num MySQL 8.4 de verdade, com
/// os scripts <c>V001</c> em diante já aplicados, sem a API: a API não chama procedures.
/// </summary>
internal static class ProceduresDeTeste
{
    /// <summary>Marca um parâmetro <c>OUT</c> na lista de argumentos de <see cref="ChamarAsync"/>; o valor volta no dicionário, pelo <paramref name="nome"/>.</summary>
    public sealed record Saida(string Nome);

    /// <summary>Um banco novo com todos os scripts embutidos aplicados.</summary>
    public static Task<string> NovoBancoAsync(MySqlFixture mysql) => mysql.NovoBancoComScriptsAsync();

    /// <summary>
    /// <c>CALL procedure(argumentos...)</c> numa conexão só (as variáveis de sessão dos <c>OUT</c> vivem nela). Os argumentos entram na ordem dos parâmetros da procedure; um <see cref="Saida"/> ocupa o lugar de um parâmetro <c>OUT</c> (ou <c>INOUT</c> sem valor). Devolve os valores lidos dos <c>OUT</c>, pelo nome que cada
    /// <see cref="Saida"/> trouxe (<c>null</c> do banco vira <c>null</c>).
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, object?>> ChamarAsync(string conexao, string procedure, params object?[] argumentos)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        return await ChamarNaConexaoAsync(c, procedure, argumentos);
    }

    /// <summary>Como <see cref="ChamarAsync"/>, numa conexão que o teste já abriu (para chamar duas procedures na mesma sessão, ou com uma transação do próprio teste).</summary>
    public static async Task<IReadOnlyDictionary<string, object?>> ChamarNaConexaoAsync(MySqlConnection conexao, string procedure, params object?[] argumentos)
    {
        var lista = new List<string>();
        await using var comando = new MySqlCommand { Connection = conexao };
        var saidas = new List<string>();
        for (var i = 0; i < argumentos.Length; i++)
        {
            if (argumentos[i] is Saida saida)
            {
                lista.Add($"@saida_{saida.Nome}");
                saidas.Add(saida.Nome);
            }
            else
            {
                lista.Add($"@p{i.ToString(CultureInfo.InvariantCulture)}");
                comando.Parameters.AddWithValue($"@p{i.ToString(CultureInfo.InvariantCulture)}", argumentos[i] ?? DBNull.Value);
            }
        }

        comando.CommandText = $"CALL {procedure}({string.Join(", ", lista)})";
        await comando.ExecuteNonQueryAsync();

        var resultado = new Dictionary<string, object?>();
        if (saidas.Count > 0)
        {
            await using var leitura = new MySqlCommand($"SELECT {string.Join(", ", saidas.Select(s => $"@saida_{s}"))}", conexao);
            await using var linha = await leitura.ExecuteReaderAsync();
            await linha.ReadAsync();
            for (var i = 0; i < saidas.Count; i++)
            {
                resultado[saidas[i]] = await linha.IsDBNullAsync(i) ? null : linha.GetValue(i);
            }
        }

        return resultado;
    }

    /// <summary>
    /// Chama a procedure esperando a recusa de negócio (<c>SIGNAL SQLSTATE '45000'</c>, ou o erro de um trigger ou de uma restrição do esquema) e devolve a mensagem do servidor, para o teste conferir o texto. Se a procedure não recusar, o teste cai.
    /// </summary>
    public static async Task<string> RecusaAsync(string conexao, string procedure, params object?[] argumentos)
    {
        var erro = await Assert.ThrowsAsync<MySqlException>(() => ChamarAsync(conexao, procedure, argumentos));
        return erro.Message;
    }

    /// <summary>Como <see cref="RecusaAsync"/>, e exige que seja uma recusa de negócio da procedure (SQLSTATE 45000), não uma falha de sintaxe, de permissão ou de chave estrangeira.</summary>
    public static async Task<string> RecusaDeNegocioAsync(string conexao, string procedure, params object?[] argumentos)
    {
        var erro = await Assert.ThrowsAsync<MySqlException>(() => ChamarAsync(conexao, procedure, argumentos));
        Assert.Equal("45000", erro.SqlState);
        return erro.Message;
    }

    public static int Inteiro(object? valor) => Convert.ToInt32(valor, CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------- dados de apoio (SQL cru)

    public static async Task<int> NivelAsync(string conexao, string nome) =>
        (int)await Sql.ContarAsync(conexao, $"SELECT ID_NIVEL_ACESSO FROM NIVEIS_DE_ACESSO WHERE NOME_NIVEL_ACESSO = '{nome}'"); // 'RH', 'Funcionario' e 'Funcionario Gestor' vêm do V005

    public static async Task<int> CargoAsync(string conexao, string? nome = null, decimal cargaHoraria = 44m)
    {
        nome ??= $"Cargo {Guid.NewGuid():N}";
        await Sql.ExecutarAsync(conexao, $"INSERT INTO CARGOS (NOME_CARGO, CARGA_HORARIA) VALUES ('{nome}', {cargaHoraria.ToString(CultureInfo.InvariantCulture)})");
        return (int)await Sql.ContarAsync(conexao, $"SELECT ID_CARGO FROM CARGOS WHERE NOME_CARGO = '{nome}'");
    }

    public static async Task<int> SetorAsync(string conexao, string? nome = null)
    {
        nome ??= $"Setor {Guid.NewGuid():N}";
        await Sql.ExecutarAsync(conexao, $"INSERT INTO SETORES (NOME_SETOR) VALUES ('{nome}')");
        return (int)await Sql.ContarAsync(conexao, $"SELECT ID_SETOR FROM SETORES WHERE NOME_SETOR = '{nome}'");
    }

    /// <summary>Uma pessoa ativa, com cargo e setor (criados na hora, se não vierem) e e-mail único. O nível padrão é <c>Funcionario</c>.</summary>
    public static async Task<int> FuncionarioAsync(string conexao, string? nome = null, string admissao = "2025-01-06", string nivel = "Funcionario", int? setorId = null, int? gestorId = null, bool ativo = true)
    {
        nome ??= $"Pessoa {Guid.NewGuid():N}";
        var cargo = await CargoAsync(conexao);
        var setor = setorId ?? await SetorAsync(conexao);
        var nivelId = await NivelAsync(conexao, nivel);
        var email = $"{Guid.NewGuid():N}@procedures.example";
        await Sql.ExecutarAsync(conexao, $"INSERT INTO FUNCIONARIOS (NOME, DATA_ADMISSAO, ATIVO, EMAIL_CORPORATIVO, ID_CARGO, ID_SETOR, ID_NIVEL_ACESSO, ID_GESTOR) VALUES ('{nome}', '{admissao}', {(ativo ? "TRUE" : "FALSE")}, '{email}', {cargo}, {setor}, {nivelId}, {(gestorId is null ? "NULL" : gestorId.Value.ToString(CultureInfo.InvariantCulture))})");
        return (int)await Sql.ContarAsync(conexao, $"SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = '{email}'");
    }

    /// <summary>Faz da pessoa um gestor (linha em <c>GESTORES</c>) e devolve o <c>ID_GESTOR</c>.</summary>
    public static async Task<int> GestorAsync(string conexao, int funcionarioId)
    {
        await Sql.ExecutarAsync(conexao, $"INSERT INTO GESTORES (ID_FUNCIONARIO) VALUES ({funcionarioId})");
        return (int)await Sql.ContarAsync(conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {funcionarioId}");
    }
}
