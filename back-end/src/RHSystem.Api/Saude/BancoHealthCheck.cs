using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.Saude;

/// <summary>Diz se o banco responde. Cada checagem tem 3 segundos: um banco que não responde não pode pendurar a rota de saúde.</summary>
public sealed class BancoHealthCheck(IServiceScopeFactory escopos) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext contexto, CancellationToken ct = default)
    {
        try
        {
            await using var escopo = escopos.CreateAsyncScope();
            var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TimeSpan.FromSeconds(3));

            return await db.Database.CanConnectAsync(limite.Token)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("O banco de dados não respondeu.");
        }
        catch (Exception erro)
        {
            return HealthCheckResult.Unhealthy("O banco de dados não respondeu.", erro);
        }
    }
}
