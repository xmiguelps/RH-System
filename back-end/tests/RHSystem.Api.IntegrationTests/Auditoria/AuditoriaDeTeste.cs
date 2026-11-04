using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Escopo;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Auditoria;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Auditoria;

// Entidades que só existem nos testes: o interceptor lê as marcas por reflexão, então serve qualquer classe [Auditado]. As tabelas são criadas
// pelo próprio teste, no banco de teste.

[Auditado(AuditoriaArea.Cadastros, "Categoria")]
public class CategoriaDeTeste : IAuditavel
{
    public int Id { get; set; }

    [Auditar("Nome")]
    public string Nome { get; set; } = string.Empty;

    public string NomeParaAuditoria => Nome;

    public string RegistroAfetado => $"Categoria {Nome}";
}

[Auditado(AuditoriaArea.Cadastros, "Item")]
public class ItemDeTeste : IAuditavel
{
    public int Id { get; set; }

    [Auditar("Nome")]
    public string Nome { get; set; } = string.Empty;

    [Auditar("Situação")]
    public bool Ativo { get; set; } = true;

    [Auditar("Categoria")]
    public int? CategoriaId { get; set; }

    [Auditar("Senha", Sensivel = true)]
    public string? Senha { get; set; }

    [Auditar("Dia")]
    public DateOnly? Dia { get; set; }

    public string? NaoMarcada { get; set; }

    public string NomeParaAuditoria => Nome;

    public string RegistroAfetado => $"Item {Nome}";
}

/// <summary>Rótulo e nome vazios: a descrição do evento fica vazia e o evento é recusado (a alteração tem de desfazer junto).</summary>
[Auditado(AuditoriaArea.Cadastros, "")]
public class ItemMudoDeTeste : IAuditavel
{
    public int Id { get; set; }

    [Auditar("Valor")]
    public string Valor { get; set; } = string.Empty;

    public string NomeParaAuditoria => string.Empty;

    public string RegistroAfetado => "Registro mudo";
}

public class SemAuditoriaDeTeste
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;
}

/// <summary>O <see cref="AppDbContext"/> de verdade mais as tabelas dos testes de auditoria (o EF guarda um modelo por tipo de contexto).</summary>
public sealed class ContextoAuditadoDeTeste(DbContextOptions<AppDbContext> opcoes) : AppDbContext(opcoes)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<CategoriaDeTeste>(e =>
        {
            e.ToTable("TESTE_CATEGORIAS");
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.Nome).HasColumnName("NOME");
            e.Ignore(x => x.NomeParaAuditoria);
            e.Ignore(x => x.RegistroAfetado);
        });
        modelBuilder.Entity<ItemDeTeste>(e =>
        {
            e.ToTable("TESTE_ITENS");
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.Nome).HasColumnName("NOME");
            e.Property(x => x.Ativo).HasColumnName("ATIVO");
            e.Property(x => x.CategoriaId).HasColumnName("ID_CATEGORIA");
            e.Property(x => x.Senha).HasColumnName("SENHA");
            e.Property(x => x.Dia).HasColumnName("DIA");
            e.Property(x => x.NaoMarcada).HasColumnName("NAO_MARCADA");
            e.Ignore(x => x.NomeParaAuditoria);
            e.Ignore(x => x.RegistroAfetado);
            e.HasOne<CategoriaDeTeste>().WithMany().HasForeignKey(x => x.CategoriaId);
        });
        modelBuilder.Entity<ItemMudoDeTeste>(e =>
        {
            e.ToTable("TESTE_ITENS_MUDOS");
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.Valor).HasColumnName("VALOR");
            e.Ignore(x => x.NomeParaAuditoria);
            e.Ignore(x => x.RegistroAfetado);
        });
        modelBuilder.Entity<SemAuditoriaDeTeste>(e =>
        {
            e.ToTable("TESTE_SEM_AUDITORIA");
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.Nome).HasColumnName("NOME");
        });
    }
}

/// <summary>Uma fábrica de contexto simples (sem o interceptor), para o <c>Auditor</c> gravar em "contexto próprio" nos testes.</summary>
public sealed class FabricaDeContextoDeTeste(string conexao) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => ContextoDeTeste.Novo(conexao);
}

internal sealed record EventoLido(
    long Id,
    DateTime Quando,
    TipoDeAtor AtorTipo,
    int? FuncionarioAtorId,
    string AtorNome,
    Perfil? AtorPerfil,
    AuditoriaArea Area,
    AuditoriaAcao Acao,
    string Descricao,
    string RegistroAfetado,
    string? Entidade,
    long? IdRegistro,
    CanalDeOrigem Canal,
    string? Ip,
    IReadOnlyList<MudancaDeAuditoria> Mudancas);

