using System.Data;
using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Autenticacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Autenticacao;

public sealed class UnidadeDeTrabalho(AppDbContext db) : IUnidadeDeTrabalho
{
    public async Task SalvarAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);

    /// <summary>Confirma se a ação termina; se ela lança, a transação é desfeita (o descarte sem Commit faz o Rollback).</summary>
    public async Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> acao, CancellationToken ct)
    {
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        var resultado = await acao();
        await transacao.CommitAsync(ct);
        return resultado;
    }

    /// <summary>O mesmo, com o isolamento da transação escolhido (<c>SET TRANSACTION ISOLATION LEVEL</c> só para ela, antes do <c>START TRANSACTION</c>).</summary>
    public async Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> acao, IsolationLevel nivel, CancellationToken ct)
    {
        await using var transacao = await db.Database.BeginTransactionAsync(nivel, ct);
        var resultado = await acao();
        await transacao.CommitAsync(ct);
        return resultado;
    }
}
