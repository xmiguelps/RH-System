using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Autenticacao;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Autenticacao;

public sealed class UsuarioRepositorio(AppDbContext db) : IUsuarioRepositorio
{
    // Nível, cargo e setor são opcionais no banco (aceitam NULL): o EF usa LEFT JOIN e o usuário aparece mesmo sem eles.
    private IQueryable<Usuario> ComDependencias =>
        db.Usuarios
            .Include(u => u.Funcionario).ThenInclude(f => f.NivelDeAcesso)
            .Include(u => u.Funcionario).ThenInclude(f => f.Cargo)
            .Include(u => u.Funcionario).ThenInclude(f => f.Setor);

    /// <summary>A comparação ignora maiúsculas e minúsculas pela collation do banco (utf8mb4_unicode_ci).</summary>
    public Task<Usuario?> ObterPorEmailCorporativoAsync(string emailNormalizado, CancellationToken ct) =>
        ComDependencias.FirstOrDefaultAsync(u => u.Funcionario.EmailCorporativo == emailNormalizado, ct);

    public Task<Usuario?> ObterPorIdAsync(int id, CancellationToken ct) =>
        ComDependencias.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<Usuario?> ObterPorFuncionarioIdAsync(int funcionarioId, CancellationToken ct) =>
        ComDependencias.FirstOrDefaultAsync(u => u.FuncionarioId == funcionarioId, ct);
}
