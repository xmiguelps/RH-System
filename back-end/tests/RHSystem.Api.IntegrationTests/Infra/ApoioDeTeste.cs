using System.Globalization;
using MySqlConnector;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Infra;

/// <summary>
/// Os ajudantes de semente por SQL (sem passar pelo domínio nem pela API) que a <see cref="SementeDoCadastro"/> da 1b não tem e a fatia 1c usa: cargo com descrição e carga opcional, benefício com as três marcas,
/// benefício dado a alguém com o status do pedido SEMPRE explícito (a coluna não tem padrão), setor com gestor, pessoa com perfil e documento. Cada método devolve o id que criou.
/// </summary>
public static class ApoioDeTeste
{
    public static async Task<int> CriarCargoAsync(string conexao, string nome, string? descricao, decimal? cargaSemanalOuNull) =>
        await InserirAsync(conexao, "INSERT INTO CARGOS (NOME_CARGO, DESCRICAO, CARGA_HORARIA) VALUES (@nome, @descricao, @carga)", ("@nome", nome), ("@descricao", descricao), ("@carga", cargaSemanalOuNull));

    public static async Task<int> CriarBeneficioAsync(string conexao, string nome, string? descricao, bool opcional, bool aceitaDependentes, bool aceitaTrajeto) =>
        await InserirAsync(
            conexao,
            "INSERT INTO BENEFICIOS (NOME_BENEFICIO, DESCRICAO, OPCIONAL, ACEITA_DEPENDENTES, ACEITA_TRAJETO) VALUES (@nome, @descricao, @opcional, @dependentes, @trajeto)",
            ("@nome", nome),
            ("@descricao", descricao),
            ("@opcional", opcional),
            ("@dependentes", aceitaDependentes),
            ("@trajeto", aceitaTrajeto));

    /// <summary>Uma linha em <c>BENEFICIOS_FUNCIONARIOS</c>. <paramref name="statusDoPedido"/> é o texto do banco (<c>APROVADO</c>, <c>PENDENTE</c>...) e é obrigatório: a coluna não tem padrão. Pedido <c>PENDENTE</c> nunca é ativo (o <c>CHECK</c> do <c>V001</c>).</summary>
    public static async Task DarBeneficioAsync(string conexao, int funcionarioId, int beneficioId, bool ativo, string statusDoPedido) =>
        await ExecutarAsync(
            conexao,
            "INSERT INTO BENEFICIOS_FUNCIONARIOS (ID_FUNCIONARIO, ID_BENEFICIO, BENEFICIO_ATIVO, STATUS_PEDIDO, DATA_PEDIDO) VALUES (@pessoa, @beneficio, @ativo, @status, @pedido)",
            ("@pessoa", funcionarioId),
            ("@beneficio", beneficioId),
            ("@ativo", ativo),
            ("@status", statusDoPedido),
            ("@pedido", new DateTime(2026, 10, 1, 9, 0, 0)));

    /// <summary>Um setor; com <paramref name="gestorFuncionarioId"/> a pessoa vira gestor (linha em <c>GESTORES</c>, se ainda não for) e o setor aponta para ela.</summary>
    public static async Task<int> CriarSetorAsync(string conexao, string nome, int? gestorFuncionarioId = null)
    {
        int? gestorId = null;
        if (gestorFuncionarioId is { } pessoa)
        {
            gestorId = await GarantirGestorAsync(conexao, pessoa);
        }

        return await InserirAsync(conexao, "INSERT INTO SETORES (NOME_SETOR, ID_GESTOR) VALUES (@nome, @gestor)", ("@nome", nome), ("@gestor", gestorId));
    }

    /// <summary>A linha de <c>GESTORES</c> da pessoa (cria se faltar) e o <c>ID_GESTOR</c>.</summary>
    public static async Task<int> GarantirGestorAsync(string conexao, int funcionarioId)
    {
        await ExecutarAsync(conexao, "INSERT IGNORE INTO GESTORES (ID_FUNCIONARIO) VALUES (@pessoa)", ("@pessoa", funcionarioId));
        return Convert.ToInt32(await EscalarAsync(conexao, "SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = @pessoa", ("@pessoa", funcionarioId)), CultureInfo.InvariantCulture);
    }

    /// <summary>Uma pessoa (e-mail único) com o nível do perfil, no setor dado, ativa ou não (inativa com <c>DATA_DESLIGAMENTO</c>), admitida em <paramref name="admissao"/> (padrão 2026-01-02).</summary>
    public static async Task<int> CriarPessoaAsync(string conexao, string nome, Perfil perfil, int? setorId = null, bool ativo = true, DateOnly? admissao = null) =>
        await SementeDoCadastro.ColaboradorAsync(
            conexao,
            nome,
            $"pessoa.{Guid.NewGuid():N}@apoio.com.br",
            admissao ?? new DateOnly(2026, 1, 2),
            ativo,
            NomesDeNivel.DoPerfil(perfil),
            setorId);

    /// <summary>Uma linha de <c>FUNCIONARIOS_DOCUMENTOS</c> por SQL. <paramref name="tipo"/> e <paramref name="status"/> são os textos do banco; <c>ENVIADO</c> exige caminho e data de envio.</summary>
    public static async Task<int> CriarDocumentoAsync(
        string conexao,
        int funcionarioId,
        string tipo,
        string status,
        string? descricao = null,
        string? caminho = null,
        string? nomeOriginal = null,
        DateTime? pedidoEm = null,
        DateTime? enviadoEm = null) =>
        await InserirAsync(
            conexao,
            "INSERT INTO FUNCIONARIOS_DOCUMENTOS (ID_FUNCIONARIO, TIPO_DOCUMENTO, DESCRICAO, STATUS_DOCUMENTO, CAMINHO_ARQUIVO, NOME_ARQUIVO_ORIGINAL, DATA_PEDIDO, DATA_ENVIO) VALUES (@pessoa, @tipo, @descricao, @status, @caminho, @nome, @pedido, @envio)",
            ("@pessoa", funcionarioId),
            ("@tipo", tipo),
            ("@descricao", descricao),
            ("@status", status),
            ("@caminho", caminho),
            ("@nome", nomeOriginal),
            ("@pedido", pedidoEm),
            ("@envio", enviadoEm));

    public static async Task ExecutarAsync(string conexao, string sql, params (string Nome, object? Valor)[] parametros)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = Comando(c, sql, parametros);
        await comando.ExecuteNonQueryAsync();
    }

    public static async Task<object?> EscalarAsync(string conexao, string sql, params (string Nome, object? Valor)[] parametros)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = Comando(c, sql, parametros);
        var valor = await comando.ExecuteScalarAsync();
        return valor is DBNull ? null : valor;
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
