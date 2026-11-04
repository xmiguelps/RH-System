using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Agendamento;

namespace RHSystem.Infrastructure.Agendamento;

/// <summary>
/// O laço do agendador. Na subida chama <c>RecuperarAsync</c> (e registra "Agendador iniciado" com quantos tipos recuperou), depois roda um ciclo a cada
/// <c>Agendador:IntervaloSegundos</c> e a recuperação de novo a cada <c>Agendador:RecuperacaoMinutos</c>. Cada chamada tem escopo novo. Erro num ciclo vai para o log e o
/// próximo ciclo segue. Com <c>Agendador:Ativo = false</c> nem começa. <b>Exige Always On</b> (plano pago do App Service): com a API dormindo nada roda, e ao acordar a
/// recuperação refaz o que passou.
/// </summary>
public sealed class AgendadorHostedService(IServiceScopeFactory escopos, OpcoesDoAgendador opcoes, ILogger<AgendadorHostedService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken parada)
    {
        if (!opcoes.Ativo)
        {
            log.LogInformation("Agendador desligado (Agendador:Ativo = false): nenhuma tarefa roda.");
            return;
        }

        try
        {
            var recuperados = await RecuperarAsync(parada);
            log.LogInformation(
                "Agendador iniciado: {Tipos} tipo(s) recuperado(s); um ciclo a cada {Segundos} s e recuperação a cada {Minutos} min.",
                recuperados,
                opcoes.IntervaloSegundos,
                opcoes.RecuperacaoMinutos);

            using var relogio = new PeriodicTimer(TimeSpan.FromSeconds(opcoes.IntervaloSegundos));
            var proximaRecuperacao = DateTime.UtcNow.AddMinutes(opcoes.RecuperacaoMinutos); // o relógio de verdade: aqui só se mede o intervalo do laço
            do
            {
                if (DateTime.UtcNow >= proximaRecuperacao)
                {
                    await RecuperarAsync(parada);
                    proximaRecuperacao = DateTime.UtcNow.AddMinutes(opcoes.RecuperacaoMinutos);
                }

                await ExecutarCicloAsync(parada);
            }
            while (await relogio.WaitForNextTickAsync(parada));
        }
        catch (OperationCanceledException) when (parada.IsCancellationRequested)
        {
            // parou junto com a aplicação
        }
    }

    private async Task<int> RecuperarAsync(CancellationToken parada)
    {
        try
        {
            await using var escopo = escopos.CreateAsyncScope();
            return await escopo.ServiceProvider.GetRequiredService<IExecutorDeTarefas>().RecuperarAsync(parada);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            log.LogError("Falha na recuperação do agendador: {Erro}", erro.GetType().Name);
            return 0;
        }
    }

    private async Task ExecutarCicloAsync(CancellationToken parada)
    {
        try
        {
            await using var escopo = escopos.CreateAsyncScope();
            await escopo.ServiceProvider.GetRequiredService<IExecutorDeTarefas>().ExecutarCicloAsync(parada);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            log.LogError("Falha no ciclo do agendador: {Erro}", erro.GetType().Name);
        }
    }
}
