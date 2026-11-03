using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using RHSystem.Application.Autenticacao;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Infra;

public sealed record PessoaDeTeste(int FuncionarioId, int UsuarioId, string Nome, string Email, Perfil Perfil);

/// <summary>
/// As pessoas de <c>contrato/ELENCO.md</c> de que os testes de escopo precisam (os mesmos nomes; "hoje" é <c>2026-10-07T12:42:00Z</c>, o dia dos exemplos).
/// Comercial (gestor Carlos) tem <see cref="MembroA"/> e <see cref="MembroB"/> na equipe; <see cref="EmAdmissao"/> (começa em 2026-10-13) e
/// <see cref="Inativo"/> estão no setor mas fora da equipe; <see cref="DeOutroSetor"/> é do Financeiro (gestora Marina); <see cref="GestorSemSetor"/> tem
/// perfil de gestor e linha em <c>GESTORES</c>, sem setor.
/// </summary>
public sealed class ElencoDeTeste
{
    public static readonly DateTime Hoje = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);

    public required PessoaDeTeste Rh { get; init; }

    public required PessoaDeTeste Gestor { get; init; }

    public required PessoaDeTeste MembroA { get; init; }

    public required PessoaDeTeste MembroB { get; init; }

    public required PessoaDeTeste EmAdmissao { get; init; }

    public required PessoaDeTeste Inativo { get; init; }

    public required PessoaDeTeste GestorDoFinanceiro { get; init; }

    public required PessoaDeTeste DeOutroSetor { get; init; }

    public required PessoaDeTeste GestorSemSetor { get; init; }

    public required int SetorComercialId { get; init; }

    public required int SetorFinanceiroId { get; init; }

    internal ConcurrentDictionary<int, string> TokensPorUsuario { get; init; } = new();

    /// <summary>
    /// Semeia o elenco no banco da <paramref name="api"/> e fixa o relógio falso em <see cref="Hoje"/>. A ordem é a que os triggers e as chaves
    /// circulares pedem: funcionários sem setor, <c>GESTORES</c>, <c>SETORES</c> com o <c>ID_GESTOR</c>, <c>ID_SETOR</c> e <c>ID_GESTOR</c> dos
    /// funcionários, usuários (só de funcionário ativo) e, por último, a inativação.
    /// </summary>
    internal static async Task<ElencoDeTeste> SemearAsync(ApiFactory api)
    {
        api.Relogio.Fixar(Hoje);
        var conexao = api.Conexao;
        await using var db = ContextoDeTeste.Novo(conexao);
        var niveis = await db.NiveisDeAcesso.ToDictionaryAsync(n => n.Nome);

        var antes = new DateOnly(2026, 1, 2);
        var pessoas = new Dictionary<string, Funcionario>
        {
            ["Rh"] = Funcionario.Criar("Ana Souza", "ana.souza@empresa.com.br", antes, niveis["RH"]),
            ["Gestor"] = Funcionario.Criar("Carlos Ribeiro", "carlos.ribeiro@empresa.com.br", antes, niveis["Funcionario Gestor"]),
            ["MembroA"] = Funcionario.Criar("Diego Farias", "diego.farias@empresa.com.br", antes, niveis["Funcionario"]),
            ["MembroB"] = Funcionario.Criar("Júlia Nunes", "julia.nunes@empresa.com.br", antes, niveis["Funcionario"]),
            ["EmAdmissao"] = Funcionario.Criar("Rafael Torres", "rafael.torres@empresa.com.br", new DateOnly(2026, 10, 13), niveis["Funcionario"]),
            ["Inativo"] = Funcionario.Criar("Henrique Alves", "henrique.alves@empresa.com.br", antes, niveis["Funcionario"]),
            ["GestorDoFinanceiro"] = Funcionario.Criar("Marina Costa", "marina.costa@empresa.com.br", antes, niveis["Funcionario Gestor"]),
            ["DeOutroSetor"] = Funcionario.Criar("Bruno Lima", "bruno.lima@empresa.com.br", antes, niveis["Funcionario"]),
            ["GestorSemSetor"] = Funcionario.Criar("Paulo Vieira", "paulo.vieira@empresa.com.br", antes, niveis["Funcionario Gestor"]),
        };
        db.Funcionarios.AddRange(pessoas.Values);
        await db.SaveChangesAsync();

        int Id(string chave) => pessoas[chave].Id;

        // GESTORES: os três gestores (Paulo fica sem setor).
        foreach (var chave in new[] { "Gestor", "GestorDoFinanceiro", "GestorSemSetor" })
        {
            await ExecutarAsync(conexao, $"INSERT INTO GESTORES (ID_FUNCIONARIO) VALUES ({Id(chave)})");
        }

        var gestorCarlos = await EscalarAsync(conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {Id("Gestor")}");
        var gestorMarina = await EscalarAsync(conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {Id("GestorDoFinanceiro")}");

        // SETORES com o gestor, e depois o setor de cada pessoa (a FK circular obriga esta ordem).
        await ExecutarAsync(conexao, $"INSERT INTO SETORES (NOME_SETOR, ID_GESTOR) VALUES ('Comercial', {gestorCarlos}), ('Financeiro', {gestorMarina})");
        var comercial = await EscalarAsync(conexao, "SELECT ID_SETOR FROM SETORES WHERE NOME_SETOR = 'Comercial'");
        var financeiro = await EscalarAsync(conexao, "SELECT ID_SETOR FROM SETORES WHERE NOME_SETOR = 'Financeiro'");
        foreach (var chave in new[] { "Gestor", "MembroA", "MembroB", "EmAdmissao", "Inativo" })
        {
            await ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = {comercial} WHERE ID_FUNCIONARIO = {Id(chave)}");
        }

        foreach (var chave in new[] { "GestorDoFinanceiro", "DeOutroSetor" })
        {
            await ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = {financeiro} WHERE ID_FUNCIONARIO = {Id(chave)}");
        }

        // ID_GESTOR de FUNCIONARIOS: o gestor do setor, para os membros. O gestor do setor (o trigger não o deixa ser gestor de si mesmo), quem
        // não tem setor e quem é do RH ficam com NULL. A ficha da 1b lê o `gestor` desta coluna.
        foreach (var chave in new[] { "MembroA", "MembroB", "EmAdmissao", "Inativo" })
        {
            await ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ID_GESTOR = {gestorCarlos} WHERE ID_FUNCIONARIO = {Id(chave)}");
        }

        await ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ID_GESTOR = {gestorMarina} WHERE ID_FUNCIONARIO = {Id("DeOutroSetor")}");

        // Usuários: o trigger só aceita funcionário ativo e hash de pelo menos 40 caracteres (aqui ninguém entra pela senha: o token sai do emissor).
        foreach (var funcionario in pessoas.Values)
        {
            db.Usuarios.Add(Usuario.Criar(funcionario, new string('x', 60)));
        }

        await db.SaveChangesAsync();

        // Por último, a inativação (o trigger do grupo inativa também o usuário).
        await ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {Id("Inativo")}");

        var emissor = api.Services.GetRequiredService<IEmissorDeTokens>();
        var tokens = new ConcurrentDictionary<int, string>();
        var usuarios = await db.Usuarios.Include(u => u.Funcionario).ThenInclude(f => f.NivelDeAcesso).AsNoTracking().ToListAsync();

        PessoaDeTeste Pessoa(string chave)
        {
            var usuario = usuarios.Single(u => u.FuncionarioId == Id(chave));
            tokens[usuario.Id] = emissor.EmitirAcesso(usuario).Token;
            return new PessoaDeTeste(usuario.FuncionarioId, usuario.Id, usuario.Funcionario.Nome, usuario.Funcionario.EmailCorporativo!, usuario.Funcionario.Perfil!.Value);
        }

        return new ElencoDeTeste
        {
            Rh = Pessoa("Rh"),
            Gestor = Pessoa("Gestor"),
            MembroA = Pessoa("MembroA"),
            MembroB = Pessoa("MembroB"),
            EmAdmissao = Pessoa("EmAdmissao"),
            Inativo = Pessoa("Inativo"),
            GestorDoFinanceiro = Pessoa("GestorDoFinanceiro"),
            DeOutroSetor = Pessoa("DeOutroSetor"),
            GestorSemSetor = Pessoa("GestorSemSetor"),
            SetorComercialId = comercial,
            SetorFinanceiroId = financeiro,
            TokensPorUsuario = tokens,
        };
    }

    private static async Task ExecutarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<int> EscalarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        return Convert.ToInt32(await comando.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
