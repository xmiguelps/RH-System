using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Autenticacao;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Autenticacao;

public sealed class RefreshTokenRepositorio(AppDbContext db) : IRefreshTokenRepositorio
{
    /// <summary>Só marca para inclusão; quem grava é a <see cref="UnidadeDeTrabalho"/> (junto com a revogação do antigo, na mesma transação).</summary>
    public Task AdicionarAsync(RefreshToken token, CancellationToken ct)
    {
        db.RefreshTokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> ObterPorHashAsync(string tokenHash, CancellationToken ct) =>
        db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    /// <summary>
    /// Atômico: um <c>UPDATE ... WHERE TOKEN_HASH = @h AND REVOGADO_EM IS NULL</c> só altera uma linha uma vez. Duas renovações simultâneas
    /// com o mesmo token: uma recebe <c>true</c>, a outra <c>false</c>. Roda na transação corrente, se houver.
    /// </summary>
    public async Task<bool> TentarRevogarAsync(string tokenHash, DateTime agoraUtc, CancellationToken ct)
    {
        var linhas = await db.RefreshTokens
            .Where(t => t.TokenHash == tokenHash && t.RevogadoEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevogadoEm, agoraUtc), ct);
        return linhas == 1;
    }

    public async Task RevogarTodosDoUsuarioAsync(int usuarioId, DateTime agoraUtc, CancellationToken ct)
    {
        await db.RefreshTokens
            .Where(t => t.UsuarioId == usuarioId && t.RevogadoEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevogadoEm, agoraUtc), ct);
    }
}
