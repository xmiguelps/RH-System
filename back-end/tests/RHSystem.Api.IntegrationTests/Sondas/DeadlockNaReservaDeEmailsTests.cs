using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Emails;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Comum;
using RHSystem.Application.Emails;

namespace RHSystem.Api.IntegrationTests.Sondas;

/// <summary>
/// Teste de estresse da reserva da fila de e-mails: vários processadores ao mesmo tempo, rodada após rodada no mesmo banco, contando o erro <b>1213</b> (deadlock) do MySQL.
/// Nasceu de uma falha intermitente de <c>Dois_processadores_simultaneos_nunca_pegam_a_mesma_mensagem</c> ("Deadlock found when trying to get lock" em
/// <c>ProcessadorDeEmails.ReservarAsync</c>). Medido ANTES da correção, com 4 processadores e 120 mensagens por rodada: de 54 a 77 deadlocks em 15 rodadas, nas três vezes que
/// rodou. A causa tratada é o isolamento padrão (REPEATABLE READ): a reserva agora abre a transação em READ COMMITTED. Um deadlock na reserva é recuperável (a transação desfaz e o
/// próximo ciclo repete), mas aqui o contrato é zero, e nenhuma mensagem pode sair duas vezes.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class DeadlockNaReservaDeEmailsTests(MySqlFixture mysql)
{
    private const int Rodadas = 6;
    private const int Processadores = 4;
    private const int MensagensPorRodada = 120;

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
    public async Task Varios_processadores_simultaneos_nunca_dao_deadlock_nem_enviam_a_mesma_mensagem_duas_vezes()
    {
        var api = await ApiDeEmails.ComecarAsync(mysql);
        api.Enviador.Pausa = TimeSpan.FromMilliseconds(3); // ciclos curtos: mais reservas ao mesmo tempo
        var deadlocks = 0;
        var outrosErros = new ConcurrentBag<string>();

        for (var rodada = 0; rodada < Rodadas; rodada++)
        {
            await using (var escopo = api.Services.CreateAsyncScope())
            {
                var emails = Enumerable.Range(0, MensagensPorRodada)
                    .Select(i => rodada * 1000 + i)
                    .Select(n => new EmailParaEnfileirar("TESTE_ENVIO", ChaveDeIdempotencia.Montar("TESTE_ENVIO", ("n", n)), $"pessoa{n}@empresa.com.br", new { Nome = "Ana" }, null, null, false, null))
                    .ToArray();
                await escopo.ServiceProvider.GetRequiredService<IFilaDeEmails>().EnfileirarLoteAsync(emails, CancellationToken.None);
            }

            using var barreira = new Barrier(Processadores);
            var tarefas = Enumerable.Range(0, Processadores).Select(_ => Task.Factory.StartNew(
                async () =>
                {
                    barreira.SignalAndWait();
                    var vazios = 0;
                    while (vazios < 2) // dois ciclos seguidos sem nada: a fila secou para este processador
                    {
                        try
                        {
                            await using var escopo = api.Services.CreateAsyncScope();
                            var tratadas = await escopo.ServiceProvider.GetRequiredService<IProcessadorDeEmails>().ProcessarCicloAsync(CancellationToken.None);
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

        var enviadas = api.Enviador.Enviadas;
        Assert.Empty(outrosErros);
        Assert.Equal((Rodadas * MensagensPorRodada, Rodadas * MensagensPorRodada), (enviadas.Count, enviadas.Select(m => m.Para).Distinct().Count())); // todas saíram, nenhuma duas vezes
        Assert.True(deadlocks == 0, $"{deadlocks} deadlock(s) (erro 1213) em {Rodadas} rodadas de {Processadores} processadores");
    }
}
