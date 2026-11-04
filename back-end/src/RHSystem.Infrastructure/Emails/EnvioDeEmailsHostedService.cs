using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Emails;

namespace RHSystem.Infrastructure.Emails;

/// <summary>
/// O laço que chama <see cref="IProcessadorDeEmails.ProcessarCicloAsync"/> a cada <c>Email:IntervaloSegundos</c>, num escopo novo por ciclo. Com
/// <c>Email:EnvioAtivo = false</c> (o padrão do <c>appsettings.json</c>) ele nem começa: a fila só acumula. Erro num ciclo vai para o log e o próximo ciclo segue.
/// </summary>
public sealed class EnvioDeEmailsHostedService(IServiceScopeFactory escopos, OpcoesDeEmail opcoes, ILogger<EnvioDeEmailsHostedService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken parada)
    {
        if (!opcoes.EnvioAtivo)
        {
            log.LogInformation("Envio de e-mails desligado (Email:EnvioAtivo = false): a fila só acumula.");
            return;
        }

        log.LogInformation("Envio de e-mails ligado: um ciclo a cada {Segundos} s, até {Lote} mensagens por ciclo.", opcoes.IntervaloSegundos, opcoes.LoteMaximoPorCiclo);
        using var relogio = new PeriodicTimer(TimeSpan.FromSeconds(opcoes.IntervaloSegundos));
        try
        {
            do
            {
                try
                {
                    await using var escopo = escopos.CreateAsyncScope();
                    await escopo.ServiceProvider.GetRequiredService<IProcessadorDeEmails>().ProcessarCicloAsync(parada);
                }
                catch (Exception erro) when (erro is not OperationCanceledException)
                {
                    log.LogError("Falha no ciclo de envio de e-mails: {Erro}", erro.GetType().Name);
                }
            }
            while (await relogio.WaitForNextTickAsync(parada));
        }
        catch (OperationCanceledException) when (parada.IsCancellationRequested)
        {
            // parou junto com a aplicação
        }
    }
}