internal static class AuditoriaDeTeste
{
    public const string Ip = "10.0.0.7";

    /// <summary>Cria as tabelas dos testes e uma pessoa que age (com usuário), no banco que já tem os scripts. Devolve a pessoa.</summary>
    public static async Task<PessoaDeTeste> PrepararBancoAsync(string conexao)
    {
        foreach (var sql in new[]
        {
            "CREATE TABLE TESTE_CATEGORIAS (ID INT AUTO_INCREMENT PRIMARY KEY, NOME VARCHAR(100) NOT NULL)",
            "CREATE TABLE TESTE_ITENS (ID INT AUTO_INCREMENT PRIMARY KEY, NOME VARCHAR(100) NOT NULL, ATIVO BOOLEAN NOT NULL, ID_CATEGORIA INT NULL, SENHA VARCHAR(100) NULL, DIA DATE NULL, NAO_MARCADA VARCHAR(100) NULL, FOREIGN KEY (ID_CATEGORIA) REFERENCES TESTE_CATEGORIAS(ID))",
            "CREATE TABLE TESTE_ITENS_MUDOS (ID INT AUTO_INCREMENT PRIMARY KEY, VALOR VARCHAR(100) NOT NULL)",
            "CREATE TABLE TESTE_SEM_AUDITORIA (ID INT AUTO_INCREMENT PRIMARY KEY, NOME VARCHAR(100) NOT NULL)",
        })
        {
            await ExecutarAsync(conexao, sql);
        }

        return await NovaPessoaAsync(conexao, "Ator de Teste", "ator.de.teste@empresa.com.br", "RH");
    }

    public static async Task<PessoaDeTeste> NovaPessoaAsync(string conexao, string nome, string email, string nivel)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        var nivelDeAcesso = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == nivel);
        var funcionario = Funcionario.Criar(nome, email, new DateOnly(2026, 1, 2), nivelDeAcesso);
        db.Funcionarios.Add(funcionario);
        await db.SaveChangesAsync();
        var usuario = Usuario.Criar(funcionario, new string('x', 60));
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return new PessoaDeTeste(funcionario.Id, usuario.Id, nome, email, funcionario.Perfil!.Value);
    }

    public static ContextoAuditadoDeTeste NovoContexto(string conexao, IAtorAtual ator, RelogioDeTeste relogio, ContextoDeAuditoria? contextoDeAuditoria = null)
    {
        var interceptor = new AuditoriaInterceptor(ator, relogio, contextoDeAuditoria ?? new ContextoDeAuditoria());
        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(conexao, new MySqlServerVersion(new Version(8, 4, 0)))
            .AddInterceptors(interceptor)
            .Options;
        return new ContextoAuditadoDeTeste(opcoes);
    }

    public static AtorDeTeste PessoaComIp(PessoaDeTeste pessoa) =>
        new() { Tipo = TipoDeAtorAtual.Pessoa, UsuarioId = pessoa.UsuarioId, FuncionarioId = pessoa.FuncionarioId, Perfil = pessoa.Perfil, Nome = pessoa.Nome, Ip = Ip };

    public static RelogioDeTeste RelogioEm(DateTime utc)
    {
        var relogio = new RelogioDeTeste();
        relogio.Fixar(utc);
        return relogio;
    }

    /// <summary>Os eventos da entidade (e do registro, se informado), lidos por um contexto SEM interceptor, mais novos por último.</summary>
    public static async Task<List<EventoLido>> EventosAsync(string conexao, string entidade, long? idRegistro = null)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        var consulta = db.EventosDeAuditoria.Include(e => e.Mudancas).Where(e => e.Entidade == entidade);
        if (idRegistro is not null)
        {
            consulta = consulta.Where(e => e.IdRegistro == idRegistro);
        }

        var eventos = await consulta.OrderBy(e => e.Id).ToListAsync();
        return [.. eventos.Select(e => new EventoLido(
            e.Id, e.Quando, e.AtorTipo, e.FuncionarioAtorId, e.AtorNome, e.AtorPerfil, e.Area, e.Acao, e.Descricao, e.RegistroAfetado,
            e.Entidade, e.IdRegistro, e.Canal, e.Ip, [.. e.Mudancas.OrderBy(m => m.Ordem)]))];
    }

    public static async Task ExecutarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        await comando.ExecuteNonQueryAsync();
    }

    public static async Task<long> EscalarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        return Convert.ToInt64(await comando.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
