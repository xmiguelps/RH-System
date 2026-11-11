namespace RHSystem.Infrastructure.Auditoria;

/// <summary>
/// O estado da auditoria de uma requisição (ou de uma tarefa em segundo plano): se o registro automático está suspenso e se o interceptor está gravando os
/// próprios eventos (a trava contra reentrada: o <c>SaveChanges</c> que grava o evento passa de novo pelo interceptor).
/// </summary>
public sealed class ContextoDeAuditoria
{
    private int _suspensoes;

    public bool Suspenso => _suspensoes > 0;

    /// <summary>Verdadeiro enquanto o interceptor grava os eventos que ele mesmo montou.</summary>
    public bool EmSalvamentoDeAuditoria { get; internal set; }

    /// <summary>O interceptor não registra nada até o <c>Dispose</c> (as suspensões se somam).</summary>
    public IDisposable Suspender()
    {
        Interlocked.Increment(ref _suspensoes);
        return new Retomada(this);
    }

    private sealed class Retomada(ContextoDeAuditoria dono) : IDisposable
    {
        private int _retomada;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _retomada, 1) == 0)
            {
                Interlocked.Decrement(ref dono._suspensoes);
            }
        }
    }
}
