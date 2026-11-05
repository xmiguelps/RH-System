using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Agendamento;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Comum;

namespace RHSystem.Api.IntegrationTests.Sondas;

/// <summary>
/// O mesmo teste de estresse da reserva da fila de e-mails (<see cref="DeadlockNaReservaDeEmailsTests"/>), agora para a reserva do agendador, que usa a mesma técnica
/// (<c>SELECT ... FOR UPDATE SKIP LOCKED</c> seguido de <c>UPDATE</c> em colunas indexadas): vários executores ao mesmo tempo, contando o erro <b>1213</b> (deadlock) do MySQL.
/// Medido ANTES da correção (reserva em REPEATABLE READ), com 4 executores e 120 tarefas por rodada: de 73 a 79 deadlocks em 15 rodadas.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class DeadlockNaReservaDeTarefasTests(MySqlFixture mysql)
{
    private const int Rodadas = 6;
    private const int Executores = 4;
    private const int TarefasPorRodada = 120;

    private static bool EDeadlock(Exception erro)
    {
        for (var atual = erro; atual is not null; atual = atual.InnerException)
        {
            if (atual is MySqlException { Number: 1213 })
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public async Task Varios_executores_simultaneos_nunca_dao_deadlock_nem_executam_a_mesma_tarefa_duas_vezes()
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        api.Tarefas.Pausa = TimeSpan.FromMilliseconds(3); // ciclos curtos: mais reservas ao mesmo tempo
        var deadlocks = 0;
        var outrosErros = new ConcurrentBag<string>();

        for (var rodada = 0; rodada < Rodadas; rodada++)
        {
            await using (var escopo = api.Services.CreateAsyncScope())
            {
                var agendador = escopo.ServiceProvider.GetRequiredService<IAgendador>();
                for (var i = 0; i < TarefasPorRodada; i++)
                {
                    var n = rodada * 1000 + i;
                    await agendador.AgendarAsync(new NovaTarefa("TESTE_TAREFA", ChaveDeIdempotencia.Montar("TESTE_TAREFA", ("n", n)), ElencoDeTeste.Hoje, ReferenciaId: n), CancellationToken.None);
                }
            }

            using var barreira = new Barrier(Executores);
            var tarefas = Enumerable.Range(0, Executores).Select(_ => Task.Factory.StartNew(
                async () =>
                {
                    barreira.SignalAndWait();
                    var vazios = 0;
                    while (vazios < 2) // dois ciclos seguidos sem nada: a fila secou para este executor
                    {
                        try
                        {
                            await using var escopo = api.Services.CreateAsyncScope();
                            var tratadas = await escopo.ServiceProvider.GetRequiredService<IExecutorDeTarefas>().ExecutarCicloAsync(CancellationToken.None);
                            vazios = tratadas == 0 ? vazios + 1 : 0;
                        }
                        catch (Exception erro)
                        {
                            // Como o serviço em segundo plano: o ciclo que falhou é descartado e o próximo repete.
                            if (EDeadlock(erro))
                            {
                                Interlocked.Increment(ref deadlocks);
                            }
                            else
                            {
                                outrosErros.Add(erro.GetType().Name);
                            }
                        }
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap()).ToArray();

            await Task.WhenAll(tarefas);
        }

        var execucoes = api.Tarefas.Execucoes;
        Assert.Empty(outrosErros);
        Assert.Equal((Rodadas * TarefasPorRodada, Rodadas * TarefasPorRodada), (execucoes.Count, execucoes.Select(e => e.Id).Distinct().Count())); // todas rodaram, nenhuma duas vezes
        Assert.True(deadlocks == 0, $"{deadlocks} deadlock(s) (erro 1213) em {Rodadas} rodadas de {Executores} executores");
    }
}
