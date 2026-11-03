using Microsoft.Extensions.Logging;

namespace RHSystem.Api.IntegrationTests.Infra;

/// <summary>Guarda em memória tudo o que a API registra em log, para o teste conferir o que foi (ou não foi) escrito.</summary>
public sealed class ColetorDeLogs : ILoggerProvider
{
    public List<string> Mensagens { get; } = [];

    public ILogger CreateLogger(string categoria) => new Registrador(Mensagens);

    public void Dispose()
    {
    }

    private sealed class Registrador(List<string> mensagens) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (mensagens)
            {
                mensagens.Add($"[{logLevel}] " + formatter(state, exception) + (exception?.ToString() ?? string.Empty));
            }
        }
    }
}
