using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Persistencia;

/// <summary>
/// Teste de fumaça do provider Pomelo 9.0.0 com EF Core 9 e MySQL 8.4: é o maior risco do spec. Incompatibilidade aparece como
/// NU1608, NU1107, MissingMethodException ou TypeLoadException ao usar UseMySql ou ao consultar.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ProviderMySqlSmokeTests(MySqlFixture mysql)
{
    // O trigger de USUARIOS recusa senha com menos de 40 caracteres: um valor do tamanho de um BCrypt.
    private const string HashDeTeste = "$2a$11$abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456";

    private static readonly DateOnly Admissao = new(2026, 1, 2);

    private static async Task<NivelDeAcesso> NivelAsync(AppDbContext db, string nome) =>
        await db.NiveisDeAcesso.SingleAsync(n => n.Nome == nome);

    private static async Task ExecutarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        await comando.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Funcionario_e_usuario_fazem_ida_e_volta_com_unicode_e_perfil_pelo_nivel()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var funcionario = Funcionario.Criar("José Ação 😀", "Jose.Acao@Empresa.com.br", Admissao, await NivelAsync(escrita, "RH"));
            escrita.Usuarios.Add(Usuario.Criar(funcionario, HashDeTeste));
            await escrita.SaveChangesAsync();
        }

        await using var leitura = ContextoDeTeste.Novo(conexao); // outro contexto: nada vem do cache do primeiro
        var usuario = await leitura.Usuarios
            .Include(u => u.Funcionario).ThenInclude(f => f.NivelDeAcesso)
            .SingleAsync(u => u.Funcionario.EmailCorporativo == "jose.acao@empresa.com.br");

        Assert.Equal("José Ação 😀", usuario.Funcionario.Nome);
        Assert.Equal(Admissao, usuario.Funcionario.DataAdmissao);
        Assert.Equal(Perfil.RH, usuario.Funcionario.Perfil);
        Assert.True(usuario.PodeEntrar);
        Assert.Equal(HashDeTeste, usuario.SenhaHash);
        Assert.Null(usuario.UltimoLoginEm);
    }

    [Fact]
    public async Task Email_corporativo_duplicado_viola_a_chave_unica()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using var db = ContextoDeTeste.Novo(conexao);
        var nivel = await NivelAsync(db, "Funcionario");
        db.Funcionarios.Add(Funcionario.Criar("Ana", "ana@empresa.com.br", Admissao, nivel));
        await db.SaveChangesAsync();

        await using var outro = ContextoDeTeste.Novo(conexao);
        outro.Funcionarios.Add(Funcionario.Criar("Outra Ana", "ana@empresa.com.br", Admissao, await NivelAsync(outro, "Funcionario")));

        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => outro.SaveChangesAsync());

        var mysqlErro = Assert.IsType<MySqlException>(erro.InnerException);
        Assert.Equal(MySqlErrorCode.DuplicateKeyEntry, mysqlErro.ErrorCode);
    }

    [Fact]
    public async Task Erro_de_trigger_chega_como_DbUpdateException_com_SQLSTATE_45000()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using var db = ContextoDeTeste.Novo(conexao);
        var funcionario = Funcionario.Criar("Inativo", "inativo@empresa.com.br", Admissao, await NivelAsync(db, "Funcionario"));
        db.Funcionarios.Add(funcionario);
        await db.SaveChangesAsync();
        funcionario.Inativar(); // o trigger recusa criar usuário para inativo: cria ativo e inativa depois
        await db.SaveChangesAsync();

        db.Usuarios.Add(Usuario.Criar(funcionario, HashDeTeste));
        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var mysqlErro = Assert.IsType<MySqlException>(erro.InnerException);
        Assert.Equal("45000", mysqlErro.SqlState);
        Assert.Contains("Nao e possivel criar usuario para funcionario inativo.", mysqlErro.Message);
    }

    [Fact]
    public async Task Funcionario_com_cargo_e_setor_carrega_os_nomes_e_sem_cargo_nem_setor_carrega_null()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var nivel = await NivelAsync(escrita, "Funcionario");
            escrita.Funcionarios.Add(Funcionario.Criar("Com Cargo", "com.cargo@empresa.com.br", Admissao, nivel));
            escrita.Funcionarios.Add(Funcionario.Criar("Sem Cargo", "sem.cargo@empresa.com.br", Admissao, nivel));
            await escrita.SaveChangesAsync();
        }

        // Cargo e Setor ainda não têm como ser criados pela aplicação (a fatia 1c cadastra): entram por SQL.
        await ExecutarAsync(conexao, "INSERT INTO CARGOS (NOME_CARGO) VALUES ('Analista de RH')");
        await ExecutarAsync(conexao, "INSERT INTO SETORES (NOME_SETOR) VALUES ('Recursos Humanos')");
        await ExecutarAsync(conexao, "UPDATE FUNCIONARIOS SET ID_CARGO = (SELECT ID_CARGO FROM CARGOS LIMIT 1), ID_SETOR = (SELECT ID_SETOR FROM SETORES LIMIT 1) WHERE EMAIL_CORPORATIVO = 'com.cargo@empresa.com.br'");

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var funcionarios = await leitura.Funcionarios.Include(f => f.Cargo).Include(f => f.Setor).OrderBy(f => f.Nome).ToListAsync();

        var comCargo = funcionarios.Single(f => f.Nome == "Com Cargo");
        Assert.Equal("Analista de RH", comCargo.Cargo!.Nome);
        Assert.Equal("Recursos Humanos", comCargo.Setor!.Nome);
        var semCargo = funcionarios.Single(f => f.Nome == "Sem Cargo");
        Assert.Null(semCargo.Cargo);
        Assert.Null(semCargo.Setor);
    }
}
