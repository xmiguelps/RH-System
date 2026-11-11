using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Infrastructure.Agendamento;

namespace RHSystem.Api.IntegrationTests.Agendamento;

/// <summary>
/// O laço em segundo plano, sem banco: o executor é um falso que conta as chamadas (o que ele faz de verdade está em <c>ExecutorDeTarefasTests</c>). Fica na coleção do MySQL só
/// para não rodar junto com os testes de banco (a memória da máquina é curta).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AgendadorHostedServiceTests
{
    private sealed class ExecutorFalso : IExecutorDeTarefas
    {
        private int _recuperacoes;
        private int _ciclos;

        public TaskCompletionSource PrimeiraRecuperacao { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Func<int, bool> LancarNoCiclo { get; set; } = _ => false;

        public int Recuperacoes => Volatile.Read(ref _recuperacoes);

        public int Ciclos => Volatile.Read(ref _ciclos);

        public Task<int> RecuperarAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref _recuperacoes);
            PrimeiraRecuperacao.TrySetResult();
            return Task.FromResult(7);
        }

        public Task<int> ExecutarCicloAsync(CancellationToken ct)
        {
            var n = Interlocked.Increment(ref _ciclos);
            return LancarNoCiclo(n) ? throw new InvalidOperationException("o ciclo quebrou (de propósito)") : Task.FromResult(0);
        }
    }

    private static (AgendadorHostedService Servico, ExecutorFalso Executor, ColetorDeLogs Log) Montar(bool ativo = true, int intervaloSegundos = 1)
    {
        var coletor = new ColetorDeLogs();
        var executor = new ExecutorFalso();
        var provedor = new ServiceCollection()
            .AddSingleton<IExecutorDeTarefas>(executor)
            .AddLogging(b => b.AddProvider(coletor))
            .BuildServiceProvider();
        var servico = new AgendadorHostedService(
            provedor.GetRequiredService<IServiceScopeFactory>(),
            new OpcoesDoAgendador { Ativo = ativo, IntervaloSegundos = intervaloSegundos, RecuperacaoMinutos = 60 },
            provedor.GetRequiredService<ILogger<AgendadorHostedService>>());
        return (servico, executor, coletor);
    }

    private static async Task EsperarAsync(Func<bool> condicao, string oQue)
    {
        var limite = DateTime.UtcNow.AddSeconds(15);
        while (!condicao())
        {
            Assert.True(DateTime.UtcNow < limite, $"passaram 15 s e não aconteceu: {oQue}");
            await Task.Delay(50);
        }
    }

    private static bool TemNoLog(ColetorDeLogs log, string trecho)
    {
        lock (log.Mensagens)
        {
            return log.Mensagens.Any(m => m.Contains(trecho, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Na_subida_chama_RecuperarAsync_de_todos_os_tipos_e_registra_o_inicio_no_log()
    {
        var (servico, executor, log) = Montar();

        await servico.StartAsync(CancellationToken.None);
        await EsperarAsync(() => TemNoLog(log, "Agendador iniciado"), "o registro do início no log");

        Assert.Equal(1, executor.Recuperacoes); // na subida, uma vez
        Assert.True(TemNoLog(log, "Agendador iniciado: 7 tipo(s) recuperado(s)")); // com quantos tipos o executor recuperou
        await EsperarAsync(() => executor.Ciclos >= 1, "o primeiro ciclo (roda logo depois da recuperação, sem esperar o intervalo)");
        await servico.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Com_Agendador_Ativo_false_nao_roda_nada()
    {
        var (servico, executor, log) = Montar(ativo: false);

        await servico.StartAsync(CancellationToken.None);
        await servico.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5)); // o laço termina na hora
        await Task.Delay(1500); // mais que um intervalo: se estivesse rodando, já teria feito um ciclo
        await servico.StopAsync(CancellationToken.None);

        Assert.Equal((0, 0), (executor.Recuperacoes, executor.Ciclos));
        Assert.True(TemNoLog(log, "Agendador desligado"));
    }

    [Fact]
    public async Task Cancelar_o_servico_encerra_o_laco_sem_lancar()
    {
        var (servico, executor, _) = Montar();
        await servico.StartAsync(CancellationToken.None);
        await executor.PrimeiraRecuperacao.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await servico.StopAsync(CancellationToken.None); // não lança

        Assert.True(servico.ExecuteTask!.IsCompletedSuccessfully); // e o laço terminou sem falha
    }

    [Fact]
    public async Task Erro_num_ciclo_vai_para_o_log_e_o_laco_segue()
    {
        var (servico, executor, log) = Montar();
        executor.LancarNoCiclo = n => n == 1; // só o primeiro ciclo quebra

        await servico.StartAsync(CancellationToken.None);
        await EsperarAsync(() => executor.Ciclos >= 2, "o segundo ciclo, depois de o primeiro quebrar");
        await servico.StopAsync(CancellationToken.None);

        Assert.True(TemNoLog(log, "Falha no ciclo do agendador: InvalidOperationException")); // só o NOME da exceção
        Assert.False(TemNoLog(log, "o ciclo quebrou")); // nunca a mensagem dela
    }
}
