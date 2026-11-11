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

    public static async Task DetalhesAsync(
        string conexao,
        int funcionarioId,
        string cpf,
        string? telefone = "11987654410",
        string? rg = null,
        string? rgOrgaoEmissor = null,
        string? emailPessoal = null,
        string? cep = null,
        string? logradouro = null,
        string? numero = null,
        string? complemento = null,
        string? bairro = null,
        string? cidade = null,
        string? uf = null) =>
        await ExecutarAsync(
            conexao,
            "INSERT INTO FUNCIONARIOS_DETALHES (ID_FUNCIONARIO, CPF, TELEFONE, RG, RG_ORGAO_EMISSOR, EMAIL_PESSOAL, CEP, LOGRADOURO, NUMERO, COMPLEMENTO, BAIRRO, CIDADE, UF) VALUES (@id, @cpf, @telefone, @rg, @orgao, @emailPessoal, @cep, @logradouro, @numero, @complemento, @bairro, @cidade, @uf)",
            ("@id", funcionarioId),
            ("@cpf", cpf),
            ("@telefone", (object?)telefone),
            ("@rg", (object?)rg),
            ("@orgao", (object?)rgOrgaoEmissor),
            ("@emailPessoal", (object?)emailPessoal),
            ("@cep", (object?)cep),
            ("@logradouro", (object?)logradouro),
            ("@numero", (object?)numero),
            ("@complemento", (object?)complemento),
            ("@bairro", (object?)bairro),
            ("@cidade", (object?)cidade),
            ("@uf", (object?)uf));

    /// <summary>
    /// Um colaborador por SQL (sem passar pelo domínio): ativo (ou inativo, com <c>DATA_DESLIGAMENTO</c>), com cargo, setor, gestor (<c>ID_GESTOR</c> de <c>GESTORES</c>), tipo de contrato em texto livre e matrícula opcionais.
    /// <paramref name="nivel"/> é o nome do nível (<c>Funcionario</c>, <c>Funcionario Gestor</c>, <c>RH</c>); nulo deixa sem nível.
    /// </summary>
    public static async Task<int> ColaboradorAsync(
        string conexao,
        string nome,
        string email,
        DateOnly admissao,
        bool ativo = true,
        string? nivel = "Funcionario",
        int? setorId = null,
        int? cargoId = null,
        int? gestorId = null,
        string? tipoContrato = "CLT",
        string? matricula = null,
        DateOnly? nascimento = null,
        DateOnly? desligamento = null) =>
        await InserirAsync(
            conexao,
            "INSERT INTO FUNCIONARIOS (NOME, EMAIL_CORPORATIVO, DATA_ADMISSAO, ATIVO, ID_NIVEL_ACESSO, ID_CARGO, ID_SETOR, ID_GESTOR, TIPO_CONTRATO, MATRICULA, DATA_NASCIMENTO, DATA_DESLIGAMENTO) VALUES (@nome, @email, @admissao, @ativo, (SELECT ID_NIVEL_ACESSO FROM NIVEIS_DE_ACESSO WHERE NOME_NIVEL_ACESSO = @nivel), @cargo, @setor, @gestor, @contrato, @matricula, @nascimento, @desligamento)",
            ("@nome", nome),
            ("@email", email),
            ("@admissao", admissao.ToDateTime(TimeOnly.MinValue)),
            ("@ativo", ativo),
            ("@nivel", (object?)nivel),
            ("@cargo", (object?)cargoId),
            ("@setor", (object?)setorId),
            ("@gestor", (object?)gestorId),
            ("@contrato", (object?)tipoContrato),
            ("@matricula", (object?)matricula),
            ("@nascimento", (object?)nascimento?.ToDateTime(TimeOnly.MinValue)),
            ("@desligamento", (object?)(ativo ? desligamento : desligamento ?? new DateOnly(2026, 9, 30))?.ToDateTime(TimeOnly.MinValue)));

    /// <summary>Uma linha de benefício do colaborador (por padrão ativa e aprovada).</summary>
    public static async Task BeneficioDoColaboradorAsync(string conexao, int funcionarioId, int beneficioId, bool ativo = true, string status = "APROVADO") =>
        await ExecutarAsync(
            conexao,
            "INSERT INTO BENEFICIOS_FUNCIONARIOS (ID_FUNCIONARIO, ID_BENEFICIO, BENEFICIO_ATIVO, STATUS_PEDIDO, DATA_PEDIDO) VALUES (@func, @beneficio, @ativo, @status, @pedido)",
            ("@func", funcionarioId),
            ("@beneficio", beneficioId),
            ("@ativo", ativo),
            ("@status", status),
            ("@pedido", new DateTime(2026, 10, 1, 9, 0, 0)));

    /// <summary>Um desligamento AGENDADO para o dia dado (a pessoa continua ativa até o dia passar).</summary>
    public static async Task DesligamentoAgendadoAsync(string conexao, int funcionarioId, DateOnly ultimoDia) =>
        await ExecutarAsync(
            conexao,
            "INSERT INTO DESLIGAMENTOS_AGENDADOS (ID_FUNCIONARIO, ULTIMO_DIA_TRABALHO, SITUACAO, CRIADO_EM) VALUES (@func, @dia, 'AGENDADO', @criado)",
            ("@func", funcionarioId),
            ("@dia", ultimoDia.ToDateTime(TimeOnly.MinValue)),
            ("@criado", new DateTime(2026, 10, 1, 12, 0, 0)));

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
