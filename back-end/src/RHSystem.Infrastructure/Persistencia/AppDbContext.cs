using Microsoft.EntityFrameworkCore;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Infrastructure.Persistencia;

/// <summary>
/// O EF Core só mapeia e consulta: as tabelas vêm dos scripts SQL versionados (sem migrations, sem EnsureCreated). O mapeamento é
/// explícito (tabela e coluna por propriedade) e o teste MapeamentoContraBancoTests o confere contra o banco criado pelos scripts.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<NivelDeAcesso> NiveisDeAcesso => Set<NivelDeAcesso>();

    public DbSet<Cargo> Cargos => Set<Cargo>();

    public DbSet<Setor> Setores => Set<Setor>();

    public DbSet<Funcionario> Funcionarios => Set<Funcionario>();

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
